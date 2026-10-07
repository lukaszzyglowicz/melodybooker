# Role-Based Authorization & Seeded Administrator Account — Plan Brief

> Full plan: `context/changes/role-based-authorization/plan.md`

## What & Why

Make the PRD's two roles (Administrator, Teacher) actually enforced instead of just seeded as empty role rows: lock every endpoint down by default, seed a real administrator account from configuration, and close the public self-registration hole left open by ASP.NET Core Identity's default UI. This is roadmap item **F-01**, a foundation slice that unlocks S-01, S-02, S-03, and S-05 — none of them can be verified as correctly scoped until the role boundary is real.

## Starting Point

Identity + EF Core are already wired up (`ApplicationUser : IdentityUser`, `ApplicationDbContext : IdentityDbContext<ApplicationUser>`), domain models (Teacher/Student/Room/Reservation) and an initial migration exist, and `RoleSeeder` already creates the `Administrator`/`Teacher` `IdentityRole` rows at startup. But: no controller has `[Authorize]`, no administrator **user** account exists, and `AddDefaultUI()`'s public Register page is reachable by anyone — contradicting the PRD's "no public sign-up" Access Control rule.

## Desired End State

Every endpoint requires login by default. An administrator can log in on day one using credentials supplied via configuration (no UI needed to create the first account). Public self-registration is closed — only a logged-in Administrator can reach the Register page. Logging in as Administrator lands on `/Admin`; logging in as Teacher lands on `/Teacher`; each role is blocked (403) from the other's area. A new test project proves all of this and gives later slices a pattern to extend.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Admin credential source | Config (`Admin:Email`/`Admin:Password`) with env-var override | Matches the Azure App Service Application Settings pattern already documented in `infrastructure.md`; no secrets committed | Plan |
| Default authorization policy | Global fallback-deny (`RequireAuthenticatedUser`) | Secure by default for every future controller added by S-01..S-05, not just the ones touched today | Plan |
| Public self-registration | Scaffold only the `Register` page, replace `[AllowAnonymous]` with `[Authorize(Roles="Administrator")]` | `AllowAnonymous` metadata on the Identity RCL page overrides any routing-convention-based policy — scaffolding is the only way to actually remove it | Plan |
| Role-boundary demonstration | Minimal `AdminController`/`TeacherController` landing stubs | Makes "every controller enforces the correct role" visually checkable in a browser today, and gives S-01/S-02 a controller to extend | Plan |
| Testing approach | New xUnit + `WebApplicationFactory` test project | No test project exists yet; this change is the right place to establish the pattern every later slice will reuse for its own auth checks | Plan |

## Scope

**In scope:**
- Fallback-deny authorization policy across the app
- `AdminUserSeeder` creating one idempotent Administrator user from configuration
- Locking down `/Identity/Account/Register` to the Administrator role
- Minimal `/Admin` and `/Teacher` role-gated landing controllers + role-based post-login redirect
- New `tests/melody.Tests` project with integration + unit coverage of the above

**Out of scope:**
- Admin roster management UI (add/edit teachers & students) — S-01
- Teacher's real student list / booking screens — S-02, S-03, S-04
- Admin reservation oversight — S-05
- Hard-blocking specialist-room mismatches (FR-012, nice-to-have)
- Email confirmation, 2FA, custom password policy, deployment/CI wiring (F-02)

## Architecture / Approach

Lock down first, then grant back exactly what's needed: a global fallback policy denies anonymous access everywhere except Identity's own login-flow pages (which keep their existing `AllowAnonymous` metadata); an idempotent startup seeder creates the one real account the system needs today; the self-registration page is scaffolded out of the RCL so its `AllowAnonymous` can be removed; and two near-empty, role-gated controllers give each role a landing page that proves the boundary works, ahead of later slices building real pages on top of them.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Global authorization policy | Fallback-deny policy + `[Authorize]` on `HomeController` | Misconfiguring the fallback policy could also lock out Identity's own login pages if their `AllowAnonymous` metadata is mishandled |
| 2. Seeded administrator account | `AdminUserSeeder`, config-driven, idempotent | Must run after `RoleSeeder` in startup ordering, or role assignment fails |
| 3. Lock down public self-registration | Scaffolded, role-restricted `Register` page; nav link hidden from anonymous users | Requires the `dotnet-aspnet-codegenerator` tool; scaffolding must target only the one page, not the whole Identity area |
| 4. Role-scoped landing stubs | `/Admin`, `/Teacher` controllers + post-login redirect | Stub controllers will be superseded by S-01/S-02 shortly after |
| 5. Automated verification | New `tests/melody.Tests` project (xUnit + `WebApplicationFactory`) | First test project in the repo — in-memory DB substitution for `ApplicationDbContext` must not require a real SQL Server instance |

**Prerequisites:** `dotnet-aspnet-codegenerator` global tool for Phase 3's scaffolding step; access to set `Admin:Email`/`Admin:Password` via `dotnet user-secrets` locally.
**Estimated effort:** ~2-3 sessions across 5 phases, consistent with the 3-week after-hours MVP timeline.

## Open Risks & Assumptions

- Assumes `dotnet-aspnet-codegenerator` can be installed/run in the dev environment for the one-page Register scaffold; if not, a manual middleware-based 404/redirect for that route is the fallback.
- Assumes a real SQL Server connection is not required for the new test project (EF Core in-memory provider substitution) — if the team later wants tests to run against real SQL Server semantics (e.g. the unique index from FR-010), that's a follow-up decision for S-03's plan, not this one.
- Teacher test accounts for manual verification must be created directly via `UserManager` since no admin UI exists yet (S-01 scope) — documented in Testing Strategy, not blocking.

## Success Criteria (Summary)

- An administrator can log in on a fresh environment without any public sign-up flow, using config-supplied credentials.
- No anonymous or wrong-role user can reach `/Identity/Account/Register`, `/Admin`, or `/Teacher`.
- `dotnet test` passes, proving the role boundary automatically for every future slice to build on.
