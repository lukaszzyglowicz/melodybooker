---
project: melodybooker
researched_at: 2026-10-05
recommended_platform: Azure App Service
runner_up: Fly.io
context_type: mvp
tech_stack:
  language: C#
  framework: ASP.NET Core MVC
  runtime: .NET 10 (net10.0)
---

## Recommendation

**Deploy on Azure App Service, with Azure SQL Database (Basic/DTU tier) co-located in the same resource group and region.**

Azure App Service is the only researched platform that offers a *managed* SQL Server (via Azure SQL Database) in the same ecosystem — Fly.io, Railway, and Render all require self-hosting SQL Server in an unmanaged container, which contradicts the "colocation preferred" interview answer and adds real operational burden (backups, patching, HA) that a solo developer on a 3-week after-hours MVP timeline cannot absorb. The "hyperscaler familiarity" answer and the single-region requirement further favor Azure, and the realistic cost (~$18–20/month for B1 App Service Plan + SQL Basic DTU) is affordable even under the cost-minimizing preference. Cloudflare Workers, Vercel, and Netlify were hard-filtered out: none can natively run a server-side ASP.NET Core application (confirmed via research — Cloudflare only supports .NET via WASI/WASM, Vercel requires beta/limited container workarounds, Netlify has no container support at all).

## Platform Comparison

Scoring: Pass = 2, Partial = 1, Fail = 0. Hard-filtered platforms (Cloudflare Workers, Vercel, Netlify) are excluded from the table below — none can run server-side ASP.NET Core without unsupported or beta workarounds.

| Platform | CLI-first | Managed/Serverless | Agent-readable docs | Stable deployment API | MCP/Integration | Sum |
|---|---|---|---|---|---|---|
| **Azure App Service** | Pass | Pass | Partial | Pass | Partial | **8/10** |
| Fly.io | Pass | Partial | Pass | Pass | Partial | 8/10 |
| Railway | Pass | Partial | Pass | Partial | Pass | 8/10 |
| Render | Partial | Partial | Pass | Pass | Pass | 8/10 |

All four platforms that can run .NET tie on raw agent-friendly scoring. The deciding factors are the soft weights from the interview:

- **Azure App Service**: `az webapp up/deploy`, `az webapp log tail`, and slot-swap rollback are all fully CLI-scriptable (Pass). Azure SQL Database, Key Vault, and App Configuration co-locate natively in the same resource group (Pass on managed services for *this specific stack*, since SQL Server is a hard requirement). Docs are open-sourced markdown on GitHub (`MicrosoftDocs/azure-docs`) but no confirmed Azure App Service-specific `llms.txt` was found (Partial). Deployment API via `az` CLI and GitHub Actions (publish profile or OIDC) is GA and deterministic (Pass). The "Azure MCP Server" landscape is fragmented — a registry of many narrow MCP servers rather than one coherent product — so agent-driven day-2 ops are less turnkey than on Fly.io/Railway (Partial).
- **Fly.io**: Fully CLI-scriptable (`flyctl`), has a live `llms.txt`, and GA persistent-process/WebSocket support. However, SQL Server has no managed offering — it must run as a self-hosted Linux container with a Fly Volume, which is an unmanaged, ops-heavy workaround for this specific stack's database requirement (Partial on managed services).
- **Railway**: Strong CLI, official `llms.txt`, and a genuinely GA, documented MCP server (`railway mcp`) — the best agent-integration story researched. But .NET requires a Dockerfile (no buildpack auto-detection), there's no dedicated rollback CLI verb (redeploy-by-ID is the workaround), and SQL Server is only available as an unmanaged community template.
- **Render**: Official Render MCP Server (GA) and strong docs, but rollback has weaker CLI parity (API-only), free-tier cold starts are significant (~1 min), and — like Fly.io and Railway — there is no managed SQL Server, only managed Postgres/Redis.

### Shortlisted Platforms

#### 1. Azure App Service (Recommended)

Wins primarily on the **colocation** criterion: Azure SQL Database is a true managed service in the same resource group as the app, matching the tech stack's explicit SQL Server choice and the user's stated preference to avoid juggling external data providers. Combined with confirmed hyperscaler familiarity, this removes the single largest operational risk (self-managing a production SQL Server) that the other three candidates all share.

#### 2. Fly.io

Best runner-up: cheapest compute, fully CLI-scriptable, agent-readable docs, and true persistent-process support. Would require accepting an unmanaged SQL Server container (or migrating to Fly Postgres, which would mean abandoning the SQL Server choice already locked into `tech-stack.md`).

#### 3. Railway

Close third: the most mature CLI+MCP agent-integration story of all candidates, but shares Fly.io's and Render's SQL Server gap, and adds a Dockerfile-only deployment path (no .NET buildpack).

## Anti-Bias Cross-Check: Azure App Service

### Devil's Advocate — Weaknesses

1. .NET 10 is still labeled "Preview" in the App Service runtime-stack picker even though .NET 10 itself reached GA/LTS on 2025-11-11 — a solo dev on a hard 2026-12-10 deadline could hit platform-side rollout quirks on a technically-preview stack.
2. Deployment slots (the mechanism for zero-downtime rollback) require the **Standard (S1, ~$70/month)** tier, not Basic (~$13/month) — the realistic cost of "colocated + safely rollback-capable" is roughly 4x the naive Basic-tier estimate.
3. Azure SQL Database Serverless (autopause) pricing looked attractive on paper but has a much higher non-paused baseline (~$150–200+/month) than the DTU Basic tier (~$5/month) — easy to provision the wrong SKU and get an unpleasant bill.
4. SQL firewall/VNet misconfiguration between App Service and Azure SQL is a common first-deploy failure (outbound IPs must be whitelisted, or managed identity configured) — not a zero-config operation.
5. The "Azure MCP Server" GA/agent-integration status is murky — multiple inconsistent third-party claims were found with no single canonical confirmed status, so agent-driven ops on Azure are less turnkey than on Fly.io/Railway/Render.

### Pre-Mortem — How This Could Fail

The team deployed MelodyBooker to Azure App Service B1 + SQL Database Basic for the MVP. Six months later it was a mess. During the 3-week sprint, the solo developer skipped deployment slots to save the $70/month Standard-tier cost, so every deploy went straight to production with no safe rollback path — a bad migration once caused a multi-hour outage during school hours. The .NET 10 "Preview" stack label turned out to matter: a platform-side patch mid-month changed default TLS behavior, breaking the SQL connection string until the dev found the right encryption setting. Application Insights was never configured, so the outage was diagnosed by guesswork rather than logs. Azure SQL's firewall rules were left wide open ("Allow Azure services") because narrowing them to App Service outbound IPs kept breaking on scale-out (IPs change). Cost crept from the planned ~$18/month to ~$45/month once Always On and a second Basic instance were added to fix cold-start complaints. None of this was individually catastrophic, but compounded, it ate the "after-hours solo dev" time budget the project never had to spare.

### Unknown Unknowns

- App Service Basic-tier outbound IPs can change on scale/restart, so "just whitelist the IP" for the SQL firewall is a maintenance trap — managed identity auth is the durable fix, but it's an extra setup step most tutorials skip.
- "Always On" (needed to avoid cold starts after idle) is unavailable on Free/Shared tiers and silently defaults to off on cheaper plans — a teacher opening the app after a quiet period could hit a 10–20s cold response if this isn't explicitly enabled.
- Deployment slot swaps don't automatically prevent cold starts — the swapped-in production slot can still go cold immediately after swap unless `WEBSITE_SWAP_WARMUP_PING_PATH` is configured, an easy-to-miss non-default setting.
- GitHub Actions' simplest publish-profile auth stores a long-lived credential with broad permissions; the more secure OIDC/federated-credential setup is GA but requires noticeably more one-time Entra ID configuration than competing platforms' "paste a token" flow.
- The Azure MCP/agent-tooling landscape is fragmented (a registry of many narrow MCP servers rather than one coherent "azure-mcp"), so agent-driven day-2 operations (log reading, redeploys) will likely require more bespoke CLI scripting than on Fly.io or Railway, which have a single cohesive CLI+MCP story.

## Operational Story

- **Preview deploys**: App Service deployment slots (Standard tier, ~$70/mo) provide a staging URL for PR/branch builds before swapping into production; on Basic tier there is no slot, so "preview" in practice means deploying to a second, cheaper App Service instance manually if needed during the MVP phase.
- **Secrets**: Store connection strings and API keys as App Service Application Settings, backed by Azure Key Vault references with Managed Identity (no secrets stored in the app itself); GitHub Actions credentials live as GitHub Secrets (publish profile) or as an Entra ID federated credential (OIDC, no stored secret) — prefer OIDC.
- **Rollback**: `az webapp deployment slot swap --name <app> --resource-group <rg> --slot staging` to promote, and the same swap run in reverse to roll back instantly (Standard tier only); on Basic tier, rollback means redeploying the previous build artifact via `az webapp deploy`. Database migrations do not auto-roll-back — any EF Core migration applied during a bad deploy needs a manual down-migration or restore from Azure SQL point-in-time backup.
- **Approval**: Promoting a slot swap to production, rotating the SQL admin credential/Key Vault secrets, and deleting/resizing the Azure SQL Database should require explicit human approval. Routine `az webapp deploy` to a non-production slot and `az webapp log tail` are safe for an agent to run unattended.
- **Logs**: `az webapp log tail --name <app> --resource-group <rg>` for live streaming; `az webapp log download` for archived logs; Application Insights (if enabled) exposes queryable logs via `az monitor app-insights query` — all read-only and CLI-scriptable.

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| .NET 10 "Preview" label on App Service causes an unexpected platform-side behavior change mid-sprint | Devil's advocate | M | M | Pin and test against the current App Service .NET 10 stack early; keep a Docker-container fallback (App Service supports Linux containers) if the native stack misbehaves before the Dec 10 deadline |
| Rollback requires Standard tier (~$70/mo), far above the Basic-tier cost estimate, creating a cost-vs-safety tradeoff | Devil's advocate | H | M | Budget for Standard tier from the start, or accept Basic-tier manual-redeploy rollback for the MVP given low traffic and non-critical early-stage risk |
| Azure SQL Database SKU mis-selection (Serverless baseline cost surprise) | Devil's advocate | M | M | Provision SQL Basic DTU (~$5/mo) for MVP instead of Serverless; revisit only if traffic patterns justify autopause |
| SQL firewall/outbound-IP churn breaks connectivity after scale events | Unknown unknowns | M | H | Use Managed Identity + Azure SQL AAD authentication instead of IP allowlisting from day one |
| Cold starts after idle periods degrade teacher-facing UX | Pre-mortem / Research finding | M | L | Enable "Always On" on the Basic tier plan (confirm availability) and configure `WEBSITE_SWAP_WARMUP_PING_PATH` if/when slots are added |
| No managed-service agent tooling (fragmented Azure MCP landscape) slows agent-driven ops | Devil's advocate | M | L | Rely on `az` CLI scripting directly rather than waiting on a unified Azure MCP server; document the exact `az` commands used in project runbooks |
| EF Core migrations applied during a bad deploy are not automatically reversible | Operational story / Research finding | L | H | Always take an Azure SQL point-in-time backup checkpoint before applying migrations in production; write down-migrations alongside up-migrations |

## Getting Started

1. Confirm the exact .NET 10 App Service runtime stack availability for your target region before relying on it: `az webapp list-runtimes --os linux | grep -i dotnet`.
2. Create the resource group, App Service plan (Basic B1), and web app: `az group create -n melodybooker-rg -l <region>`, `az appservice plan create -n melodybooker-plan -g melodybooker-rg --sku B1 --is-linux`, `az webapp create -g melodybooker-rg -p melodybooker-plan -n melodybooker --runtime "DOTNETCORE:10.0"`.
3. Provision Azure SQL Database (Basic DTU) in the same resource group/region: `az sql server create`, `az sql db create --service-objective Basic`, then enable Managed Identity on the web app and grant it SQL access instead of using IP-based firewall rules.
4. Wire up GitHub Actions deployment using OIDC (not publish-profile) per `learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect`, so `git push` to main triggers `az webapp deploy` without a stored long-lived secret.
5. Validate the full operational loop before building features: deploy a "hello world" MVC page, confirm `az webapp log tail` shows live logs, and confirm the app connects to Azure SQL Database via Managed Identity.

## Out of Scope

The following were not evaluated in this research:
- Docker image configuration
- CI/CD pipeline setup
- Production-scale architecture (multi-region, HA, DR)
