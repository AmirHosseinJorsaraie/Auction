# PublicWeb structure

The customer-facing frontend is split into two .NET projects so authentication tokens never enter browser storage:

- `ArtMarketplace.PublicWeb` is the ASP.NET Core host and BFF. It owns OIDC, the opaque PostgreSQL-backed session cookie, antiforgery validation, token refresh, and fixed-destination API proxies.
- `Client/ArtMarketplace.PublicWeb.Client` is the Blazor WebAssembly UI. It contains pages, reusable components, transport models, and the single `MarketplaceApiClient` gateway.

## Folder responsibilities

- `Components/`: the host document and server error page.
- `Client/Pages/`: one focused component per route/workflow.
- `Client/Components/`: reusable presentation and loading-state components.
- `Client/Models/`: JSON transport contracts kept out of page markup.
- `Client/Services/`: browser-side API and antiforgery handling.
- `wwwroot/`: shared visual assets and responsive styling.

Comments are reserved for security boundaries and logic whose intent is not obvious from its name. Straightforward component markup is kept readable through small components and semantic CSS classes instead of line-by-line comments.
