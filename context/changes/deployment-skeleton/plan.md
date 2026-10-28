# Deployment Skeleton (F-02) — CI/CD for the Existing Azure Dev Environment

## Overview

`infra/` already contains a complete Bicep module set and it is **already deployed
live** in Azure (`melodybooker-rg`, environment `dev`: App Service Plan, Web App,
Azure SQL with AAD-only auth, Key Vault, Log Analytics, Application Insights).
What's missing is the **CI/CD glue**: there is no GitHub Actions workflow, no
credential wired between GitHub and Azure, and the application code currently
running on the live Web App is stale (the deployed `/health` endpoint redirects
to the login page instead of returning `Healthy`, because the code deployed
there predates the current `AllowAnonymous()` health-check wiring). This plan
wires `push`-to-`main` so it automatically deploys the current app code to the
*existing* dev Web App, closing the roadmap's F-02 item. `prod` is
intentionally **not** created by this plan.

> **Adaptation (discovered during Phase 1 implementation, 2026-10-26)**: the
> plan originally called for OIDC via a dedicated Azure AD App Registration.
> The user's Azure AD tenant (`hitachienergy.com`) blocks non-admin users from
> registering applications ("Insufficient privileges to complete the
> operation"), and no path to an Azure AD admin exists for this project. Azure
> AD app registration rights are a *directory*-level permission, entirely
> separate from the user's `Owner` role on the Azure *subscription* — so this
> blocks OIDC/service-principal auth of any kind, not just this specific
> command. Verified there is no PIM-eligible directory role to self-activate
> either (`roleEligibilityScheduleInstances` query returned
> `PermissionScopeNotGranted`). Resolution: use the Web App's **Publish
> Profile** (Kudu/SCM deployment credential) instead — an ARM-level credential
> gated only by `Microsoft.Web/sites/*` RBAC (which the user's subscription
> `Owner` role already grants), with zero Azure AD dependency. This covers app
> *code* deploys. Azure AD's blocked app-registration also means CI cannot
> authenticate to redeploy *infrastructure* (Bicep) at all — the pipeline
> originally planned to re-apply Bicep on every push, including
> `Microsoft.Authorization/roleAssignments` writes requiring `User Access
> Administrator`; with no CI-side Azure identity, this is no longer possible.
> Bicep deploys stay a **manual, local step** run by whoever has subscription
> access (unchanged from today's actual practice — infra rarely changes, and
> the user already applies it this way). See updated Phase 1/2 below and "What
> We're NOT Doing".

## Current State Analysis

- `infra/main.bicep` + `infra/modules/*.bicep` define: App Service Plan (B1),
  Linux Web App (`DOTNETCORE|10.0`, system-assigned identity, Key Vault
  reference for the connection string, `WEBSITE_SWAP_WARMUP_PING_PATH=/health`),
  Azure SQL (AAD-only auth, no SQL password), Key Vault (RBAC-mode), Log
  Analytics + Application Insights. `infra/main.parameters.dev.json` is the only
  parameters file — no `prod` file exists.
- Confirmed via `az resource list --resource-group melodybooker-rg`: all of the
  above are **already provisioned and running** (`melodybooker-dev-app-k7sqmyvh2szbg`
  is `Running`). This was deployed previously by hand/CLI on an earlier course
  branch (`git log --oneline -- infra` → single commit `076a1c5` on `m1l5`), not
  via any pipeline that exists today.
- `src/melody/Program.cs:12-13,48-49,75,78-90` — EF Core is fully wired:
  `AddDbContext<ApplicationDbContext>`, a `/health` endpoint with
  `AddDbContextCheck<ApplicationDbContext>().AllowAnonymous()`, and the app
  applies pending EF Core migrations itself at startup
  (`db.Database.MigrateAsync()`), explicitly commented as a stopgap "Phase 0: no
  CI/CD migration step exists yet".
- Live smoke test: `GET https://melodybooker-dev-app-k7sqmyvh2szbg.azurewebsites.net/health`
  returns HTTP 200 but with the **login page** HTML body, not `Healthy` text —
  the code running there is older than the current `main`/`m2l6` HEAD.
- `.github/workflows/` contains only `sync-roadmap-issues.yml` — no deploy
  workflow. `gh secret list` returns zero secrets. No Azure AD App Registration
  named `melodybooker*` exists (`az ad app list` empty) — OIDC trust must be
  created from scratch.
- `infra/modules/sqlDatabase.bicep:19-33` sets `azureADOnlyAuthentication: true`
  with no SQL password login at all. The Web App's connection string
  (`infra/main.bicep:97-104`) authenticates as `Active Directory Managed
  Identity`, which requires the Web App's managed identity to exist as a
  **contained database user inside `melodybookerdb`** — this grant is SQL-level
  (`CREATE USER … FROM EXTERNAL PROVIDER`), not an ARM/Bicep resource, and no
  such script exists anywhere in the repo. Nothing confirms this grant has ever
  been applied — if it hasn't, `MigrateAsync()` will throw at Web App startup
  once current code is deployed, crash-looping the app.
- `infra/main.bicep:84-95` creates two `Microsoft.Authorization/roleAssignments`
  (Key Vault Secrets Officer for the AAD admin user, Secrets User for the Web
  App's identity). Re-running this template requires the executing principal to
  hold `Microsoft.Authorization/roleAssignments/write` at that scope — plain
  **Contributor does not include this**; the identity doing `az deployment
  group create` needs `User Access Administrator` (or `Owner`) on
  `melodybooker-rg`, in addition to Contributor.
- `context/changes/azure-deployment-plan/plan.md` (different, informal
  change-id, predates `infra/`) is now stale on several points (assumed EF Core
  still needed to be added, assumed migration strategy was undecided) but its
  resource-naming/region/SKU choices match what's actually deployed, and its
  "Etap 3" CI/CD sketch (what-if on PR, real deploy on merge, OIDC) is the basis
  for Phase 2 below.

### Key Discoveries:

- The Web App's managed-identity principal id is a Bicep output
  (`webApp.outputs.principalId`, `infra/main.bicep:143`) — reuse it for the
  one-time SQL grant script rather than looking it up by hand.
- `src/melody/Program.cs:93-95` already special-cases a `Testing` ASP.NET Core
  environment to use `EnsureCreatedAsync()` instead of `MigrateAsync()` — CI's
  `dotnet test` run is unaffected by any of this.
- Tests live in `tests/melody.Tests/melody.Tests.csproj`; solution is
  `melodybooker.sln`.

## Desired End State

Pushing a commit to `main` automatically: builds + tests the app, publishes
and deploys the current app code to the dev Web App via a publish-profile
credential, and verifies `/health` returns `Healthy`. Infrastructure (Bicep)
is applied manually/locally, not by CI (see Overview adaptation note). `prod`
does not exist yet and the workflow does not attempt to create it.

**Verification**: merge a commit to `main`, watch the workflow run turn green,
then `curl https://melodybooker-dev-app-k7sqmyvh2szbg.azurewebsites.net/health`
returns `200 Healthy` and the rendered UI reflects the current `main` code
(e.g. shows the Administrator reservation-oversight screens).

## What We're NOT Doing

- **Not creating a `prod` environment** — no `infra/main.parameters.prod.json`,
  no prod resource group, no prod deploy job. Deferred to a future change (the
  workflow is intentionally dev-only; see Phase 2).
- **Not destroying/recreating the existing dev resources** — nothing in this
  plan touches the resource group's existing resources.
- **Not redeploying infrastructure (Bicep) from CI** — discovered during Phase
  1 that the Azure AD tenant blocks non-admin app registration, so no
  CI-side Azure identity could be provisioned at all (not even for Bicep
  reads/what-if). Infra changes stay a manual, local `az deployment group
  create`, same as how the live environment was originally provisioned. This
  is a tighter scope than originally planned (see Overview adaptation note) —
  not a deliberate simplification.
- **Not changing the EF Core migration strategy** — `MigrateAsync()` at Web App
  startup stays exactly as implemented today; the pipeline does not add a
  separate migration step or approval gate.
- **Not moving to Standard-tier App Service Plan / deployment slots** — stays
  on Basic B1 (no slots); rollback remains "redeploy the previous artifact",
  per `infrastructure.md`'s already-accepted risk tradeoff.
- **Not building a Docker image** — the Web App runs the native
  `DOTNETCORE|10.0` Linux runtime stack; app code is zip-deployed, not
  containerized.
- **Not setting up Azure Monitor alerting/dashboards** — Application Insights
  collection already exists; alert rules are out of scope.
- **Not pursuing OIDC further** — would require an Azure AD admin outside this
  project's reach; the publish-profile credential is the pragmatic path given
  that constraint (see Overview adaptation note). Revisit OIDC if/when
  Azure AD app-registration access becomes available.

## Implementation Approach

Three phases, in dependency order: (1) obtain a publish-profile credential for
app-code deploys (no Azure AD identity needed — see Overview adaptation note),
including the one-time SQL grant for the Web App's managed identity; (2) add
the GitHub Actions workflow itself (build/test gate → app code deploy + smoke
test on `main`); (3) run it for real and document the result, including that
infra changes stay a manual, local step.

## Phase 1: Publish-profile credential for app-code deploys

> **Adapted from the original OIDC-based plan** — see the Overview note above.
> No Azure AD App Registration, no OIDC federated credential, no RBAC role
> assignments. The pipeline needs exactly one credential, scoped only to this
> Web App, obtainable with the subscription access the user already has.

### Overview

Retrieve the Web App's publish profile (Kudu/SCM deployment credential) and
store it as a single GitHub Actions secret. Separately, grant the
already-existing Web App managed identity a SQL database user inside
`melodybookerdb` so the app can actually read/write data once current code is
deployed — this part is unchanged from the original plan, since it has nothing
to do with CI authentication.

### Changes Required:

#### 1. Retrieve the Web App's publish profile

**File**: none (Azure-side, read-only ARM call — no Azure AD involved)

**Intent**: Obtain the Kudu/SCM deployment credential scoped to exactly this
one Web App. This is an ARM-level action gated by `Microsoft.Web/sites/*` RBAC
(the user's subscription `Owner` role already covers it), not an Azure AD
permission — confirmed working during this phase.

**Contract**:

```bash
az webapp deployment list-publishing-profiles \
  --resource-group melodybooker-rg \
  --name melodybooker-dev-app-k7sqmyvh2szbg \
  --xml > publish-profile.xml
```

#### 2. Store it as a GitHub Actions secret

**File**: GitHub repo settings (via `gh` CLI, not a repo file — the XML must
never be committed)

**Intent**: Make the credential available to the deploy workflow as
`AZURE_WEBAPP_PUBLISH_PROFILE`.

**Contract**:

```bash
gh secret set AZURE_WEBAPP_PUBLISH_PROFILE < publish-profile.xml
rm publish-profile.xml   # never leave the credential on disk
```

#### 3. One-time SQL grant for the Web App's managed identity

**File**: `infra/sql/grant-webapp-identity.sql` (new) — **unchanged from the
original plan**, already created during this phase.

**Intent**: Create the Web App's system-assigned managed identity as a
contained database user inside `melodybookerdb` and grant it read/write roles,
so EF Core's `MigrateAsync()`/queries succeed once current app code (with its
real connection string) is running. This is unrelated to the CI auth
mechanism above and still required regardless of how the app code gets
deployed.

**Contract**: (already committed as part of this phase)

```sql
CREATE USER [melodybooker-dev-app-k7sqmyvh2szbg] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [melodybooker-dev-app-k7sqmyvh2szbg];
ALTER ROLE db_datawriter ADD MEMBER [melodybooker-dev-app-k7sqmyvh2szbg];
```

### Success Criteria:

#### Automated Verification:

- `gh secret list` shows `AZURE_WEBAPP_PUBLISH_PROFILE`

#### Manual Verification:

- `infra/sql/grant-webapp-identity.sql` has been run once against `melodybookerdb` by the AAD admin and completes without error (user confirms)
- Connecting to the DB and running `SELECT name FROM sys.database_principals WHERE name = 'melodybooker-dev-app-k7sqmyvh2szbg'` returns one row

---

## Phase 2: GitHub Actions CI/CD workflow

### Overview

Add `.github/workflows/deploy.yml`: build + test gate on every push/PR; on
push to `main`, publish + zip-deploy the app code to the dev Web App via the
publish-profile secret, then a smoke test against `/health`. Infrastructure
(Bicep) changes are **not** applied by this workflow — they stay a manual,
local `az deployment group create` run by whoever has subscription access (see
Overview adaptation note), reviewed the same way any other code change is
reviewed (PR diff), just applied out-of-band instead of by CI.

### Changes Required:

#### 1. CI/CD workflow definition

**File**: `.github/workflows/deploy.yml` (new)

**Intent**: Single workflow, two logical flows gated by event type:
- `pull_request` targeting `main`: `dotnet build` + `dotnet test` only. No
  Azure interaction at all — there is no CI-side Azure credential capable of
  running `what-if` (that would need `az login`, which is exactly what's
  blocked; see Overview).
- `push` to `main`: same build+test gate, then `dotnet publish -c Release -o
  publish-output`, zip it, and deploy via `azure/webapps-deploy@v3` using the
  publish-profile secret, then `curl --fail` against `/health` and grep for
  `Healthy`.

**Contract**: Deploy step uses `azure/webapps-deploy@v3` with `app-name:
melodybooker-dev-app-k7sqmyvh2szbg` and `publish-profile:
${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}` — no `azure/login` step, no
`permissions: id-token: write`, no OIDC. `dotnet-version: '10.0.x'` via
`actions/setup-dotnet@v4`. The resource group/Web App name are literal
(`melodybooker-rg`, `melodybooker-dev-app-k7sqmyvh2szbg`) per the confirmed
dev-only scope.

#### 2. Document the deferred `prod` path and the manual infra step

**File**: `.github/workflows/deploy.yml` (same file, top-of-file comment)

**Intent**: A top-of-file comment stating that (a) this workflow intentionally
targets `dev` only, `prod` is future follow-up, and (b) infra/Bicep changes
are NOT applied by this workflow — they're a manual local step, documented in
`infra/README.md` (Phase 3), because no CI-side Azure AD identity could be
provisioned in this tenant.

**Contract**: Comment block, no functional code.

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` passes inside the workflow run
- `dotnet test` passes inside the workflow run (50/50, matching local baseline)
- The `push`-to-`main` job completes with exit code 0 end-to-end (publish + zip-deploy + smoke test step)

#### Manual Verification:

- Opening a PR against `main` shows only the build/test gate running (no infra preview — infra changes are reviewed via the PR diff like any other change, then applied manually; this is a known, accepted, and documented tradeoff)
- After merging to `main`, the Actions tab shows the workflow green

---

## Phase 3: End-to-end validation + runbook

### Overview

Trigger the real pipeline, confirm the live dev app now serves current code
and is healthy, and write down the operational notes (rollback, where `prod`
is deferred) so this isn't tribal knowledge.

### Changes Required:

#### 1. Operational runbook

**File**: `infra/README.md` (new)

**Intent**: Short runbook: how deploys happen (push to `main`), how to roll
back (redeploy the previous artifact via `az webapp deploy` with the prior zip
— no slot swap on Basic tier), how to run the one-time SQL grant for a future
environment, and an explicit "prod is not provisioned yet" note pointing at
the roadmap's F-02 follow-up.

**Contract**: Markdown document; no code contract.

### Success Criteria:

#### Automated Verification:

- `curl -s -o /dev/null -w '%{http_code}' https://melodybooker-dev-app-k7sqmyvh2szbg.azurewebsites.net/health` returns `200`
- Response body of the same request contains `Healthy`

#### Manual Verification:

- Loading `https://melodybooker-dev-app-k7sqmyvh2szbg.azurewebsites.net/` shows the current login page styling/branding (confirms current code, not the stale deploy)
- Logging in as the seeded Administrator and opening the reservation-oversight screen (from the just-archived `admin-reservation-oversight` change) works against the live dev database
- `infra/README.md` accurately describes the rollback steps (spot-check by reading it)

---

## Testing Strategy

### Unit Tests:

- No new unit tests — this change is infra/CI, not application logic. The
  existing `dotnet test` suite (50 tests) is the gate the pipeline runs.

### Integration Tests:

- The pipeline's own smoke test (`curl /health`) is the integration check for
  this change; there is no separate test project to add.

### Manual Testing Steps:

1. Open a PR touching any file and confirm the `what-if` step runs and reports no destructive changes.
2. Merge to `main` and watch the Actions run turn green.
3. Hit `/health` and the app's login page; confirm current code and DB connectivity.

## Migration Notes

No application data migration. The one-time SQL grant (Phase 1, item 4) is the
only state-changing step outside of normal `dotnet ef` migrations, and it only
needs to run once per environment (dev now; `prod` later, when that environment
is created).

## References

- `infra/main.bicep`, `infra/modules/*.bicep` — existing infrastructure definitions
- `src/melody/Program.cs:12-13,48-49,75,78-95` — EF Core wiring, health check, startup migration
- `context/foundation/infrastructure.md` — platform research, risk register (rollback, cold starts, SQL firewall)
- `context/changes/azure-deployment-plan/plan.md` — superseded informal plan (resource naming/region/SKU choices still accurate)
- `context/foundation/roadmap.md` — F-02 item this change closes

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Publish-profile credential for app-code deploys

#### Automated

- [x] 1.1 GitHub secret AZURE_WEBAPP_PUBLISH_PROFILE is set — 1507eb3

#### Manual

- [ ] 1.2 SQL grant script run once against melodybookerdb
- [ ] 1.3 sys.database_principals query confirms the contained user exists

### Phase 2: GitHub Actions CI/CD workflow

#### Automated

- [ ] 2.1 dotnet build passes in workflow
- [ ] 2.2 dotnet test passes in workflow (50/50)
- [ ] 2.3 push-to-main job completes end-to-end with exit code 0 (publish + zip-deploy + smoke test)

#### Manual

- [ ] 2.4 PR shows only build/test gate running, no infra preview
- [ ] 2.5 Actions tab shows green run after merge to main

### Phase 3: End-to-end validation + runbook

#### Automated

- [ ] 3.1 /health returns HTTP 200
- [ ] 3.2 /health body contains "Healthy"

#### Manual

- [ ] 3.3 Login page shows current code/branding
- [ ] 3.4 Admin reservation-oversight screen works against live dev DB
- [ ] 3.5 infra/README.md accurately describes rollback and the manual infra-deploy step
