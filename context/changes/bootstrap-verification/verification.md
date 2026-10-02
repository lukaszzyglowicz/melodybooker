---
bootstrapped_at: 2026-10-02T09:12:00Z
starter_id: dotnet
starter_name: ".NET (ASP.NET Core webapi)"
project_name: melodybooker
language_family: dotnet
package_manager: dotnet
cwd_strategy: custom-override (user-directed location + template, see notes)
bootstrapper_confidence: verified
phase_3_status: ok
audit_command: "dotnet list package --vulnerable"
---

## Hand-off

```yaml
starter_id: dotnet
package_manager: dotnet
project_name: melodybooker
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: custom
  quality_override: false
  self_check_answers:
    typed: true
    from_official_starter: true
    conventions: true
    docs_current: true
    can_judge_agent: true
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: true
```

> Solo developer building a music-school room-booking web app within a short after-hours
> MVP timeline, with authentication and weekly-recurring scheduling (treated as a
> background-jobs concern) in scope. The user chose the custom path and confirmed a
> preference for SQL Server, which pairs naturally with this starter's Entity Framework
> integration. .NET (ASP.NET Core webapi) is the only registry candidate for the
> `(web-app, dotnet)` cell and clears all four agent-friendly gates. Bootstrapper
> confidence is verified. Deployment defaults to Azure App Service; CI runs on GitHub
> Actions with auto-deploy-on-merge. The five-point self-check came back clean.

**Run-time override (confirmed with user at Step 0):** the user corrected the scaffold
location and template for this run only — the hand-off file on disk is unchanged.

| Field       | Hand-off value              | Override applied this run        |
| ----------- | ---------------------------- | --------------------------------- |
| Location    | cwd root                     | `src/`                            |
| Project dir | (n/a — cwd is the project)   | `src/dotnet10`                    |
| Template    | `webapi` (ASP.NET Core API)  | `mvc` (ASP.NET Core MVC web app)  |
| .NET target | SDK default                  | `net10.0`                         |

## Pre-scaffold verification

| Signal       | Value                                          | Severity | Notes                                              |
| ------------ | ----------------------------------------------- | -------- | --------------------------------------------------- |
| npm package  | not run                                          | n/a      | non-JS starter; no npm-distributed CLI               |
| GitHub repo  | not run                                          | n/a      | `docs_url` is `https://learn.microsoft.com/aspnet/core`, not a GitHub repo URL — no recency signal available |
| Local SDK    | .NET SDKs 9.0.314 and 10.0.200 installed         | fresh    | `net10.0` target is supported locally                |

No staleness warning raised. Proceeding was unconditional (WARN-AND-CONTINUE slot; no signal to warn on here).

## Scaffold log

**Resolved invocation**: `dotnet new mvc -n dotnet10 -o src/dotnet10 --no-restore -f net10.0`
**Strategy**: custom (user-directed subdirectory output, not the registry's default `subdir-then-move`/root-cwd convention)
**Exit code**: 0
**Files written**: 24 (Controllers, Models, Views, wwwroot assets, Program.cs, dotnet10.csproj, appsettings*.json, launchSettings.json)
**Conflicts (.scaffold siblings)**: none — `src/dotnet10/` did not previously exist, so the conflict matrix had nothing to adjudicate
**.gitignore handling**: absent in scaffold (this `dotnet new mvc` template does not emit its own `.gitignore`); cwd's existing `.gitignore` untouched
**Temp-dir cleanup**: n/a — scaffolded directly to `src/dotnet10`, no `.bootstrap-scaffold/` round-trip needed since the target directory was new and user-specified

`context/` in cwd was not touched at any point.

## Post-scaffold audit

**Tool**: `dotnet list package --vulnerable` (run after `dotnet restore` inside `src/dotnet10`)
**Summary**: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
**Direct vs transitive**: not applicable — no vulnerable packages reported
**Raw output**: "The given project `dotnet10` has no vulnerable packages given the current sources."

## Hints recorded but not acted on

| Hint                     | Value               |
| ------------------------ | -------------------- |
| bootstrapper_confidence  | verified              |
| quality_override         | false                 |
| path_taken               | custom                |
| self_check_answers       | typed=true, from_official_starter=true, conventions=true, docs_current=true, can_judge_agent=true |
| team_size                | solo                  |
| deployment_target        | azure-app-service     |
| ci_provider              | github-actions        |
| ci_default_flow          | auto-deploy-on-merge  |
| has_auth                 | true                  |
| has_payments             | false                 |
| has_realtime             | false                 |
| has_ai                   | false                 |
| has_background_jobs      | true                  |

## Next steps

Next: a future skill will set up agent context (the project's AI configuration file, AGENTS.md). For now, your project is scaffolded and verified — happy hacking.

Useful manual steps in the meantime:
- `git init` (if you have not already) to start your own repo history.
- The hand-off's recommended template was `webapi`; you scaffolded `mvc` instead at `src/dotnet10` — reconcile this with your PRD/plan if the API-only shape was assumed elsewhere.
- Wire up SQL Server + Entity Framework per the hand-off's rationale (not scaffolded automatically in v1).
- Address `has_auth` and `has_background_jobs` (weekly-recurring bookings) — no scaffolding was added for these in v1; they are logged as hints only.
- No vulnerable packages found at scaffold time — re-run `dotnet list package --vulnerable` periodically as dependencies change.
