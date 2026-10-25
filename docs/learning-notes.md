# یادداشت‌های آموزشی

## ۱. Modular Monolith در برابر Clean Architecture

این دو جایگزین هم نیستند:

- **Modular Monolith** شکل مرزبندی قابلیت‌های کسب‌وکار در یک deployment است؛ Identity، Catalog و Orders ماژول‌های جدا ولی هم‌process هستند.
- **Clean Architecture** جهت dependency کد را تعیین می‌کند؛ Domain framework را نمی‌شناسد و Infrastructure به داخل وابسته است.

در این پروژه، پوشه/namespace/schema مرز ماژول را نشان می‌دهد و project reference/architecture test جهت لایه‌ها را کنترل می‌کند.

## ۲. Aggregate و invariant

Aggregate مرز consistency است، نه صرفاً مجموعه‌ای از classها. برای نمونه:

- `MarketplaceListing` transitionهای خودش را کنترل می‌کند.
- `Order` snapshot و status خودش را کنترل می‌کند.
- عملیات checkout چون دو aggregate را هماهنگ می‌کند، یک application use case و transaction مشترک دارد.

قاعده‌ای مثل «اثر تأییدنشده publish نشود» فقط با مخفی کردن دکمه حل نمی‌شود؛ Backend باید آن را دوباره بررسی کند.

## ۳. چرا Artwork از Listing جداست؟

Artwork هویت فیزیکی و provenance پایدار است. Listing فقط یک روش عرضه با قیمت و وضعیت فروش است. اگر price/status روی Artwork قرار گیرد، اضافه شدن Auction باعث enumهای بزرگ، شرط‌های متقاطع و migration پرهزینه می‌شود. جدایی این دو، Marketplace V1 را ساده و مسیر V2 را روشن نگه می‌دارد.

## ۴. چرا ArtworkRevision لازم است؟

کارشناس باید بداند چه نسخه‌ای را دیده و تأیید کرده است. mutation همان رکورد بعد از approval، ارزش review را از بین می‌برد. بنابراین submission نسخه را freeze می‌کند و تغییرات بعدی revision جدید می‌سازند. Listing در Draft هنوز pin تأییدشده ندارد؛ فقط use case انتشار revision دارای تصمیم مؤثر را اتمیک در `PublishedArtworkRevisionId` pin می‌کند.

## ۵. App Router و Server Component

در Next.js App Router، folderها route را تعریف می‌کنند و `page.tsx`/`layout.tsx` قرارداد framework هستند.

- **Server Component** پیش‌فرض است؛ روی سرور render می‌شود، secret یا token را به browser نمی‌دهد و JavaScript کمتری ارسال می‌کند.
- **Client Component** با `'use client'` مرز interaction است و برای state، event handler، hook یا browser API لازم می‌شود.

هدف این نیست که همه چیز Server یا همه چیز Client باشد؛ صفحه عمومی می‌تواند Server Component باشد و فقط فیلتر تعاملی یا modal بخش Client داشته باشد.

## ۶. BFF چیست؟

Backend for Frontend لایه سروری مختص هر UI است. مرورگر با cookie امن به BFF وصل می‌شود و BFF با access token سمت سرور API را فراخوانی می‌کند. نتیجه مهم:

- token در `localStorage` یا JavaScript نیست.
- Public و Admin session/permission مستقل دارند.
- API فقط trust boundary استاندارد Bearer دارد.

BFF جای authorization دامنه را نمی‌گیرد؛ API همچنان owner، role، verification و policy را بررسی می‌کند.

## ۷. role، verification و permission

- role می‌گوید کاربر در عملیات سازمانی چه جایگاهی دارد (`Expert`, `Admin`).
- verification یک وضعیت کسب‌وکاری و زمان‌دار است (`Pending`, `Approved`, `Expired`).
- permission/policy تصمیم عملی است (`CanSubmitArtwork`, `CanReviewArtwork`).

ترکیب این سه در یک enum معمولاً باعث رشد انفجاری حالت‌ها می‌شود.

## ۸. optimistic و pessimistic concurrency

- **Optimistic:** رکورد `Version` دارد؛ update تنها اگر version هنوز همان باشد موفق می‌شود. برای review decision و ویرایش معمول مناسب است.
- **Row lock/conditional update:** در checkout لازم است فقط یک خریدار transition `Published → Reserved` را ببرد. PostgreSQL منبع حقیقت است و lock توزیع‌شده Redis لازم نیست.

UI می‌تواند تجربه optimistic داشته باشد، اما correctness همیشه از transaction سرور می‌آید.

## ۹. snapshot در Order

Order نباید بعداً با تغییر title، price، seller profile یا address گذشته تغییر کند. بنابراین داده قراردادی هنگام checkout snapshot می‌شود. FK برای traceability باقی می‌ماند، ولی نمایش و مبلغ تاریخی از snapshot می‌آید.

## ۱۰. TanStack Query کجا استفاده می‌شود؟

TanStack Query برای داده‌ای مفید است که در Client Component بارها fetch/mutate و invalidate می‌شود؛ مانند review queue تعاملی. صفحه عمومی read-heavy را Server Component می‌تواند مستقیم از API بگیرد. استفاده هم‌زمان بدون policy باعث double caching و رفتار مبهم می‌شود.

## ۱۱. TypeScript type با validation runtime فرق دارد

type/interface بعد از compile وجود ندارد. Zod ورودی runtime فرم یا پاسخ boundary را بررسی می‌کند و TypeScript تجربه توسعه را بهتر می‌کند. هیچ‌کدام جای validation و authorization Backend را نمی‌گیرند.

## ۱۲. RTL فقط `dir="rtl"` نیست

layout، icon، breadcrumb، table، keyboard flow، mixed-direction identifier و اعداد نیاز به بررسی دارند. CSS logical propertyهایی مانند `margin-inline-start` به‌جای `margin-left` امکان RTL پایدارتر می‌دهند. زمان در UTC ذخیره و فقط در UI به locale/تقویم مناسب render می‌شود.

## ۱۳. snapshot تکراری همیشه محافظ نیست

چون Order در V1 تک‌فروشنده است، deadline رزرو و نسخه eligibility برای همه itemها یکسان‌اند. نگهداری همان دو مقدار روی هر Reservation امکان drift می‌سازد. منبع canonical روی Order قرار می‌گیرد و Reservation فقط با `OrderId` اجباری آن را به ارث می‌برد؛ Listing همچنان نسخه eligibility زمان انتشار خودش را دارد تا checkout و payment بتوانند تغییر صلاحیت فروشنده را کشف کنند.

## ۱۴. قفل فقط برای موجودی نیست

انتقال مالکیت با edit/submit اثر و supersession با ایجاد review جدید race دارند. ترتیب lock مشترک و unique index مکمل یکدیگرند: lock اجازه می‌دهد authorization و state جاری دوباره سنجیده شود و index آخرین محافظ invariant است. supersession append-only پرونده بسته را reopen نمی‌کند؛ در race به workflow فعال برنده attach می‌شود تا خطای uniqueness هرگز safeguard یک approval باطل‌شده را گم نکند.

## ۱۵. liveness با readiness فرق دارد

`liveness` می‌گوید process زنده است و نباید به PostgreSQL یا storage وابسته باشد؛ در غیر این صورت اختلال dependency باعث restart loop می‌شود. `readiness` می‌گوید instance فعلاً می‌تواند traffic واقعی بگیرد، پس اتصال واقعی dependencyهای ضروری را بررسی می‌کند.

## ۱۶. environment در Next.js

متغیرهای دارای پیشوند `NEXT_PUBLIC_` داخل bundle مرورگر قرار می‌گیرند و عملاً public هستند. secret و endpoint داخلی باید فقط در Server Component، Route Handler یا runtime سرور خوانده شوند. مقدار build-time ممکن است در artifact ثابت شود، اما runtime environment هنگام اجرای container قابل تغییر است؛ محل خواندن متغیر این تفاوت را تعیین می‌کند.

## ۱۷. Server و Client Component

فایل‌های App Router به‌صورت پیش‌فرض Server Component هستند و JavaScript کمتری به مرورگر می‌فرستند. فقط بخش نیازمند state، event handler یا browser API باید با `"use client"` به Client Component تبدیل شود. صفحه خانه Phase 1 عمداً Server Component باقی مانده است.

