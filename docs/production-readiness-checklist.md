# V1 Production Readiness Checklist

## Implemented technical gates

- [x] Locked builds, non-root runtime images and pinned service images
- [x] Central OIDC/BFF sessions, secure host-only cookies, MFA staff policies and permission matrix
- [x] Production rejects Fake payment, placeholder secrets, HTTP identity/public URLs and missing trusted proxy configuration
- [x] Trusted forwarded headers, HSTS/HTTPS redirect, security headers, trace IDs and no-store private responses
- [x] Liveness/readiness endpoints and container health checks
- [x] Idempotent migration artifact, EF drift gate and real PostgreSQL migration test in CI
- [x] Unit/architecture/integration/frontend regression suites and OpenAPI drift gate
- [x] Deployment, forward-fix, backup/restore, incident, reconciliation and performance runbooks

## Mandatory external evidence before go-live

- [ ] Country, currency, merchant-of-record, tax, refund/dispute and cancellation rules approved by Legal/Finance/Product
- [ ] Real payment provider adapter, sandbox certification, webhook source policy and production credentials approved
- [ ] Ownership-transfer legal trigger approved before enabling its feature switch
- [ ] Seller verification fields, document retention/deletion and malware-scanner provider approved
- [ ] Hosting topology, exact trusted proxy addresses, TLS/certificate rotation and secret manager configured
- [ ] Data Protection/OIDC keys encrypted at rest with access/rotation policy
- [ ] Audit retention/tamper-resistance policy approved
- [ ] SLO, traffic profile, p95/p99 budget, RPO and RTO approved and measured
- [ ] Full production-like seller → review → publish → checkout → verified payment E2E passed
- [ ] Concurrency/Testcontainers suite passed in CI with evidence attached
- [ ] Empty/previous-version migration, backup and restore drill completed
- [ ] Keyboard-only, responsive RTL and supported-browser review completed
- [ ] No unresolved critical/high security or data-integrity finding; dependency and image scans reviewed
- [ ] Incident simulation and payment reconciliation drill signed off

V1 implementation can be complete while deployment readiness remains **blocked** by unchecked external evidence. “Production-ready” may be declared only when every mandatory item is checked by its accountable owner.
