# Repository Guidelines

MelodyBooker is a greenfield ASP.NET Core MVC web app (net10.0) for a single music school: teachers book rooms for students' weekly lessons, and the app enforces that specialist-instrument rooms are prioritized for the students who need them.

## Current state — read before implementing

The repo is still at the default `dotnet new mvc` scaffold stage: no auth, EF Core, booking logic, tests, or CI workflow exist yet — don't assume any are wired up. Consult `@context/foundation/prd.md` for requirements (roles, FR-001..FR-016, business rules) before adding a feature, and `@context/foundation/tech-stack.md` for the stack rationale (SQL Server + EF Core planned, Azure App Service deploy target, GitHub Actions CI planned).

## Domain rules to preserve

- Two roles only: **Administrator** (manages teachers/students/assignments, sees and edits all reservations) and **Teacher** (reserves rooms only for their own assigned students). Students have no login.
- Lesson duration is derived from the student's class, never user input: 20 minutes for classes 1–4, 40 minutes for classes 5–8 (FR-009).
- Bookings repeat weekly for the full school year; double-booking the same room/time slot must always be hard-blocked with no override (FR-008, FR-010).
- Booking a specialist room (piano/xylophone/drums) for a mismatched student is a soft warning only (FR-011). Hard-blocking mismatches (FR-012) is an explicit nice-to-have — do not implement it unless asked.

## Project structure

- `melodybooker.sln` — solution file; the only project is `src/melody/melody.csproj`.
- `src/melody/Controllers`, `src/melody/Models`, `src/melody/Views`, `src/melody/wwwroot` — standard ASP.NET Core MVC layout.
- `src/melody/Program.cs` — minimal hosting model, `AddControllersWithViews()`, conventional routing `{controller=Home}/{action=Index}/{id?}`.
- `context/foundation/` — PRD, tech-stack decision, and shaping notes; treat as the source of truth for product scope instead of re-deriving requirements elsewhere.

## Build and run

- `dotnet build melodybooker.sln` — build the solution.
- `dotnet run --project src/melody/melody.csproj` — run the app.
- No test project exists yet. If you add one, register it in `melodybooker.sln`; run a single test with `dotnet test --filter FullyQualifiedName~ClassName.MethodName`.
- No lint/format config exists; use `dotnet format` if introduced.

## Coding conventions

`Nullable` and `ImplicitUsings` are both enabled in `src/melody/melody.csproj` — write nullable-aware C# and omit `using` statements for BCL namespaces. Controllers follow the stock MVC convention (`XController` in `Controllers/`, matching views under `Views/X/`).

## Commits and PRs

No enforced commit convention exists (`git log` shows short, informal, lowercase messages). No CI gate exists yet to satisfy before merging.
