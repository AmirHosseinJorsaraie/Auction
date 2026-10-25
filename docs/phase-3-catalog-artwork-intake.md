# Phase 3 — Catalog & Artwork Intake

## Delivered

- `Artwork`, immutable/versioned `ArtworkRevision`, `Artist`, `Category`, and clean-only `ArtworkAsset` domain models.
- Seller-owned Persian artwork draft form and `My Artworks` pages.
- Canonical seller/account lock guard before create, edit, submit, and listing-draft operations.
- Partial unique indexes for one mutable draft per artwork, one open review case, one pending review round, and one draft listing.
- Atomic Draft → Frozen submission with review case/round creation.
- Draft-only `MarketplaceListing` with positive-price database constraint and no seller-controlled published revision.
- One-time upload intents with extension/MIME/size/count validation and dedicated rate limits.
- S3-compatible presigned PUT into a private quarantine bucket.
- Exact-byte read, magic-byte validation, malware-scanner boundary, SHA-256, server-only create-if-absent promotion, and quarantine cleanup.
- JPEG/PNG decode and re-encode through SkiaSharp to remove embedded metadata; SVG, HTML, and archives are rejected.
- Separate clean-private and public-derivative buckets. Domain assets cannot point at quarantine, and a public image requires a derivative key.
- Owner/staff-authorized short-lived private download links and non-enumerating 404 behavior for IDOR resistance.
- Phase 3 EF migration and idempotent SQL artifact.

## Security behavior

Production uses `FailClosedFileSafetyScanner` until a real malware provider is configured. Consequently, finalize is intentionally denied outside Development/Testing rather than silently treating files as clean. Development uses a deterministic scanner that rejects the EICAR test marker; it is not a production antivirus substitute.

The server scans the bytes read in one bounded operation and promotes that same in-memory stream. It never accepts ETag as a content hash and never re-fetches a mutable quarantine key after the verdict. Clean and derivative writes use `If-None-Match: *`.

## Endpoints

- `GET /api/categories`
- `POST /api/backoffice/categories` (`SuperAdmin` + MFA)
- `GET /api/artworks/mine`
- `GET /api/artworks/{artworkId}`
- `POST /api/artworks`
- `PUT /api/artworks/{artworkId}/draft`
- `POST /api/artworks/{artworkId}/submit`
- `POST /api/artworks/{artworkId}/listing-draft`
- `POST /api/artworks/{artworkId}/uploads/intents`
- `POST /api/artworks/{artworkId}/uploads/{intentId}/finalize`
- `GET /api/artworks/{artworkId}/assets/{assetId}/download`
- `GET /api/media/{assetId}`

## Validation evidence

- .NET Release build: zero warnings and zero errors.
- Non-container .NET tests: 28 passed.
- PublicWeb typecheck, lint, production build, and Vitest: passed with repository-local Node 24.
- EF idempotent script generated at `artifacts/phase3-idempotent.sql`; it preserves the original Phase 2 migration identity and adds no Auction table or column.
- PostgreSQL migration smoke remains pending because Docker is unavailable on this host.

## Remaining production work

- Integrate and observe a production malware-scanning provider.
- Provision three buckets/policies and deny public ACL/user PUT access to clean namespaces.
- Add a periodic idempotent cleanup worker for expired intents and orphan quarantine objects.
- Run the Testcontainers migration/concurrency suite where Docker is available.
- Add browser-level E2E coverage against HTTPS hostnames and real SeaweedFS.

