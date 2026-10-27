# Deployment and Migration Runbook

## Release invariants

- Deploy immutable images built from one Git commit; never rebuild a promoted tag.
- TLS terminates only at the approved reverse proxy. Set the exact proxy IP through `ReverseProxy__KnownProxies__0`; the application rejects an empty trust list in Production.
- Production secrets come from the platform secret store. Placeholder values, HTTP identity URLs and the Fake payment provider fail startup.
- Database migration is a separate release step. Application containers do not migrate automatically.
- Take and verify a restorable backup before every schema release.

## Pre-deployment

1. Run `scripts/verify-release.ps1` on a host with .NET 10.0.401, Node 24.19.0 and Docker.
2. Download the CI `database-migration-bundle` artifact or run `scripts/generate-migration-bundle.ps1` from the exact release commit.
3. Confirm image digests, configuration allow-list, certificate expiry, secret rotation dates and trusted proxy address.
4. Confirm `/health/live` and `/health/ready` probe paths in the platform configuration. Public Web uses `/api/health`.
5. Freeze destructive admin operations for the migration window and record the release owner, database backup identifier and rollback decision deadline.

## Deployment order

1. Create a PostgreSQL backup and complete the verification checks in `backup-restore.md`.
2. Inspect the generated SQL. Apply it once with `psql -v ON_ERROR_STOP=1 -f migrations.sql` using the migration identity, not the runtime identity.
3. Deploy Identity, API, Backoffice and Public Web images from the same commit.
4. Keep instances out of traffic until readiness succeeds. Liveness proves only that the process is alive; readiness verifies required dependencies.
5. Smoke-test OIDC login/logout, public gallery, private document denial, Backoffice permission matrix, checkout and a verified payment callback in a non-production payment environment.
6. Verify security headers, HTTPS redirects, `X-Trace-Id`, no-store on private endpoints and logs free of tokens, secrets, signatures, signed URLs and PII payloads.
7. Gradually enable traffic and watch error rate, latency, connection saturation, reservation expiry backlog, reconciliation backlog and outbox age.

## Rollback and forward-fix

Application rollback is allowed only when the previous binary is compatible with the migrated schema. EF `Down` methods are not an automatic production rollback plan because data written after migration can be lost or reinterpreted.

Preferred response:

1. Stop traffic to the faulty version.
2. If the schema is backward-compatible, redeploy the previous image.
3. Otherwise deploy a reviewed forward-fix migration and compatible application image.
4. Restore the database only for confirmed destructive corruption and only after incident command approves the data-loss boundary.
5. Record the release commit, migration list, timeline, trace IDs and reconciliation actions.

## Post-deployment evidence

Store image digests, migration output, health results, smoke-test results, dependency scan, backup identifier and approving people with the release record. A green process alone is not release evidence.
