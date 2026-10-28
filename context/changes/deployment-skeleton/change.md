---
change_id: deployment-skeleton
title: Wire GitHub Actions CI/CD (OIDC) to the existing Azure App Service dev environment
status: implementing
created: 2026-10-26
updated: 2026-10-26
archived_at: null
---

## Notes

F-02 z roadmap.md. Infrastruktura Bicep (`infra/`) już istnieje i jest wdrożona
w Azure (`melodybooker-rg`, środowisko dev), ale brak CI/CD — kod aplikacji na
żywym App Service jest nieaktualny. Ten change wiąże oba światy: OIDC auth +
GitHub Actions workflow, który na merge do `main` idempotentnie redeployuje
infrę i wdraża aktualny kod na dev. Prod świadomie odłożony do osobnej zmiany.

Zastępuje nieformalny `context/changes/azure-deployment-plan/plan.md` (inny
change-id, napisany przed stworzeniem `infra/` — część jego założeń jest już
nieaktualna, np. "EF Core trzeba dopiero dodać").
