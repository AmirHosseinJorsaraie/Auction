# گزارش Phase 9 — Hardening & Delivery

## نتیجه

این فاز آخرین فاز پیاده‌سازی V1 است. هدف آن تبدیل «کد کار می‌کند» به یک release قابل ارزیابی بود: production configuration fail-closed، مرز reverse proxy مشخص، probe و security header، migration artifact، تست regression، build کانتینر در CI و runbookهای عملیاتی اضافه شدند.

پیاده‌سازی V1 تمام شده است، اما پروژه عمداً **production-ready اعلام نمی‌شود**. provider واقعی، تصمیم‌های حقوقی/مالی، اجرای production-like E2E، restore drill و SLO/RPO/RTO هنوز شواهد خارجی لازم دارند و در `production-readiness-checklist.md` به‌صورت gate باز مانده‌اند.

## hardening اجراشده

- Production بدون IP دقیق reverse proxy بالا نمی‌آید؛ forwarded header فقط از همان proxy و با یک hop پذیرفته می‌شود.
- issuer/callback عمومی باید HTTPS باشند و placeholder/secret کوتاه رد می‌شود.
- Fake payment همچنان در Production fail-fast است.
- پاسخ‌ها `nosniff`، deny-frame/CSP frame ancestor، Referrer/Permissions policy و `X-Trace-Id` دارند.
- endpointهای خصوصی API و کل Backoffice/Identity با `Cache-Control: no-store` پاسخ می‌دهند.
- API، Identity و Backoffice liveness/readiness و Public Web health endpoint دارند.
- runtime containerها non-root هستند؛ Compose برای appها `no-new-privileges` و حذف capability دارد.
- Next.js نیز security header و production BFF configuration validation دارد.

## delivery و CI

CI اکنون علاوه بر build/test:

- drift میان EF model و آخرین migration را رد می‌کند؛
- idempotent SQL migration bundle می‌سازد و artifact نگه می‌دارد؛
- migrationها را روی PostgreSQL واقعی Testcontainers اجرا می‌کند؛
- dependencyهای NuGet و npm production را بررسی می‌کند؛
- OpenAPI/TypeScript drift را رد می‌کند؛
- هر چهار image قابل‌استقرار را build می‌کند.

`scripts/verify-release.ps1` همین مسیر را برای release host تکرار می‌کند و `scripts/generate-migration-bundle.ps1` SQL همان commit را می‌سازد.

## runbookها

- deployment، migration و forward-fix
- backup و restore drill
- incident response و key/secret compromise
- payment reconciliation بدون تحریف receipt
- performance/capacity baseline بدون اختراع SLO
- production readiness checklist با تفکیک gate فنی و approval خارجی

## نتیجه اعتبارسنجی محلی

- build کل solution: موفق با صفر warning/error
- ۹۹ تست غیرکانتینری .NET: Domain=41، Application=17، Architecture=8، Integration=27 و Backoffice=6
- ۴ تست Vitest، typecheck و lint: موفق
- EF pending-model check: بدون drift
- production build Public Web: موفق
- NuGet transitive vulnerability report و npm audit کامل/production: صفر vulnerability گزارش‌شده در ۲۰۲۶-۱۰-۲۷
- startup آزمایشی Production با تنظیمات قوی ولی `Payments:Provider=Fake`: مطابق انتظار fail-fast

Docker روی این ماشین موجود نیست؛ بنابراین Testcontainers، Compose build، production-like E2E، load baseline و restore drill فقط در CI/محیط عملیاتی قابل اثبات‌اند و به‌عنوان pass محلی گزارش نشده‌اند.

## وضعیت V1

هیچ فاز پیاده‌سازی دیگری پس از Phase 9 وجود ندارد. کار بعدی feature phase نیست؛ بستن gateهای checklist توسط Product/Legal/Finance/Security/Operations و اجرای شواهد محیطی است.
