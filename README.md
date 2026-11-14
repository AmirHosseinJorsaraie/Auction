# Art Marketplace

The V1 foundation for an art marketplace built as a modular monolith using Clean Architecture.

## Prerequisites

- .NET SDK `10.0.401`
- Visual Studio with the ASP.NET and web development workload (for Windows debugging)
- Docker with Compose v2 only for the containerized setup and Docker-backed integration test

The repository includes portable .NET 10, PostgreSQL, and SeaweedFS runtimes under `.tools`, so Docker is not required for normal Visual Studio debugging on this configured Windows machine.

## Debug in Visual Studio on Windows

Open `ArtMarketplace.sln`, then run the local dependencies once from a PowerShell terminal at the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-LocalDependencies.ps1
```

Select the shared `Local Debug` launch profile and press **F5**. The profile is stored in `ArtMarketplace.slnLaunch` and starts these four .NET projects:

- `ArtMarketplace.Identity`
- `ArtMarketplace.Api`
- `ArtMarketplace.Backoffice`
- `ArtMarketplace.PublicWeb`

All class libraries and test projects must remain set to **None**. The public UI is a Blazor WebAssembly client hosted by an ASP.NET Core BFF.

Local Debug URLs:

- Public Web: `https://localhost:3000`
- Identity/OIDC: `https://localhost:7134`
- API/OpenAPI: `https://localhost:7047/swagger`
- API readiness: `https://localhost:7047/health/ready`
- Backoffice: `https://localhost:7177`

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
```

To run only the public site, use `powershell -ExecutionPolicy Bypass -File .\scripts\Start-PublicWeb.ps1`.

The values in `.env.example` are intended for local development only. The `.env` file, Data Protection keys, dependencies, and build outputs are excluded from Git.

Implementation details are available in the [Phase 1 report](docs/phase-1-foundation.md), [Phase 2 report](docs/phase-2-identity-access.md), [Phase 3 report](docs/phase-3-catalog-artwork-intake.md), [Phase 4 report](docs/phase-4-expert-review-moderation.md), [Phase 5 report](docs/phase-5-public-gallery-listings.md), [Phase 6 report](docs/phase-6-cart-checkout-orders.md), [Phase 7 report](docs/phase-7-payment-abstraction.md), [Phase 8 report](docs/phase-8-administration-ui-polish.md), and [Phase 9 report](docs/phase-9-hardening-delivery.md). Review the [production readiness checklist](docs/production-readiness-checklist.md) before any real deployment. The repository also includes an [illustrated Persian backend learning guide](docs/backend-learning-guide-fa.md) and an [English backend learning guide](docs/backend-learning-guide.md).

The authentication flow requires `identity.localhost`, `marketplace.localhost`, and `backoffice.localhost` to be configured behind local or environment-specific HTTPS termination and a reverse proxy. Secure cookies intentionally do not downgrade to HTTP.
