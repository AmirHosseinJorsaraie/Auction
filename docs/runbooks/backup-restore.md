# PostgreSQL Backup and Restore Runbook

RPO and RTO are product/operations decisions and must be recorded before go-live. This runbook does not invent them.

## Backup

Use a dedicated backup identity and a protected environment variable/secret file; never put a database password in command history.

```powershell
pg_dump --dbname "$env:DATABASE_URL" --format=custom --no-owner --no-acl --file artifacts/art-marketplace.backup
pg_restore --list artifacts/art-marketplace.backup | Select-Object -First 20
Get-FileHash artifacts/art-marketplace.backup -Algorithm SHA256
```

Encrypt the artifact with the approved KMS, upload it to immutable/retained storage and record database version, UTC time, size and SHA-256. A backup is incomplete until restore is proven.

## Restore drill

1. Provision an isolated PostgreSQL instance of the production major version with no network route to production services.
2. Create an empty database and restore:

```powershell
createdb --maintenance-db "$env:RESTORE_ADMIN_URL" art_marketplace_restore
pg_restore --dbname "$env:RESTORE_DATABASE_URL" --clean --if-exists --no-owner --no-acl artifacts/art-marketplace.backup
```

3. Start the exact release images against the restored database with outbound email/payment/storage disabled.
4. Require readiness success and compare counts for users, artwork, orders, payment receipts, applications, ownership transfers, audit events and migrations.
5. Sample referential integrity and verify the latest paid orders against provider-side records without changing either system.
6. Record elapsed restore time and data timestamp to measure actual RTO/RPO; obtain Operations and Product sign-off.
7. Destroy the isolated restore environment and its decrypted data according to retention policy.

## Failure handling

Never repair a failed restore in place and call it a successful drill. Preserve logs, create another empty database, fix the documented procedure and repeat from the immutable backup.
