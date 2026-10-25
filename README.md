# Art Marketplace

پایه فنی V1 یک بازار هنر با معماری Modular Monolith و Clean Architecture است.

## پیش‌نیازها

- .NET SDK `10.0.401`
- Node.js `24.19.0` و npm `11.17+`
- Docker با Compose v2 برای PostgreSQL، SeaweedFS و integration testها

## اجرای محلی

```powershell
Copy-Item .env.example .env
docker compose -f deploy/docker-compose.yml up --build
```

سرویس‌ها:

- Public Web: `http://localhost:3000`
- API/OpenAPI: `http://localhost:8080/openapi/v1.json`
- API liveness: `http://localhost:8080/health/live`
- API readiness: `http://localhost:8080/health/ready`
- Backoffice: `http://localhost:8081`
- Identity/OIDC (behind the required local HTTPS termination): `http://localhost:8082`

## اجرای مستقل

```powershell
dotnet restore ArtMarketplace.sln --locked-mode
dotnet build ArtMarketplace.sln -c Release --no-restore
dotnet test ArtMarketplace.sln -c Release --no-build

Set-Location src/Frontend/PublicWeb
npm ci
npm run typecheck
npm run lint
npm test
npm run build
```

مقادیر `.env.example` فقط برای توسعه محلی‌اند. فایل `.env`، keyهای Data Protection و خروجی build در Git ثبت نمی‌شوند.

جزئیات تحویل در [گزارش Phase 1](docs/phase-1-foundation.md)، [گزارش Phase 2](docs/phase-2-identity-access.md)، [گزارش Phase 3](docs/phase-3-catalog-artwork-intake.md) و [گزارش Phase 4](docs/phase-4-expert-review-moderation.md) آمده است. [راهنمای فارسی و تصویری یادگیری بک‌اند](docs/backend-learning-guide-fa.md) و [نسخه انگلیسی](docs/backend-learning-guide.md) نیز موجودند. برای flow ورود، دامنه‌های `identity.localhost`، `marketplace.localhost` و `backoffice.localhost` باید با HTTPS و reverse proxy محلی/محیطی تنظیم شوند؛ cookieهای امن عمداً روی HTTP downgrade نمی‌شوند.
