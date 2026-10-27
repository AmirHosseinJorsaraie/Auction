# گزارش Phase 8 — Administration & UI Polish

## هدف و نتیجه

این فاز Backoffice را از یک پوسته نمایشی به ابزار عملیاتی واقعی تبدیل می‌کند. داده‌های dashboard، کاربر، عرضه، سفارش و پرداخت از API خوانده می‌شوند؛ authorization فقط به مخفی‌کردن منو متکی نیست و هر endpoint با permission صریح، scope API و MFA محافظت می‌شود.

## مدل دسترسی

permissionها از role جدا هستند تا اضافه‌شدن یک role به‌تنهایی اختیار نامحدود نسازد:

- `Expert`: تصمیم کارشناسی با `Reviews.Decide`
- `Admin`: مشاهده مدیریت، مدیریت کاربر، catalog/content، audit و payment monitoring
- `SuperAdmin`: همه اختیارهای Admin به‌اضافه `Roles.Manage`
- `User`: بدون دسترسی Backoffice

ماتریس چهار role در تست API بررسی می‌شود. تغییر نقش و وضعیت حساب دلیل اجباری، MFA، audit، افزایش `AuthorizationVersion` و revoke فوری sessionهای BFF همان subject دارد. Admin نمی‌تواند خود را SuperAdmin کند و حذف/غیرفعال‌کردن آخرین SuperAdmin رد می‌شود.

## عملیات کاربر و ایمنی تجاری

غیرفعال‌کردن حساب فقط flag ورود را عوض نمی‌کند. `SellerEligibility` همان کاربر نیز invalidate می‌شود، Listing منتشرشده suspend می‌شود و Order/Listing رزروشده یا فروخته‌شده به عقب برنمی‌گردد؛ به‌جای rollback تجاری، hold ممیزی‌شده روی Order قرار می‌گیرد. فعال‌سازی دوباره حساب approval فروشندگی را خودکار برنمی‌گرداند.

این رفتار از پنجره‌ای جلوگیری می‌کند که حساب از نظر ورود بسته باشد ولی آثارش همچنان قابل خرید یا fulfillment باشند.

## API و نمایش‌های عملیاتی

- dashboard با countهای واقعی کاربران، verification، review، listing، order، reconciliation و audit روز
- queryهای محدود و صفحه‌بندی‌شده برای کاربران، listingها، سفارش‌ها، تلاش‌های پرداخت و audit
- مدیریت category و تعلیق listing با دلیل
- صفحات اطلاع‌رسانی و banner زمان‌دار؛ متن به‌صورت plain text نمایش داده می‌شود و HTML خام اجرا نمی‌شود
- configuration view فقط برای SuperAdmin و فقط از allow-list؛ secret، merchant account و access key همیشه `[redacted]` هستند
- audit view عمداً `DataJson` را برنمی‌گرداند تا payload احتمالی PII در جدول عمومی عملیات نشت نکند

Backoffice مستقیماً `MarketplaceDbContext` را query نمی‌کند. persistence ثبت session پشت `AddBffSessionPersistence` در Infrastructure پنهان است و همه داده عملیاتی از API permission-checked عبور می‌کند.

## UI، RTL و accessibility

صفحات dashboard، کاربران، عملیات، محتوا، audit و configuration با navigation مبتنی بر policy ساخته شدند. حالت loading/empty/error و نتیجه mutation نمایش داده می‌شود. shell فارسی `dir="rtl"`، skip link، main focus target، focus-visible، reduced-motion، table caption و layout responsive دارد. یک guard خودکار markup این موارد پایه را در تست Backoffice ثابت نگه می‌دارد.

Public Web نیز banner فعال و route صفحه اطلاع‌رسانی `/info/{slug}` را از API عمومی می‌گیرد و در قطع API fail-soft عمل می‌کند.

## migration و session rollout

Migration `Phase8AdministrationOperations` جدول‌های content/banner و `SubjectUserId` روی BFF session را اضافه می‌کند. sessionهای قدیمی که subject قابل اتکا ندارند هنگام migration revoke می‌شوند؛ session جدید subject را از claim استاندارد `sub` ذخیره می‌کند تا disable/role-change بتواند آن‌ها را دقیق و فوری باطل کند.

## اعتبارسنجی

- build کل solution با warning/error صفر
- ۹۰ تست غیرکانتینری موفق: Domain=41، Application=17، Architecture=8، Integration=18 و Backoffice=6؛ شامل permission matrix، role safeguards و accessibility markup
- `dotnet ef migrations has-pending-model-changes` بدون model drift
- قرارداد OpenAPI و TypeScript client دوباره تولید شد
- Public Web: typecheck، lint، Vitest و production build موفق

تست PostgreSQL/Testcontainers به Docker نیاز دارد و روی این ماشین که Docker ندارد اجراشدنی نیست؛ این محدودیت به‌عنوان pass گزارش نشده و در CI/Phase 9 باید اجرا شود.

## مواردی که عمداً باقی مانده‌اند

- retention و WORM policy نهایی audit نیازمند تصمیم Legal/Security است.
- مدیریت banner در V1 محدود به ایجاد و انتخاب خودکار جدیدترین banner فعال است؛ scheduling پیشرفته لازم نیست.
- آزمون keyboard-only و responsive با حساب‌های واقعی پس از بالا آمدن stack کامل در Phase 9 تکرار می‌شود.
- production payment/provider، refund و تصمیم حقوقی انتقال مالکیت همچنان prerequisite اعلام readiness هستند.
