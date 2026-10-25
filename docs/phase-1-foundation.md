# گزارش Phase 1 — Solution Foundation

آخرین به‌روزرسانی: ۲۰۲۶-۱۰-۱۷

## تصمیم‌های تثبیت‌شده

- ریشه نهایی: `D:\Projects\Auction`
- solution: `ArtMarketplace.sln`
- Backend/Backoffice: .NET SDK `10.0.401`، target framework `net10.0`
- Public Web: Node `24.19.0` LTS، Next.js `16.4.2`، React `19.3.1`، TypeScript `5.9.3`
- PostgreSQL: image `17.6-alpine` با digest ثابت
- object storage محلی: SeaweedFS `4.41` با digest ثابت و ADR-0007
- migration در startup خودکار اجرا نمی‌شود؛ deployment/CI آن را کنترل می‌کند.

## خروجی‌ها

- لایه‌های Domain، Application، Infrastructure، Contracts و API
- Public Web با App Router، TypeScript، Tailwind و پایه shadcn/ui
- Backoffice با Blazor Interactive Server و RTL
- Problem Details، OpenAPI، JSON logging، rate limiting و health probeهای تفکیک‌شده
- PostgreSQL migration smoke test با Testcontainers
- architecture test برای dependency ruleهای Clean Architecture
- contract/type generation و drift check برای OpenAPI در CI
- Dockerfile مستقل برای هر deployable و Compose محلی
- package lock برای NuGet و npm

## رفتار health

- `/health/live` فقط سلامت process را می‌سنجد و به dependency خارجی وصل نمی‌شود.
- `/health/ready` اتصال واقعی PostgreSQL و دسترس‌پذیری endpoint ذخیره‌ساز شیء را می‌سنجد.
- در نبود dependencyها، process زنده می‌ماند، liveness برابر ۲۰۰ و readiness برابر ۵۰۳ است.

## شواهد اجرای محلی

- `dotnet build ArtMarketplace.sln -c Release --no-restore`: موفق، صفر warning/error
- Unit/Application/Architecture/Backoffice tests: ۱۴ تست موفق
- Public Web: typecheck، ESLint، Vitest و production build موفق؛ یک تست frontend موفق
- API: status و OpenAPI برابر ۲۰۰؛ liveness برابر ۲۰۰؛ readiness بدون dependency برابر ۵۰۳
- Public Web: پاسخ ۲۰۰، `dir=rtl` و عنوان فارسی تأیید شد.
- Backoffice: home/login برابر ۲۰۰، RTL و placeholder ورود فارسی تأیید شد.
- production dependency audit در npm: صفر آسیب‌پذیری شناخته‌شده

## محدودیت اعتبارسنجی این ماشین

Docker/Podman نصب نیست. بنابراین `docker compose config/up` و Testcontainers محلی اجرا نشد؛ integration test عمداً با `DockerUnavailableException` fail شد. workflow CI این تست را روی runner دارای Docker اجرا می‌کند.

پنج finding با شدت high در `npm audit` کامل، همگی زنجیره transitive ابزار توسعه `eslint-config-next → fast-glob → micromatch → braces` هستند. production audit پاک است و npm فعلاً نسخه اصلاح‌شده‌ای برای `braces` ارائه نمی‌کند؛ این مورد در `known-issues.md` پیگیری می‌شود.

## نکته امنیتی Data Protection

Backoffice key ring را در مسیر قابل تنظیم `DataProtection:KeysPath` نگه می‌دارد. Compose volume پایداری keyها را فراهم می‌کند. keyهای محیط محلی encryption-at-rest ندارند؛ قبل از استقرار مشترک/تولید، محافظت key ring با KMS/certificate و rotation policy الزامی است.
