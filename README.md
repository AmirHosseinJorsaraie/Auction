# Art Marketplace

The V1 foundation for an art marketplace built as a modular monolith using Clean Architecture.

## Prerequisites

- .NET SDK `10.0.401`
- Node.js `24.19.0` and npm `11.17+`
- Docker with Compose v2 for PostgreSQL, SeaweedFS, and integration tests

## Run locally

```powershell
Copy-Item .env.example .env
docker compose -f deploy/docker-compose.yml up --build
```

Services:

- Public Web: `http://localhost:3000`
- API/OpenAPI: `http://localhost:8080/openapi/v1.json`
- API liveness: `http://localhost:8080/health/live`
- API readiness: `http://localhost:8080/health/ready`
- Backoffice: `http://localhost:8081`
- Identity/OIDC (behind the required local HTTPS termination): `http://localhost:8082`

## Run the projects independently

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

The values in `.env.example` are intended for local development only. The `.env` file, Data Protection keys, dependencies, and build outputs are excluded from Git.

Implementation details are available in the [Phase 1 report](docs/phase-1-foundation.md), [Phase 2 report](docs/phase-2-identity-access.md), [Phase 3 report](docs/phase-3-catalog-artwork-intake.md), [Phase 4 report](docs/phase-4-expert-review-moderation.md), [Phase 5 report](docs/phase-5-public-gallery-listings.md), [Phase 6 report](docs/phase-6-cart-checkout-orders.md), [Phase 7 report](docs/phase-7-payment-abstraction.md), and [Phase 8 report](docs/phase-8-administration-ui-polish.md). The repository also includes an [illustrated Persian backend learning guide](docs/backend-learning-guide-fa.md) and an [English backend learning guide](docs/backend-learning-guide.md).

The authentication flow requires `identity.localhost`, `marketplace.localhost`, and `backoffice.localhost` to be configured behind local or environment-specific HTTPS termination and a reverse proxy. Secure cookies intentionally do not downgrade to HTTP.
