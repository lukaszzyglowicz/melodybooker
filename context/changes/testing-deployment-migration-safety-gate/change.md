---
change_id: testing-deployment-migration-safety-gate
title: Deployment & migration safety gate (rollout Phase 4)
status: implemented
created: 2026-11-04
updated: 2026-11-04
archived_at: null
---

## Notes

Open a change folder for rollout Phase 4 of context/foundation/test-plan.md: "Deployment & migration safety gate".
Risks covered: #3 (an EF Core migration behaves differently against the real SQL Server target than it does under the InMemory/SQLite providers the test suite uses).
Risk response intent:
- Risk #3: prove a migration applied against the real target provider (SQL Server) produces the same schema/constraints the test suite assumes under InMemory/SQLite; challenge "green tests on InMemory/SQLite" does not mean "the migration is production-safe"; likely cheapest layer is a process/CI-gate (migration dry-run against a disposable SQL Server/Azure SQL instance), not a classic unit test; avoid relying solely on InMemory-provider test runs as proof a migration is production-safe.
After creating the folder, follow the downstream continuation rule.
