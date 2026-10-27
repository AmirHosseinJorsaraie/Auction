# Performance and Capacity Baseline

The volume and latency budgets require Product/Operations approval. Record them before running this baseline; do not convert arbitrary local-machine numbers into an SLO.

## Required dataset and scenarios

- Agreed counts for users, artists, artwork revisions/assets, published listings, orders, payment events and audit events.
- Public gallery search/filter/sort with cold and warm cache.
- Backoffice dashboard and each paginated monitor at first, middle and last page.
- Authenticated cart, checkout and payment initiation at agreed concurrency.
- Reservation expiry and verification expiry workers while foreground traffic runs.
- Callback duplicate and reconciliation paths.

## Evidence

Capture p50/p95/p99 latency, throughput, error/429 rate, PostgreSQL CPU/IO/connections/locks, query count, slow-query plans, application CPU/memory and worker backlog. Use `EXPLAIN (ANALYZE, BUFFERS)` only on a production-like clone, never casually on production.

Review that filters and stable orderings use indexes, page size stays capped at 100, queries do not return PII unnecessarily and no N+1 pattern appears. Store tool version, workload file, dataset seed, commit SHA and raw results with the release record.

Release is blocked if the agreed budget fails, deadlocks appear, memory grows without bound, readiness fails under load, or a correctness invariant changes under concurrency.
