---
type: observability-audit
date: 2026-11-06 15:20
mode: audit            # audit | verify
commit: b0413e7
branch: m3l5
dirty_tree: false
areas: [reservation-booking, admin-roster-management, auth-and-plumbing]
area_source: foundation  # foundation | arguments | user
runtime_proof: not-run
error_tracker: Application Insights (conditional, env-gated)
previous_report: null
findings: { critical: 2, high: 2, medium: 2, low: 1 }
---

# Observability audit — reservation-booking, admin-roster-management, auth-and-plumbing (2026-11-06)

## 1. TL;DR

- **Every `catch (DbUpdateException)` in the app (3 of 3 sites) swallows the
  exception completely** — no `ILogger` call, no tracker call, nothing. A
  real DB failure (deadlock, connection drop, unrelated constraint) during a
  booking or a teacher-account creation looks identical, from the server's
  point of view, to the one case the code actually expects (the
  double-booking race) — both just render a generic in-app message.
- **No controller in the codebase takes an `ILogger<T>` dependency.** The
  only place that logs anything is startup seeding
  (`Data/AdminUserSeeder.cs`). Even if Application Insights is fully wired
  in Azure, business-logic failures have no code path that would ever reach
  it.
- `AdminTeachersController.Create`'s compensating cleanup
  (`_userManager.DeleteAsync(user)`, line 93) discards its own result. If
  the compensation itself fails, an orphaned `ApplicationUser` with the
  Teacher role but no linked `Teacher` row can exist — and the one place
  that reads it (`TeacherController.Index`) treats "no linked row" as an
  ordinary empty roster, not an error, so the broken account is invisible
  forever.
- Deploys (`.github/workflows/deploy.yml`) never pass a commit SHA or build
  version into the running app, so even a fully working tracker couldn't
  attribute an event to the deploy that caused it.
- Positive counter-example: `Data/AdminUserSeeder.cs` does this correctly
  (`LogWarning`/`LogError` with the real exception) — proof the pattern is
  known, just not applied anywhere else.

## 2. Capture model

- **Runtime:** single long-running ASP.NET Core MVC server process
  (`Program.cs`), no background workers, queues, or serverless functions
  found anywhere in `src/melody`.
- **Error tracker:** Application Insights, wired only when a connection
  string is present (`Program.cs:38-44`): `builder.Services
  .AddApplicationInsightsTelemetry()` runs conditionally on
  `ApplicationInsights:ConnectionString` /
  `APPLICATIONINSIGHTS_CONNECTION_STRING`. Azure App Service injects this
  automatically in the deployed environment; no local config sets it.
  **Outside repo / assumption:** whether that variable is actually present
  on the live App Service instance — unconfirmed, not visible from the
  repo.
- **Capture boundary:** `app.UseExceptionHandler("/Home/Error")` +
  `app.UseHsts()` are registered only when `!app.Environment.IsDevelopment()`
  (`Program.cs`, non-Development branch). ASP.NET Core's built-in
  `ExceptionHandlerMiddleware` logs any exception it catches via the
  framework's own `ILogger` before rendering `HomeController.Error()`
  (`Controllers/HomeController.cs`) — this is framework-default behavior,
  not app code, so truly unhandled exceptions that reach the top ARE
  logged. The gap is everything caught **below** that boundary, inside
  controller actions (see Findings).
- **Logging:** only `Logging:LogLevel` in `appsettings.json` /
  `appsettings.Development.json` (Default: Information, Microsoft.AspNetCore:
  Warning) — console/platform logging via the default .NET logging
  provider, no structured sink (Serilog, etc.). No code anywhere wires log
  lines to alerts; that would be a platform/dashboard concern, **outside
  repo**.
- **Scrubbing/sampling:** none found in code — no `TelemetryInitializer`,
  no sampling processor, no PII filter. Nothing degrades events once they
  reach the tracker; the gap is entirely upstream (events never generated).
- **Deploy identity:** `.github/workflows/deploy.yml` builds, tests, and
  publishes the app with no step that writes a commit SHA, build number, or
  version into configuration/environment/`appsettings`. No `InformationalVersion`,
  no custom App Insights cloud role/version tag anywhere in `Program.cs` or
  the `.csproj`.
- **Health check:** `/health` (`Program.cs`) uses
  `AddHealthChecks().AddDbContextCheck<ApplicationDbContext>()`, allows
  anonymous access, and is smoke-tested post-deploy
  (`deploy.yml`'s "Smoke test /health" step) — this is a good, verified
  deploy-time signal, not a gap.

**Assumptions (outside the repo, not findings) — unconfirmed:**
- Application Insights connection string is actually set on the Azure App
  Service instance (`melodybooker-dev-app-k7sqmyvh2szbg`).
- Azure App Service's own platform logging/Event Log captures a crashed
  startup (e.g. if `db.Database.MigrateAsync()` throws before `app.Run()`)
  — `Program.cs` has no try/catch around the startup migration/seeding
  sequence, which is reasonable because a crash-on-startup is exactly the
  kind of failure the migration-safety-gate CI job and the post-deploy
  `/health` smoke test are meant to catch before it reaches users.

## 3. What reaches the tracker (static-only — no runtime proof run this pass)

| Failure shape | Response | Platform logs | Tracker | Verdict |
|---|---|---|---|---|
| `DbUpdateException` on reservation save (double-booking or any other DB cause) — `AdminReservationsController.cs:170`, `TeacherReservationsController.cs:134` | 200, re-rendered form with generic "already booked" error | none | none | **Invisible** — real cause indistinguishable from the expected race |
| `DbUpdateException` creating a Teacher account — `AdminTeachersController.cs:90` | 200, re-rendered form with generic error; compensating account delete attempted | none | none | **Invisible**, plus the compensation's own success/failure is unchecked |
| Compensating `_userManager.DeleteAsync(user)` itself fails — `AdminTeachersController.cs:93` | No visible effect to the admin (already past the 200 above) | none | none | **Invisible** — orphaned account persists silently |
| Orphaned Teacher account later loads `/Teacher` — `TeacherController.cs:28` | 200, empty student roster, looks like "0 students assigned" | none | none | **Invisible** — real data-integrity break looks identical to a normal empty roster |
| Truly unhandled exception reaching the top (any other controller) | 500, generic `Error.cshtml` | yes (framework default `ILogger` call inside `ExceptionHandlerMiddleware`) | yes, *if* App Insights connection string is set in Azure (unconfirmed) | Framework default — acceptable baseline |

## 4. Systemic root causes

1. **Shared "catch `DbUpdateException`, render a generic message, never log" pattern** — `Controllers/AdminReservationsController.cs:170`, `Controllers/TeacherReservationsController.cs:134`, `Controllers/AdminTeachersController.cs:90`. A reasonable local decision (give users a friendly "this is already booked" message instead of a 500) implemented identically in three places, all of which catch the entire `DbUpdateException` type rather than inspecting *which* constraint failed. Any other DB-level failure — a deadlock, a dropped connection, a constraint violation unrelated to the booking race — is silently relabeled as "already booked" with zero diagnostic trail. Explains findings #1, #2, #3.
2. **No controller has an `ILogger<T>` dependency at all.** Confirmed by a repo-wide sweep (`grep -n "catch|LogError|LogWarning" src/melody`): the only `LogWarning`/`LogError` calls in the entire app are in `Data/AdminUserSeeder.cs` (startup-only code), which demonstrates the correct pattern is already known in this codebase — it's just never applied to request-handling code. This is *why* root cause 1 is possible: there's no logger even available to call in those catch blocks today. Explains the same three findings plus the structural gap behind them.
3. **The cleanup compensating for a failed Teacher-profile save is itself unverified** — `Controllers/AdminTeachersController.cs:93`: `await _userManager.DeleteAsync(user);` discards the returned `IdentityResult`. If this delete fails (e.g. a concurrent request, a transient Identity-store error), the orphaned `ApplicationUser` + Teacher role persists with no Teacher row and no signal that cleanup failed. Explains finding #4, and compounds with finding #5 below.
4. **A downstream "graceful degradation" hides the orphaned-account state permanently** — `Controllers/TeacherController.cs:28`: `if (teacher is null) { return View(new List<TeacherStudentListItemViewModel>()); }`. This path exists to be user-friendly (show an empty roster rather than crash), but it makes a genuine data-integrity violation (a Teacher-role account with no linked Teacher row — exactly the state root cause 3 can produce) indistinguishable from "this teacher legitimately has zero students." No log, no distinguishing UI state, nothing. Explains finding #5.
5. **No release/deploy identity reaches the running app** — `.github/workflows/deploy.yml`'s `deploy` job publishes and deploys the app with no step writing a commit SHA/build number into configuration or environment, and no code in `Program.cs`/the `.csproj` reads or sets an application version. Even if Application Insights is fully enabled in Azure, no event it captures could be attributed to a specific deploy. This is a code-level gap (nothing in the repo *could* tag a release even if asked to), not merely a missing dashboard setting. Explains finding #6.

## 5. Findings by area

### Reservation booking (teacher/admin room reservations)

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| 1 | `Controllers/AdminReservationsController.cs:170` | swallowed | **high** | A `DbUpdateException` of any cause during an admin's edit-reservation save returns 200 with "this slot is already booked"; no log, no tracker event. A non-race DB failure (deadlock, timeout) is misreported as a booking conflict and nobody is told the real cause. | Inject `ILogger<AdminReservationsController>`; log the exception (include entity ids) before falling back to the generic message. Consider checking `ex.InnerException`/constraint name to confirm it really is the unique-index race before treating it as expected. |
| 2 | `Controllers/TeacherReservationsController.cs:134` | swallowed | **high** | Same as #1, for a teacher creating a reservation. | Same fix as #1, scoped to this controller. |

### Admin roster management (teacher accounts)

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| 3 | `Controllers/AdminTeachersController.cs:90` | swallowed | **high** | A `DbUpdateException` saving the new Teacher profile returns 200 with a generic error and triggers a compensating account delete; the original exception is never logged. | Inject `ILogger<AdminTeachersController>`; log the exception before the compensating delete. |
| 4 | `Controllers/AdminTeachersController.cs:93` | missing-throw | **critical** | The compensating `_userManager.DeleteAsync(user)`'s `IdentityResult` is discarded. If it fails, an orphaned Teacher-role account with no Teacher profile silently exists — exactly the state the surrounding code comments say must never happen — with zero signal to anyone. | Check the returned `IdentityResult`; on failure, log an Error (with the user id, not raw PII) so an on-call engineer can find and manually fix the orphaned account. |
| 5 | `Controllers/TeacherController.cs:28` | flattened-response | **critical** | Any orphaned Teacher-role account (from finding #4, or any other future cause) renders as an ordinary empty "my students" page — indistinguishable from a real teacher with zero assigned students. No log, no alert, no diagnostic trail; the break can persist indefinitely unnoticed. | Log a Warning (with the `ApplicationUser` id) when `teacher is null` so the condition is at least discoverable in logs, and consider a distinct "account setup incomplete" view state instead of a silent empty list. |

### Platform / plumbing

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| 6 | `.github/workflows/deploy.yml` (`deploy` job) + `Program.cs` (no version/release code) | missing-context | **medium** | Even with Application Insights fully enabled, no event can be tied to the deploy/commit that produced it, making "did the last release cause this?" unanswerable from telemetry alone. | Pass the commit SHA/build version as an app setting during deploy (e.g. an environment variable read in `Program.cs` and set as an App Insights cloud role instance or custom property, or at minimum logged once at startup). |
| 7 | All controllers (repo-wide) | logged-not-captured (structural) | **medium** | No controller can emit a diagnostic log today; adding one requires first wiring `ILogger<T>` into the constructor. This is the structural reason findings #1-#3 exist and will recur for any new controller written the same way. | Add `ILogger<T>` as a constructor parameter to the affected controllers (start with the three doing manual exception handling), and treat it as the default pattern for new controller actions that catch exceptions. |

## 6. Recommended fix order

1. **Findings #1, #2, #3** (same mechanism, 3 locations) — inject `ILogger<T>` into `AdminReservationsController`, `TeacherReservationsController`, `AdminTeachersController` and log the caught `DbUpdateException` before falling back to the generic user message. Closes root causes 1 and 2 for the highest-traffic flow (booking) and removes the most instances of invisibility for the least code.
2. **Finding #4** — check the `IdentityResult` from the compensating `_userManager.DeleteAsync(user)` in `AdminTeachersController.Create` and log on failure. Small, localized change that closes root cause 3, the one most likely to leave permanent silent corruption.
3. **Finding #5** — log a Warning when `TeacherController.Index` finds `teacher is null`. Depends on nothing else; closes root cause 4 and gives a detection path for finding #4 even before it's fixed.
4. **Finding #6** — wire a commit SHA/build version into the deploy and app config. Lower urgency (only matters once telemetry exists to attribute), but it's a one-time, low-effort change that pays off every future audit/incident.

No privacy constraints apply to any of these fixes — none of the logged values (exception details, `ApplicationUser` ids) are raw PII beyond what Identity already stores; avoid logging the password/email fields directly if adding more context later.

## 7. Changes since last audit

N/A — first observability audit for this repository; no previous report exists.

## 8. Method and limits

- **Method:** read directly rather than fanning out to parallel subagents —
  the entire app is 7 controllers plus a handful of `Data/` seeders
  (confirmed via a repo-wide `grep -n "ApplicationInsights|ILogger|_logger|catch\s*\("` sweep
  across `src/melody`), small enough that direct reading covered every
  controller, `Program.cs`, the Identity area, and all three `Data/*Seeder.cs`
  files without sampling. All controllers and both plumbing files involved
  in the findings above were read in full; this doubles as the step 5
  spot-check — every cited `file:line` was read directly, not inferred.
- **Repo-wide sweep counts:** exactly 3 `catch` blocks exist in
  `src/melody` application code (excluding the Identity scaffold's
  `Register.cshtml.cs`, which only has defensive `throw`s for misconfigured
  DI, not exception handling); all 3 catch `DbUpdateException` and all 3 are
  swallowed with no logging. Exactly 2 `LogWarning`/`LogError` call sites
  exist in the whole app, both in `Data/AdminUserSeeder.cs`.
- **Runtime proof:** not run this pass (`--runtime` not requested). All
  findings are static-only; severities assume the monitoring pipeline
  (Application Insights) is on in production, per the skill's rubric.
- **Not verifiable from the repo:** whether
  `APPLICATIONINSIGHTS_CONNECTION_STRING` is actually set on the Azure App
  Service instance, and whether Azure's platform logging captures a crashed
  startup migration. Both are listed as assumptions in Section 2.
- **No probe harness exists yet** — if runtime proof is wanted for findings
  #1-#5, a worktree-isolated run with a local fake-ingest endpoint
  (`references/runtime-probes.md`) would need to be set up first.
