# Test Plan

> Phased test rollout for this project. Strategy is frozen at the top
> (§1–§5); cookbook patterns at the bottom (§6) fill in as phases ship.
> Read before writing any new test.
>
> Refresh: re-run `/10x-test-plan --refresh` when stale (see §8).
>
> Last updated: 2026-11-04 (Phase 3 researched)

## 1. Strategy

Tests follow three non-negotiable principles for this project:

1. **Cost × signal.** The cheapest test that gives a real signal for the
   risk wins. Do not promote to integration/e2e because it "feels safer."
   Do not add a CI migration-gate check where a unit test already catches
   the regression.
2. **User concerns are first-class evidence.** Risks anchored in "the team
   is worried about X, and the failure would surface somewhere in
   `<area>`" carry the same weight as PRD lines or hot-spot data.
3. **Risks are scenarios, not code locations.** This plan documents *what
   could fail* and *why we believe it's likely* — drawn from documents,
   interview, and codebase *signal* (churn, structure, test base). It does
   NOT claim to know which line owns the failure. That knowledge is
   produced by `/10x-research` during each rollout phase. If the plan and
   research disagree about where the failure lives, research is the
   ground truth.

Hot-spot scope used for likelihood weighting: `src/melody/` (last 30 days;
excludes `wwwroot/`, build output, archive/docs).

## 2. Risk Map

The top failure scenarios this project must protect against, ordered by
risk = impact × likelihood. Risks are failure scenarios in user / business
terms, not test names. The Source column cites the *evidence that surfaced
this risk* — never a specific file as "where the failure lives" (that is
research's job, see §1 principle #3).

| # | Risk (failure scenario) | Impact | Likelihood | Source (evidence — not anchor) |
|---|---|---|---|---|
| 1 | A room/time slot that is already booked gets booked again anyway — including the boundary case where one lesson ends exactly when another starts | High | High | PRD FR-008/FR-010 (hard-block, no override); interview Q1, Q3; hot-spot dir `src/melody/Controllers/` (TeacherReservationsController.cs, 5 commits/30d) |
| 2 *(abuse/IDOR)* | A teacher reaches or mutates another teacher's student/reservation data by ID, or a role-boundary refactor momentarily lets the wrong role reach an admin-only action | High | Medium | Interview Q2, Q4; PRD NFR (minors' data — teacher scoped to own students only) |
| 3 | An EF Core migration behaves differently against the real SQL Server target than it does under the InMemory/SQLite providers the test suite uses | High | Medium | Interview Q2; `infrastructure.md` Risk Register ("EF Core migrations applied during a bad deploy are not automatically reversible") |
| 4 | Reservation-form validation (student class × instrument × room) silently breaks one condition while fixing another | Medium | High | Interview Q2, Q3; hot-spot dir `src/melody/Models/ViewModels/` (ReservationFormViewModel.cs, 4 commits/30d) |
| 5 | Lesson-duration derivation mishandles the class 4/5 boundary or a student with no assigned class | Medium | Medium | PRD FR-009; interview Q4 |

**Impact × Likelihood rubric.**

| Rating | Impact | Likelihood |
|--------|--------|------------|
| High   | user loses access, data, or money; failure is publicly visible | area changes weekly, or we have already been burned here |
| Medium | feature degrades, a workaround exists, only some users affected | touched occasionally, has been a source of bugs |
| Low    | cosmetic, easily reverted, no data effect | stable code, rarely touched |

### Risk Response Guidance

| Risk | What would prove protection | Must challenge | Context `/10x-research` must ground | Likely cheapest layer | Anti-pattern to avoid |
|------|-----------------------------|----------------|--------------------------------------|-----------------------|-----------------------|
| #1 | An overlapping room/time booking — including end=start boundary — is always rejected, and the database never ends up holding two overlapping confirmed reservations for the same room | "no exception thrown" does not mean "no overlap was persisted" — verify DB state, not just the HTTP response | Boundary semantics (inclusive/exclusive), weekly-recurrence modeling, concurrency safety on near-simultaneous booking attempts | unit (overlap comparison) + integration (persisted state via existing WebApplicationFactory/Sqlite harness) | Asserting with the same comparison expression copied from the implementation (oracle problem) |
| #2 | A request for another teacher's student/reservation, made directly at the endpoint with a different owner's ID, is always denied (403/404); admin-only actions are unreachable to a teacher-role request regardless of UI state | "the UI hides the button" is not proof the server rejects the request | How ownership is checked today (claims vs DB lookup), whether it's enforced per-action or only via shared query scoping | integration (call controller actions directly with a different teacher's resource ID) | Testing only the "own student" happy path and treating absence of an error as proof of correct scoping |
| #3 | A migration applied against the real target provider (SQL Server) produces the same schema/constraints the test suite assumes under InMemory/SQLite | Green tests on InMemory/SQLite do not prove the migration is safe on SQL Server | Which EF Core features in use differ by provider; whether a migration dry-run/validation step exists before deploy | process/CI-gate (migration dry-run against a disposable SQL Server/Azure SQL instance), not a classic unit test | Relying solely on InMemory-provider test runs as proof a migration is production-safe |
| #4 | Every student-class × instrument × room-type combination yields the correct computed (duration, hard-block, soft-warning) per FR-009/FR-011, and changing one rule doesn't silently alter another's output | "no validation error shown" does not mean "the correct value was computed/persisted" | Whether duration-derivation and specialist-instrument-warning logic share code or are only adjacent in the form | unit (viewmodel/validation logic directly) | One large integration test that confirms "it works" without pinpointing which field combination regressed |
| #5 | Every class 1–8 maps to the correct duration (20 vs 40 min); a student with no assigned class produces an explicit validation error, not a silent default | Testing only one representative class per duration bucket does not cover the 4/5 boundary or the null-class case | Where the class→duration mapping lives; current behavior when `StudentClass` is null/unset | unit (pure mapping function) | Covering one class value per duration bucket and skipping the boundary |

## 3. Phased Rollout

Each row is a discrete rollout phase that will open its own change folder
via `/10x-new`. Status moves left-to-right through the values below; the
orchestrator updates Status as artifacts appear on disk.

| # | Phase name | Goal (one line) | Risks covered | Test types | Status | Change folder |
|---|---|---|---|---|---|---|
| 1 | Booking-conflict correctness | Prove the double-booking hard-block holds at boundaries and in persisted DB state, not just the HTTP response | #1 | unit + integration | complete | `context/changes/testing-booking-conflict-correctness/` |
| 2 | Authorization & ownership boundaries | Prove role and ownership checks hold at the endpoint, independent of UI | #2 | integration | complete | `context/changes/testing-authorization-ownership-boundaries/` |
| 3 | Reservation form & duration validation hardening | Cover cross-field validation and lesson-duration edge cases | #4, #5 | unit | researched | `context/changes/testing-reservation-form-duration-validation/` |
| 4 | Deployment & migration safety gate | Close the gap between InMemory/SQLite test behavior and real SQL Server migration behavior | #3 | process/CI-gate | not started | — |

**Status vocabulary** (fixed — parser literals): `not started` → `change opened` → `researched` → `planned` → `implementing` → `complete`.

## 4. Stack

| Layer | Tool | Version | Notes |
|---|---|---|---|
| unit + integration | xUnit + `Microsoft.AspNetCore.Mvc.Testing` | xunit 2.9.3, Mvc.Testing 10.0.12 | `WebApplicationFactory`-based integration tests already in use across 12 test files |
| test data providers | EF Core InMemory + Sqlite | 10.0.12 | Used interchangeably across existing tests; provider-drift risk tracked as Risk #3 |
| e2e | none yet — see §3 Phase 2 for endpoint-level coverage; no browser/UI e2e planned | n/a | Not justified by cost × signal for this MVP; UI snapshot testing explicitly excluded (§7) |
| lint/format | none configured | n/a | Repo has no `dotnet format` or analyzer gate wired into CI |
| (optional) AI-native | none | n/a | No AI-native review tool available/justified this session |

**Stack grounding tools (current session):**
- Docs: none available (no Context7/framework-docs MCP exposed in this session) — relied on local manifests (`melody.Tests.csproj`) and `infrastructure.md`; checked: 2026-11-02
- Search: `web_search` available — not invoked, no stack-version ambiguity arose; checked: 2026-11-02
- Runtime/browser: none available — no Playwright/browser MCP exposed in this session; checked: 2026-11-02
- Provider/platform: GitHub MCP server available (deferred) — not used this session; project runs GitHub Actions (`.github/workflows/deploy.yml`), not Azure DevOps Pipelines, so the also-available Azure DevOps (PSE) MCP is not relevant; checked: 2026-11-02

## 5. Quality Gates

| Gate | Where | Required? | Catches |
|---|---|---|---|
| build | CI (`.github/workflows/deploy.yml`) on PR + push to main | required | compile errors |
| unit + integration (existing 12-file suite) | CI on PR + push to main | required | logic regressions in already-shipped slices |
| booking-conflict boundary tests | CI on PR, after §3 Phase 1 lands | required after §3 Phase 1 | overlap/double-booking regressions |
| authorization/ownership integration tests | CI on PR, after §3 Phase 2 lands | required after §3 Phase 2 | IDOR / role-scoping regressions |
| reservation-form validation unit tests | CI on PR, after §3 Phase 3 lands | required after §3 Phase 3 | cross-field validation regressions |
| migration dry-run against SQL Server | deploy pipeline, after §3 Phase 4 lands | required after §3 Phase 4 | provider-specific migration failures before they hit production data |
| pre-prod smoke (`/health`) | between merge and prod (`deploy.yml`) | wired already | environment-specific deploy failures |

## 6. Cookbook Patterns

How to add new tests in this project. Each sub-section is filled in once
the relevant rollout phase ships; before that, the sub-section reads
"TBD — see §3 Phase N."

### 6.1 Adding a unit test
- TBD — see §3 Phase 1 (overlap-detection boundary pattern) and Phase 3 (viewmodel/validation pattern).

### 6.2 Adding an integration test
- TBD — see §3 Phase 1 (persisted-state verification pattern) and Phase 2 (cross-owner authorization pattern).

### 6.3 Adding an e2e test
- Not planned — see §4 and §7; no e2e layer justified for this MVP.

### 6.4 Adding a test for a new reservation/booking endpoint
- TBD — see §3 Phase 1 and Phase 2 for the reference patterns once shipped.

### 6.5 Adding a migration safety check
- TBD — see §3 Phase 4.

### 6.6 Per-rollout-phase notes
(Filled in by `/10x-implement` as each phase ships.)

## 7. What We Deliberately Don't Test

- **Static/informational pages (Home/Privacy, etc.)** — low impact, no data at stake. Re-evaluate if these pages start carrying business logic. (Source: Phase 2 interview Q5.)
- **Pixel/CSS appearance of admin panels (UI snapshot tests)** — brittle, breaks on routine style changes, low signal. Re-evaluate if a visual regression causes a real incident. (Source: Phase 2 interview Q5.)
- **Developer seed data (`AdminUserSeeder`, etc.)** — one-time dev-environment code, not production logic. Re-evaluate if seeding logic starts running in production paths. (Source: Phase 2 interview Q5.)

## 8. Freshness Ledger

- Strategy (§1–§5) last reviewed: 2026-11-02
- Stack versions last verified: 2026-11-02
- AI-native tool references last verified: 2026-11-02 (none in use)

Refresh (`/10x-test-plan --refresh`) when:

- a new top-3 risk surfaces from the roadmap or archive,
- a recommended tool's `checked:` date is older than three months,
- the project's tech stack changes (new framework, new test runner),
- §7 negative-space no longer matches what the team believes.
