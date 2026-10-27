# Deployment Assets

`docker-compose.yml` یک stack توسعه/تست محلی است و به‌دلیل Fake payment و defaultهای local یک manifest production نیست. Production باید از orchestrator، reverse proxy/TLS، secret manager، immutable image digest، backup و observability مصوب استفاده کند.

Before any deployment, follow:

1. `docs/production-readiness-checklist.md`
2. `docs/runbooks/deployment.md`
3. `docs/runbooks/backup-restore.md`
4. `docs/runbooks/incident-response.md`

The three ASP.NET services require the exact reverse-proxy IP in `ReverseProxy__KnownProxies__0` when `ASPNETCORE_ENVIRONMENT=Production`. Do not configure an open network range merely to make forwarded headers work.
