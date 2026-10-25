# Phase 2 — Identity, access, profiles, and seller verification

## Implemented baseline

- Self-hosted ASP.NET Core Identity and OpenIddict authorization server.
- Authorization Code flow with PKCE; password and implicit grants are not enabled.
- Reference access and refresh tokens with 10-minute and 14-day lifetimes.
- Certificate-backed signing and encryption. Startup fails when either certificate is absent or unreadable.
- Confirmed email, strong passwords, five-attempt lockout, recovery tokens, and built-in authenticator MFA UI.
- Idempotent roles: `User`, `Expert`, `Admin`, and `SuperAdmin`.
- No default administrative password. Bootstrap can only promote an existing, confirmed, MFA-enrolled account.
- Staff authorization requires both a staff role and `amr=mfa`.
- API validates reference tokens by authenticated introspection, issuer, audience, scope, role, and MFA policy.
- Public Next.js and Blazor backoffice use distinct OIDC confidential clients and distinct host-only cookies.
- Tokens are held only in encrypted server-side PostgreSQL sessions. Database keys are HMAC digests of 256-bit random browser handles.
- Session namespaces, HMAC secrets, encryption purposes, cookie names, and data-protection application names are isolated by surface.
- CSRF checks protect state-changing BFF routes and logout.
- Profile and saved-address APIs are restricted to the authenticated subject.
- Seller verification supports draft, pending, changes requested, approved, rejected, revoked, and expired states with optimistic versions.
- Seller eligibility is separate from roles and uses canonical, monotonic versions.
- Revocation emits a durable outbox record plus an append-only invalidation and audited target resolutions.
- Security decisions are append-only audit events with actor, subject, trace identifier, timestamp, and structured data.

## Required secrets and endpoints

Copy `.env.example` into the deployment secret system and replace every placeholder. Do not commit the resulting file. Public-web variables are shown with a `PublicWeb__` prefix so Compose or the deployment platform can map them into that container without the prefix. Backoffice variables follow the same convention.

The identity service deliberately has no development-certificate fallback. Generate local-only certificates with:

```powershell
.\scripts\New-DevelopmentOidcCertificates.ps1 `
  -SigningPassword '<random-signing-password>' `
  -EncryptionPassword '<different-random-encryption-password>'
```

Production keys should come from the deployment secret/key system. Rotate by deploying the new signing/encryption credential while retaining the previous public signing key and decryption key for at least the maximum token lifetime; make the new credential active; then remove the prior credential only after the overlap window. A missing key is a startup failure, never a request-time temporary-key fallback.

## Database

The Phase 2 migrations create Identity, OpenIddict, profiles, addresses, verification, seller eligibility, invalidation, BFF session, security audit, and outbox tables. An idempotent deployment script is generated at `artifacts/phase2-idempotent.sql`.

Apply migrations from a controlled deployment job, not concurrently from every application replica:

```powershell
$env:DOTNET_ROOT = (Resolve-Path .tools/dotnet10).Path
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
.\.tools\dotnet-ef\dotnet-ef.exe database update `
  --project src/Backend/ArtMarketplace.Infrastructure/ArtMarketplace.Infrastructure.csproj `
  --startup-project src/Backend/ArtMarketplace.Identity/ArtMarketplace.Identity.csproj `
  --context MarketplaceDbContext
```

## Verification performed

- Full .NET solution build with warnings treated as errors.
- Domain, application, architecture, backoffice, and API authorization-boundary tests.
- Public web production build, Vitest suite, TypeScript type-check, and ESLint.
- Public web production dependency audit: zero known vulnerabilities. The full audit reports five high-severity findings only in the ESLint toolchain (`braces` through `eslint-config-next`), with no upstream fix currently available; it is excluded from the production image/runtime.
- EF Core idempotent migration script generation.
- The real PostgreSQL migration smoke test remains environment-dependent because this workstation does not provide Docker; CI already owns that container-backed check.

## Operations follow-up

- Connect a production email delivery provider before enabling public registration.
- Run the PostgreSQL/Testcontainers job in CI and retain the resulting migration evidence.
- Configure production HTTPS hostnames, trusted proxy headers, certificate mounts, and secret mappings.
- Add outbox dispatch workers and alert on repeated delivery failure before downstream Phase 3 consumers rely on invalidation events.
- Schedule expired-session cleanup; revocation is immediate, while physical deletion is an operational retention task.
