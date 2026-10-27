# Incident Response Runbook

## First actions

1. Assign incident commander, operations lead and communications owner.
2. Record UTC start time, affected surface, release commit and first known `X-Trace-Id`.
3. Preserve logs and database/provider evidence. Do not delete payment events, receipts, audit records or invalidations.
4. Contain with the narrowest reversible control: remove traffic, disable a user, suspend a listing, stop payment initiation or turn off ownership transfer.
5. Do not expose tokens, secrets, identity documents, addresses or raw callback payloads in chat/tickets.

## Severity guide

- SEV-1: unauthorized access, double-sell, incorrect ownership transfer, missing/duplicated financial application, secret/key compromise or destructive data loss.
- SEV-2: checkout/payment unavailable, reconciliation backlog growing, verification bypass attempt, restore/readiness failure or widespread login failure.
- SEV-3: degraded latency, isolated UI failure or delayed non-critical notification.

## Scenario controls

### Suspected account or role compromise

Disable the account, revoke sessions, rotate affected client/user credentials, inspect role/audit history and preserve authorization-version evidence. Confirm the last SuperAdmin safeguard has not been bypassed.

### Payment mismatch or duplicate success

Stop automated fulfillment/ownership transfer for the affected orders. Keep all provider events and receipts append-only, open or update reconciliation cases and follow `payment-reconciliation.md`. Never force Order status to hide a provider discrepancy.

### Review/eligibility integrity failure

Suspend published listings and place holds on reserved/sold orders. Do not republish automatically after reapproval; resolve durable invalidations and explicitly reinstate on the current eligibility version.

### Secret or signing-key compromise

Remove traffic if tokens or callbacks can be forged. Rotate the compromised key through the secret manager, revoke affected sessions/tokens, deploy, and verify old material is rejected. OIDC signing-key rotation requires an overlap/validation plan approved by Security.

## Closure

Reconcile all financial and ownership records, complete a timeline and root-cause analysis, identify detection/response gaps, assign corrective actions and validate them with a regression test or drill.
