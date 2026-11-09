# Artifact 3 — Kontrybutorzy (git)

> **Ograniczenie wprost:** to repo ma **jednego autora commitów przez całą historię**
> (`lukaszzyglowicz <luka103@gmail.com>` — zweryfikowano `git log --format="%an <%ae>" | sort -u`,
> zero innych tożsamości, zero botów/automatyzacji do odfiltrowania). Agent AI (Copilot/skille
> `/10x-*`) współtworzył większość commitów wykonawczych, ale człowiek zawsze prowadził zmianę
> (zatwierdzał plany, commitował ręcznie) — zgodnie z zasadą promptu, commit z człowiekiem jako
> autorem i agentem jako współautorem liczy się jako praca człowieka. Przy zespole
> jednoosobowym skupienie całej wiedzy w jednej głowie **to norma, nie ryzyko** — poniższa tabela
> nie wskazuje więc "kogo zapytać zamiast X", tylko które obszary wymagają najwięcej kontekstu od
> tej jednej osoby, zanim ktokolwiek inny dołączy.

## Top 5 obszarów (z artifact-1 + artifact-2)

| # | Obszar | Dlaczego trafił na listę |
|---|---|---|
| 1 | `Program.cs` (DI/Identity/cookies/authz/telemetry/routing+seeding) | artifact-2: "fałszywy hub" — 6 niepowiązanych odpowiedzialności w jednym pliku; artifact-1: 8 commitów, w tym 2 fix-y związane z rolami/auth |
| 2 | `AdminReservationsController` + `TeacherReservationsController` (reguła FR-010/FR-011) | artifact-2: realne sprzężenie przez wspólną funkcję statyczną `Reservation.Overlaps`/`IsInstrumentMismatch`, wywoływaną niezależnie w obu plikach — zmiana w jednym wymaga ręcznej synchronizacji z drugim |
| 3 | `Data/ApplicationDbContext.cs` + seederzy (`RoleSeeder`, `AdminUserSeeder`, `RoomSeeder`) | artifact-2: najwyższy fan-in w repo (10 plików), brak warstwy repozytorium — każda zmiana schematu/seedingu przechodzi przez tę samą osobę |
| 4 | `Views/AdminStudents` + roster administratora (`AdminTeachersController`, `AdminStudentsController`) | artifact-1: najaktywniejszy podobszar widoków (12 commitów), rdzeń funkcji roster |
| 5 | Harness testowy i jakość (`tests/melody.Tests/AgentHooksTests.cs`, `.github/hooks`, `.github/workflows`, `FaultInjectingWebApplicationFactory`) | artifact-1: jedyny obszar z realnie "psującym się" testem (3 fix-y na `AgentHooksTests.cs`, w tym jeden złapany na żywo w tej sesji) + cała fala hardeningu z tygodnia W45 |

## Linia wsparcia per obszar

Dla wszystkich 5 obszarów kontrybutor jest ten sam: **lukaszzyglowicz** — jedyny autor w historii
repo. Poniżej, zamiast "kogo zapytać", rozbicie **jakiego rodzaju kontekstu** ta osoba będzie
potrzebować przypomnieć sobie przy każdym obszarze (bo nawet zespół jednoosobowy korzysta z takiej
mapy, żeby wiedzieć gdzie szukać uzasadnień decyzji sprzed tygodni):

| Obszar | Rodzaj wiedzy do odtworzenia | Gdzie szukać uzasadnienia w repo |
|---|---|---|
| `Program.cs` | Dlaczego seeding/migracje są warunkowane środowiskiem `Testing` (SQLite vs SQL Server); dlaczego App Insights jest opcjonalne | `context/changes/role-based-authorization/plan.md`, `context/changes/testing-deployment-migration-safety-gate/plan.md` |
| `Reservation.Overlaps` / `IsInstrumentMismatch` w dwóch kontrolerach | Który z FR-010 (hard-block) vs FR-011 (soft warning) obowiązuje gdzie; że to świadoma decyzja, nie przeoczenie duplikacji | `context/foundation/prd.md` (FR-008/010/011/012), `context/archive/2026-10-19-teacher-books-room/plan.md`, `context/archive/2026-10-20-specialist-instrument-warning/plan.md` |
| `ApplicationDbContext` + seederzy | Kolejność seedowania (role → admin user → pokoje), dlaczego `EnsureCreatedAsync` zamiast `MigrateAsync` w testach | `context/changes/testing-deployment-migration-safety-gate/plan.md`, komentarze w `Program.cs` |
| Roster administratora (`AdminStudents`/`AdminTeachers`) | Dlaczego usuwanie nauczyciela z przypisanymi uczniami jest blokowane; wzorzec partiala `_StudentFormFields` | `context/archive/2026-10-09-admin-roster-management/plan.md`, `context/changes/admin-students-ui-polish/plan.md` |
| Harness testowy / hooki jakości | Dlaczego `AgentHooksTests.cs` bywa flaky w CI (brak `dotnet` w PATH w pewnych środowiskach), mechanizm `ThrowingApplicationDbContext`/`AsyncLocal`-nie-działa-w-TestServer | `context/changes/log-swallowed-db-exceptions/plan.md`, `.github/hooks/*.js`, ta sesja (CI log z `CheckEditedFile_DotnetMissingFromPath...`) |

## Ograniczenia

- Brak drugiego kontrybutora oznacza, że ta mapa nie może wskazać realnego "bus factor" poza
  oczywistym (=1) — każda rekomendacja typu "zapytaj X" sprowadza się do "sprawdź odpowiedni
  `plan.md` w `context/changes/`", bo to jedyny ślad decyzji poza głową autora.
- Udział agenta AI w poszczególnych commitach nie jest wiarygodnie policzalny (patrz ograniczenie w
  artifact-1 — trailer `Co-authored-by: Copilot` pojawia się dopiero w części historii), więc nie
  rozróżniono tu "commitów człowieka" od "commitów przy wsparciu agenta" ilościowo.
- Ta mapa nie mówi nic o tym, co się stanie, gdy dołączy druga osoba — ocenia tylko stan obecny
  (jeden kontrybutor, cała wiedza nieudokumentowana poza plikami `context/changes/*/plan.md`).
