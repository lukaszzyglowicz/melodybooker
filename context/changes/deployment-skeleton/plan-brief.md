# Deployment Skeleton (F-02) — Plan Brief

> Full plan: `context/changes/deployment-skeleton/plan.md`

## What & Why

Wire `push`-to-`main` so it automatically deploys MelodyBooker to the Azure
App Service **dev** environment that already exists. Right now the Bicep
infrastructure (`infra/`) is fully written and actually running in Azure, but
there's no CI/CD — the live app is stuck on stale code and every deploy would
have to be done by hand. This closes roadmap item **F-02**.

## Starting Point

`infra/main.bicep` + modules already define and have **already provisioned**
App Service, Azure SQL (AAD-only auth), Key Vault, Application Insights, and
Log Analytics in `melodybooker-rg`. The app itself already has EF Core,
startup auto-migration, and a `/health` endpoint. What's missing is purely the
CI/CD layer: no GitHub Actions workflow, no deploy credential wired up, zero
GitHub secrets, and (likely) no SQL-level permission grant for the Web App's
managed identity.

## Desired End State

A merge to `main` triggers a green GitHub Actions run that deploys the current
app code to the existing Web App via a publish-profile credential, ending in a
smoke test confirming `/health` returns `Healthy`. Infra (Bicep) changes are
applied manually/locally — no Azure AD identity could be provisioned for CI in
this tenant (see Key Decisions).

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Reconciling the already-live dev environment | Idempotent Bicep redeploy onto existing RG | The resources already exist and work; destroying them adds risk/cost with no benefit | Plan (user Q&A) |
| OIDC provisioning | ~~Agent writes `az` commands; user runs them~~ → **Superseded during implementation**: Azure AD tenant blocks app registration entirely; switched to Publish Profile (no Azure AD needed) | Tenant policy has no admin path available to this project | Phase 1 implementation (2026-10-26) |
| EF Core migration strategy | Keep `MigrateAsync()` at Web App startup, unchanged | Already implemented and working; no need to add pipeline complexity | Plan (user Q&A) |
| Dev/Prod architecture | One subscription, two resource groups | Simpler to manage than two subscriptions; prod isn't being created yet anyway | Plan (user Q&A) |
| Pipeline trigger | Auto-deploy to dev on merge; prod stays manual/future | Matches "prod not in scope yet" without blocking dev automation | Plan (user Q&A) |
| Prod scope | Deferred entirely to a future change | Avoids scope creep; dev CI/CD alone is already the real gap | Plan (user Q&A) |
| RBAC for pipeline identity | Contributor + User Access Administrator | Bicep's Key Vault role-assignment resources need `roleAssignments/write`, which plain Contributor lacks | Plan (research) |

## Scope

**In scope:**
- Publish Profile credential retrieval (Web App Kudu/SCM, no Azure AD) stored as a GitHub secret
- One-time SQL grant so the Web App's managed identity can actually read/write `melodybookerdb`
- `.github/workflows/deploy.yml`: build/test gate, main-branch app code deploy + smoke test
- A short `infra/README.md` runbook, including the manual infra-deploy step

**Out of scope:**
- Creating a `prod` environment or its parameters file
- Destroying/recreating the existing dev resources
- Changing the EF Core migration strategy or moving to Standard-tier slots
- Containerizing the app
- Redeploying infrastructure (Bicep) from CI — no Azure AD identity available in this tenant; stays manual/local

## Architecture / Approach

One GitHub Actions workflow, two paths by event: PRs get only the build/test
gate (no Azure interaction); pushes to `main` get app-code publish + zip-deploy
+ smoke test. Auth is a Publish Profile secret scoped to just this Web App —
no Azure AD, no `azure/login`, no OIDC (switched from the original OIDC design
after discovering the tenant blocks app registration entirely).

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Publish-profile credential | GitHub secret with the Web App's deploy credential, one-time SQL permission for the Web App identity | Missing SQL grant would crash-loop the app once current code deploys |
| 2. CI/CD workflow | `.github/workflows/deploy.yml` — build/test, deploy, smoke test (no infra step) | PR reviewers must remember infra changes aren't auto-applied — covered in the workflow's own top comment and `infra/README.md` |
| 3. Validation + runbook | Real pipeline run, confirmed healthy dev app, `infra/README.md` | None beyond normal first-run flakiness |

**Prerequisites:** Subscription-level access (Contributor/Owner) on `melodybooker-rg` to read the publish profile; `gh` CLI admin access to the repo for secrets.
**Estimated effort:** ~1 session across 3 phases (mostly `az`/`gh` commands + one workflow file).

## Open Risks & Assumptions

- **Resolved during implementation**: the original OIDC plan assumed Azure AD
  App Registration rights were available; the tenant blocked this entirely
  with no admin path. Pivoted to a Publish Profile credential (ARM-level, no
  Azure AD), which also means Bicep/infra deploys from CI are no longer
  possible — infra stays a manual, local step (see plan.md Overview).
- Assumes the Web App's managed identity has **not** yet been granted SQL
  access — if it already was (undocumented), Phase 1 item 3 is a harmless no-op
  (`CREATE USER` on an existing user fails loudly, which is fine to re-run
  idempotently with `IF NOT EXISTS` guards if needed).
- Assumes the human running Phase 1's SQL commands has sufficient Azure
  AD and subscription rights to act as SQL AAD admin (confirmed: user already
  is the SQL AAD admin and is logged into `az cli` with access to
  `melodybooker-rg`).

## Success Criteria (Summary)

- Pushing to `main` deploys app code automatically — no manual `az webapp deploy` ever again for dev.
- `/health` on the live dev app returns `200 Healthy`.
- No long-lived Azure AD secret is stored in GitHub (publish-profile credential is scoped to just this one Web App, not subscription-wide).
