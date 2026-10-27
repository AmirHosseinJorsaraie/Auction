# Payment Reconciliation Runbook

## Principle

`VerifiedPaymentReceipt` means the provider reports money movement. It does not prove the Order was eligible for automatic application. Never delete or rewrite a receipt to make Order state appear consistent.

## Daily review

1. Open the Backoffice reconciliation view with a dedicated MFA staff account.
2. Compare provider key, transaction ID, merchant reference/account, amount, currency and provider timestamp against the provider portal/API.
3. Verify Order deadline/status, existing payment application, seller eligibility version, approval integrity and commercial holds.
4. Classify the case: late success, amount/currency/account mismatch, extra receipt, duplicate event, eligibility/review hold or unknown.
5. Record the external evidence identifier and approved action without copying secret/signature/raw payload data.

## Allowed outcomes

- Confirm the existing application was correct and close the case with evidence.
- Refund through the approved provider/legal workflow, then record the provider refund reference when that V1 policy is implemented.
- Escalate for fulfillment/legal review while preserving the Order/Listings holds.
- Mark provider data disputed and escalate to Finance/Payments.

Manual database updates, receipt deletion, forced `Sold/Completed`, automatic late-payment application and reuse of an idempotency key for a different intent are prohibited.

## Monitoring

Alert on oldest-open-case age, new-case rate, unmatched provider settlements, more than one receipt per Order, callbacks rejected for signature/freshness and any ownership transfer without exactly one payment application.
