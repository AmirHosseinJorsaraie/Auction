# وضعیت پیشرفت

## وضعیت فعلی

- مرحله: **Phase 4 — Expert Review & Moderation**
- وضعیت: **پیاده‌سازی backend/frontend و ۳۳ تست غیرکانتینری انجام شده؛ migration/race smoke واقعی در انتظار Docker/CI**
- آخرین به‌روزرسانی: ۲۰۲۶-۱۰-۲۵
- production code نوشته‌شده: Identity/OpenIddict، دو BFF، artwork intake/media، review assignment/finding/decision/supersession، audit/outbox و seller-safe review result
- migration/table/endpoint مربوط به V2: هیچ؛ تغییرات فقط در scope V1 هستند

## کارهای انجام‌شده در Phase 0

- تحلیل scope V1 و جداسازی صریح موارد V2
- پیشنهاد Modular Monolith و Clean Architecture
- تعیین مرز ماژول‌ها و dependency ruleها
- طراحی اولیه aggregateها، ER و state machineها
- پیشنهاد authentication مبتنی بر OIDC/BFF و session امن
- تعریف invariantهای review، publish، reservation، payment و file access
- تعریف سیاست cart/order تک‌فروشنده‌ای پیشنهادی
- مستندسازی migration path برای exclusivity میان Marketplace و Auction در V2
- تدوین backlog و acceptance criteria فازهای ۱ تا ۹
- ثبت ریسک‌ها، فرض‌ها و تصمیم‌های باز

## فایل‌های این مرحله

- `docs/architecture.md`
- `docs/domain-model.md`
- `docs/v2-readiness.md`
- `docs/implementation-plan.md`
- `docs/decisions/0001-modular-monolith-and-clean-architecture.md`
- `docs/decisions/0002-central-oidc-and-bff-sessions.md`
- `docs/decisions/0003-versioned-artwork-and-channel-exclusivity.md`
- `docs/decisions/0004-single-seller-cart-and-order.md`
- `docs/decisions/0005-object-storage-and-file-boundaries.md`
- `docs/decisions/0006-database-authoritative-checkout-and-payment.md`
- `docs/progress.md`
- `docs/learning-notes.md`
- `docs/known-issues.md`

## وضعیت فعلی workspace

- solution نهایی `D:\Projects\Auction\ArtMarketplace.sln` شامل ۱۱ پروژه .NET است.
- Git repository در `D:\Projects\Auction` شناسایی نشده است؛ workflow CI آماده است اما هنوز commit/run نشده است.
- SDK رسمی .NET `10.0.401` به‌صورت portable در پوشه gitignored پروژه استفاده و build موفق شد؛ نصب‌های سراسری همچنان 9.x هستند.
- Node.js `24.19.0` LTS به‌صورت portable استفاده شد؛ runtime سراسری قدیمی Node 20 تغییری نکرد.
- Docker در PATH موجود نیست؛ اجرای compose/Testcontainers تا نصب یا فراهم‌شدن runtime کانتینر ممکن نیست.
- جزئیات build/test/runtime در `docs/phase-1-foundation.md` ثبت شده است.

## شواهد اعتبارسنجی Phase 0

- بررسی همه ۱۳ فایل Markdown: code fenceها متوازن و همه لینک‌های نسبی محلی معتبر (`MARKDOWN_STRUCTURE_OK`).
- `dotnet sln .\Auction.sln list`: خروجی `No projects found in the solution.`؛ بنابراین build/test اجرایی معنادار وجود ندارد.
- `rg --files`: فقط solution خالی و اسناد Phase 0 را نشان داد؛ production code یا artifact مربوط به V2 ساخته نشده است.
- auditهای دامنه، امنیت و ساختار backlog اجرا و همه یافته‌های P0/P1 گزارش‌شده اصلاح شدند؛ بازبینی نهایی امنیت/یکپارچگی مالی مورد P0/P1 حل‌نشده‌ای نیافت و checkهای نهایی consistency روی متن به‌صورت محلی تکرار شدند.

## گام بعدی پس از تأیید

پس از اجرای migration/race smoke در Docker/CI، Phase 5 (Public Gallery & Marketplace Listings) گام بعدی برنامه است.

## کارهای انجام‌شده در Phase 1

- انتقال ریشه به `D:\Projects\Auction` و تغییر نام solution به `ArtMarketplace.sln`
- scaffold پنج لایه backend، Public Web و Backoffice
- pin کردن .NET 10.0.401، Node 24.19.0 و dependencyها همراه lock file
- health، OpenAPI، Problem Details، JSON logging و rate limiting پایه
- PostgreSQL/SeaweedFS Compose با digest ثابت و Dockerfileهای مستقل
- تست‌های واحد، معماری، frontend، Backoffice و migration smoke با Testcontainers
- CI برای restore/build/test/typecheck/lint/audit/contract drift
- اجرای موفق build و تست‌های غیرکانتینری و smoke دستی سه deployable

## کارهای انجام‌شده در Phase 2

- Identity مرکزی با ASP.NET Core Identity و OpenIddict، Authorization Code + PKCE و reference token
- client و session مستقل Public/Backoffice با cookieهای host-only و tokenهای صرفاً server-side
- sessionهای رمز‌شده PostgreSQL با handle digest، timeout مطلق، revoke و refresh rotation
- role/policy/MFA، lockout، confirmed email، recovery و bootstrap بدون password پیش‌فرض
- profile/address self-service و workflow کامل seller verification با version optimistic
- eligibility canonical، invalidation/outbox، audit append-only و expiration worker
- migration و script idempotent، تست‌های domain/architecture/401/403 و build frontend

## کارهای انجام‌شده در Phase 3

- Artwork/Revision نسخه‌دار، Artist/Category، Asset تمیز و Marketplace Listing فقط در حالت Draft
- lock order حساب/eligibility سپس Artwork/Review و unique indexهای race-safe
- upload intent تک‌مصرف، presigned PUT به quarantine، magic/MIME/size validation و rate limit مستقل
- scan fail-closed، SHA-256، promotion همان byte stream با create-if-absent و cleanup quarantine
- decode/re-encode تصویر، حذف metadata و bucket مستقل public derivative/private clean
- endpoint دانلود خصوصی با ownership/staff+MFA و محافظت IDOR
- freeze اتمیک revision و ایجاد ReviewCase/ReviewRound یکتا
- صفحات فارسی `My Artworks` و ثبت metadata/upload/submit با React Hook Form + Zod
- migration و script idempotent Phase 3؛ بدون artifact مربوط به Auction

## کارهای انجام‌شده در Phase 4

- permission صریح `Reviews.Decide` و جلوگیری از اختیار ضمنی Admin
- assignment و conflict-of-interest برای owner/submitter/conflict ثبت‌شده
- finding/evidence با visibility خصوصی و seller-visible
- decision immutable و revision-pinned با optimistic concurrency
- request changes، ساخت revision جدید و re-review در همان case باز
- annotation غیرماهوی و supersession ماهوی بدون بازنویسی تاریخچه
- safeguard case/round اتمیک پس از supersession و notification از طریق Outbox
- API فروشنده بدون private note و صفحه RTL بررسی در Backoffice
- دو migration و script idempotent Phase 4؛ بدون artifact مربوط به Auction

