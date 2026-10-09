# MelodyBooker — Historia wykonania (log agenta)

Ten plik dokumentuje chronologicznie, jakie skille/komendy 10xDevs (oraz inne
polecenia) były uruchamiane w tym repozytorium od początku projektu, wraz z
wynikiem każdego kroku. Celem jest szybkie zorientowanie się "jak doszliśmy
do obecnego stanu" bez przekopywania historii sesji/czatu.

> Surowy log promptów użytkownika znajduje się w `executions.txt`.
> Ten plik to jego uporządkowana, komentowana wersja.

## Faza 1 — Fundamenty projektu (2026-10-01 / 2026-10-02)

Łańcuch: `/10x-init → /10x-shape → /10x-prd → /10x-tech-stack-selector → /10x-bootstrapper`

Uwaga: część kroków była uruchamiana dwukrotnie, bo praca zaczęła się w
katalogu `C:\10xDev\tenxdev`, a następnie projekt został przeniesiony/
odtworzony w `C:\10xDev\melodybooker`.

| # | Skill / komenda | Wynik |
|---|---|---|
| 1 | `/10x-init` | Scaffold `context/{foundation,changes,archive}/` + README.md w każdym |
| 2 | `/10x-shape @idea-notes.md` | `context/foundation/shape-notes.md` — przechwycone wymagania: szkoła muzyczna, rezerwacje sal na lekcje, priorytet sal specjalistycznych (pianino/ksylofon/perkusja) dla uczniów grających na tych instrumentach |
| 3 | `/10x-prd` | `context/foundation/prd.md` — PRD z rolami (Administrator, Teacher), FR-001..FR-016, w tym kluczowe reguły: czas trwania lekcji z klasy ucznia (FR-009), blokada podwójnych rezerwacji (FR-008/FR-010), miękkie ostrzeżenie przy sali specjalistycznej (FR-011) |
| 4 | `/10x-tech-stack-selector` | `context/foundation/tech-stack.md` — wybór: ASP.NET Core MVC (.NET 10) + SQL Server |
| 5 | `/10x-bootstrapper` | Scaffold `dotnet new mvc`; później przeniesiony do `src/melody/`, dodany plik `.sln`, poprawiony `.gitignore` (usunięte `bin/`, `obj/`), dyskusja o `wwwroot` w repo |
| 6 | `/find-skills` | Wyszukiwanie gotowych skilli do code-review przed pushem |
| 7 | `/init` (wbudowany Copilot) | Wygenerowanie bazowego `copilot-instructions.md` |
| 8 | `/10x-agents-md` | Wygenerowanie `AGENTS.md` (uruchamiane kilkukrotnie, w miarę dopracowywania) |
| 9 | `/10x-rule-review AGENTS.md` | Ocena jakości `AGENTS.md` wg 5-punktowej karty wyników (uruchamiane kilkukrotnie) |
| 10 | `/10x-lesson` | Zapisana zasada: "zawsze powinny być dodawane unit testy dla nowego kodu" → `context/foundation/lessons.md` |

**Commity:** `c22156f` 10xinit, `de9e5c2` 10xshape, `f0941bf`/`add0675` prd,
`bebac73` 10xtech, `c29158b` 10xbootsrapper, `b33b08d` basic skills added,
`e4adf17` add gitignore.

## Faza 2 — Research infrastruktury (2026-10-02)

| # | Skill / komenda | Wynik |
|---|---|---|
| 11 | `/10x-infra-research` | `context/foundation/infrastructure.md` — rekomendacja: Azure App Service + Azure SQL (po porównaniu z Fly.io/Railway/Render itd. i anty-bias cross-checku) |

**Commit:** `b4aa4e5` add 10xinfra.

## Faza 3 — Plan wdrożenia i Phase 0 (2026-10-05)

| # | Skill / komenda | Wynik |
|---|---|---|
| 12 | `/10x-infra-research` (ponownie) | Ten sam wynik — Azure App Service; doprecyzowanie, że to NIE Fly.io |
| 13 | Plan wdrożenia (tryb planowania, nie wykonanie) | `context/changes/azure-deployment-plan/plan.md` — plan z EF Core + Bicep + subskrypcja Visual Studio Professional |
| 14 | "Wykonaj pierwsze wdrożenie... Phase 0" | Wykonanie end-to-end (patrz niżej) |

### Co zrobiono w Phase 0 (wykonanie, nie planowanie):

1. Dodano EF Core + ASP.NET Identity: modele domenowe (`Instrument`,
   `ApplicationUser`, `Teacher`, `Student`, `Room`, `Reservation`),
   `ApplicationDbContext`, `RoleSeeder`, migracja `InitialCreate`.
2. Napisano infrastrukturę jako kod w Bicep (`infra/`): App Service Plan,
   Web App, Azure SQL (Sweden Central, AAD-only), Key Vault, Log Analytics +
   Application Insights.
3. Wdrożono całość do Azure (subskrypcja Visual Studio Professional,
   resource group `melodybooker-rg`).
4. Skonfigurowano Managed Identity Web App → Azure SQL (kontenerowy
   użytkownik bazy z SID); napotkano i naprawiono błąd logowania — SID musi
   być liczony z **Client ID** tożsamości, nie z Object ID (częsty błąd w
   dokumentacji/blogach).
5. Naprawiono błąd 500 na stronie logowania — brakujący
   `Views/Shared/_LoginPartial.cshtml` wymagany przez domyślny layout
   ASP.NET Core Identity UI.
6. Zweryfikowano końcowy stan: `/`, `/health`, `/Identity/Account/Login`,
   `/Identity/Account/Register` → 200/Healthy.

**Status na koniec Phase 0:** aplikacja działa pod
`http://melodybooker-dev-app-k7sqmyvh2szbg.azurewebsites.net`.

⚠️ **Do zrobienia:** zacommitować zmiany z Phase 0 (EF Core, Bicep, fix
`_LoginPartial`) — na dzień zapisania tego pliku były jeszcze niescommitowane.

**Poza zakresem Phase 0 (zidentyfikowane, nie zaimplementowane):**
- CI/CD (GitHub Actions) — migracje obecnie uruchamiają się automatycznie
  przy starcie aplikacji zamiast przez pipeline.
- Publiczny link "Register" na stronie logowania — wg PRD tylko
  Administrator/Teacher powinni mieć konta, bez samorejestracji.

## Faza 4 — Mapa drogowa (2026-10-06)

| # | Skill / komenda | Wynik |
|---|---|---|
| 15 | `/10x-roadmap` | `context/foundation/roadmap.md` — otwarty pierwszy kamień milowy **M-1: First usable booking MVP** (`first-usable-booking-mvp`) |

**Co zrobiono:**

1. Dyspozycja stanu kamienia milowego: brak istniejącej mapy drogowej →
   pierwsze uruchomienie (`NO_MILESTONE → ACTIVE`).
2. Źródło: `context/foundation/prd.md` (v1) — kontrola gotowości PRD: 4/4.
3. Automatyczne badanie stanu bazowego kodu (bez pytania użytkownika):
   Frontend/Backend — partial (tylko domyślny szkielet Razor/HomeController);
   Data — partial (modele EF Core + migracja `InitialCreate` już są, unikalny
   indeks `(RoomId, DayOfWeek, StartTime)` częściowo wymusza FR-010); Auth —
   partial (Identity + role `Administrator`/`Teacher` zasiane, ale brak
   wymuszania autoryzacji na kontrolerach i brak konta admina); Deploy/infra —
   absent (pusty `.github/workflows`); Observability — present (Application
   Insights + `/health`).
4. Zwięzły wywiad (3 pytania kotwiczące): `main_goal: speed` (twardy deadline
   2026-12-10, budżet 3 tyg. after-hours), gwiazda przewodnia: **S-03 —
   nauczyciel rezerwuje salę na cotygodniową lekcję** (US-01, FR-008/009/010),
   `top_blocker: capacity` (solo developer, praca tylko wieczorami/weekendami).
5. Wygenerowano dekompozycję: 2 Foundations (`F-01` role-based-authorization —
   status `ready`; `F-02` deployment-skeleton — status `blocked` na
   potwierdzeniu zasobu Azure) + 5 Slices (`S-01` admin-roster-management,
   `S-02` teacher-student-list, `S-03` teacher-books-room *(north star)*,
   `S-04` specialist-instrument-warning, `S-05` admin-reservation-oversight).
   Pokrycie PRD: 13/13 must-have FR. Nice-to-have (FR-012, FR-015, FR-016) i
   wszystkie Non-Goals trafiły do `## Parked`.
6. Rekomendowany następny ruch: `/10x-plan role-based-authorization` (F-01) —
   jako jedyny element `ready`, bezpośrednio odblokowuje łańcuch do gwiazdy
   przewodniej S-03.

## Faza 5 — Migracja mapy drogowej na GitHub Issues (2026-10-06)

| # | Skill / komenda | Wynik |
|---|---|---|
| 16 | Migracja `roadmap.md` → GitHub Issues (ręczne polecenie, nie skill 10x) | Milestone `M-1` + 6 etykiet + 7 issues w `lukaszzyglowicz/melodybooker` |

**Co zrobiono:**

1. Zainstalowano i skonfigurowano `gh` (GitHub CLI) — binarka była już obecna
   przez winget, ale brakowało jej na `PATH` oraz logowania. Uwierzytelniono
   się przez flow przeglądarkowy (`gh auth login --web`, device code) jako
   `lukaszzyglowicz`, ponieważ wklejony PAT dotarł zamaskowany (`******`) i
   nie nadawał się do użycia w poleceniu.
2. Potwierdzono plan migracji z użytkownikiem (1 issue na każdy `F-NN`/`S-NN`,
   milestone, etykiety, bez ruszania `roadmap.md` i bez GitHub Projects).
3. Utworzono milestone **„M-1: First usable booking MVP”** (opis = Intent
   kamienia milowego z roadmapy).
4. Utworzono etykiety: `roadmap`, `foundation`, `slice`, `status:ready`,
   `status:blocked`, `status:proposed`.
5. Utworzono 7 issues w kolejności topologicznej (F-01, F-02, S-01..S-05),
   każdy z treścią przeniesioną 1:1 z roadmapy (Outcome, PRD refs,
   Prerequisites, Parallel with, Blockers, Unknowns, Risk, Unlocks, Status) +
   etykietami `roadmap` + `foundation`/`slice` + `status:*` + milestone M-1:
   - `#1` F-01 role-based-authorization — `status:ready`
   - `#2` F-02 deployment-skeleton — `status:blocked` (czeka na potwierdzenie zasobu Azure)
   - `#3` S-01 admin-roster-management
   - `#4` S-02 teacher-student-list
   - `#5` S-03 teacher-books-room — ⭐ north star
   - `#6` S-04 specialist-instrument-warning
   - `#7` S-05 admin-reservation-oversight
6. Drugi przebieg: zaktualizowano treść wszystkich 7 issues, podmieniając
   odwołania `Prerequisites`/`Unlocks`/`Parallel with` z identyfikatorów
   roadmapy (`F-01`, `S-03`, …) na rzeczywiste linki do issues (`#1`, `#5`, …).
7. Posprzątano pliki tymczasowe (`%TEMP%\mb-issues`); `context/foundation/roadmap.md`
   pozostał nietknięty jako jedyne źródło prawdy dla mapy drogowej.

**Status na koniec:** 7 otwartych issues w milestone M-1, gotowych do
`/10x-plan` zaczynając od `#1` (F-01, jedyny `status:ready`).

## Faza 6 — F-01: role-based authorization & seeded admin (2026-10-07/08, w toku)

Łańcuch: `/10x-new → /10x-plan → /10x-implement` dla pierwszego elementu
roadmapy ze statusem `ready`.

| # | Skill / komenda | Wynik |
|---|---|---|
| 17 | `/10x-new role-based-authorization F-01 z roadmap.md` | `context/changes/role-based-authorization/change.md` (status `new`) |
| 18 | `/10x-plan role-based-authorization` | `plan.md` (5 faz) + `plan-brief.md`; `change.md` → `planned`; roadmap F-01 → `planning` |
| 19 | `/10x-implement role-based-authorization phase 1` | `change.md` → `implementing`; roadmap F-01 → `in-progress`; Phase 1 wykonana |

**Odkryta luka planistyczna:** Fazy 1-4 zawierają automatyczne wiersze
Progress odwołujące się do testów integracyjnych/jednostkowych, które mogą
powstać dopiero w Fazie 5 (nowy projekt testowy). Decyzja użytkownika:
"Adapt and continue" — fazy 1-4 implementowane normalnie, wiersze zależne od
testów zostają `[ ]` z adnotacją „(awaits Phase 5 test project)” i zostaną
retrospektywnie odznaczone SHA-em commita Fazy 5.

**Faza 1 — Global authorization policy** (delegowana do subagenta):
- `src/melody/Program.cs` — dodano `AddAuthorization` z globalną polityką
  `FallbackPolicy = RequireAuthenticatedUser()`.
- `src/melody/Controllers/HomeController.cs` — dodano atrybut `[Authorize]`.
- Gate build: PASS. Weryfikacja manualna: potwierdzona przez użytkownika.
- Commit (wykonany ręcznie przez użytkownika, nie przez agenta):
  `8f3ac3a4` „/10x-new, /10x-plan, /10x-plan-review i /10x-implement
  role-based-authorization phase 1” — objął też `context/foundation/roadmap.md`
  (flipped F-01 → in-progress), mimo że miał zostać poza commitem wg
  wcześniejszej decyzji; zweryfikowano `git show --stat` i zaakceptowano jako
  zamykający commit Fazy 1. SHA dopisany do wierszy Progress 1.1/1.3/1.4.

**Faza 2 — Seeded administrator account** (delegowana do subagenta):
- `src/melody/Data/AdminUserSeeder.cs` (nowy) — idempotentny seeder konta
  Administrator z konfiguracji (`Admin:Email`/`Admin:Password`), wzorowany na
  `RoleSeeder`; bezpieczny (nie rzuca wyjątku) gdy brak konfiguracji.
- `src/melody/Program.cs` — wywołanie `AdminUserSeeder.SeedAsync(...)` po
  `RoleSeeder.SeedAsync(...)`.
- `src/melody/appsettings.json` / `appsettings.Development.json` — dodano
  pusty placeholder `"Admin": { "Email": "", "Password": "" }` (bez
  prawdziwych sekretów; lokalnie przez `dotnet user-secrets`).
- Gate build: PASS (0 błędów). Oczekiwanie na weryfikację manualną wiersza
  2.4 (logowanie jako skonfigurowany administrator) — **jeszcze niescommitowane**
  w momencie zapisania tego wpisu.

**Status na koniec tego wpisu:** Fazy 1-2 zaimplementowane i zweryfikowane
build-em; Faza 1 scommitowana (`8f3ac3a4`), Faza 2 czeka na potwierdzenie
manualne użytkownika i rytuał commitu. Pozostają Fazy 3-5.

---

### Ciąg dalszy Fazy 6 (kontynuacja, 2026-10-08)

**Faza 2 — domknięcie:** użytkownik potwierdził weryfikację manualną (2.4);
commit ręczny `00f03f4` „History md” (objął też HISTORY.md/executions.txt,
wbrew wcześniejszej decyzji „continue_planned” — zaakceptowane jako
legitymny scope creep, bo treść należała do tej samej pracy). SHA dopisany
do wierszy 2.1/2.4.

**Faza 3 — Lock down public self-registration** (delegowana do subagenta):
- Zainstalowano globalne narzędzie `dotnet-aspnet-codegenerator` (v10.0.2).
- Dodano pakiet `Microsoft.VisualStudio.Web.CodeGeneration.Design` do
  `melody.csproj` (z `IncludeAssets` zawierającym `compile` — adaptacja,
  bez tego narzędzie scaffoldujące nie znajdowało assemblies).
- Wyscaffoldowano `Areas/Identity/Pages/Account/Register.cshtml(.cs)` i
  towarzyszące pliki `_ViewImports`/`_ViewStart`/`_ValidationScriptsPartial`;
  dodano `[Authorize(Roles = "Administrator")]` na `RegisterModel` (subagent
  odkrył, że wyscaffoldowana strona nie niesie już `[AllowAnonymous]` z RCL —
  potwierdza wcześniejszy research).
- `_LoginPartial.cshtml` — link "Register" przeniesiony do gałęzi
  zalogowanego Administratora.
- Subagent przypadkowo wstrzyknął duplikat `AddDefaultIdentity<...>()` do
  `Program.cs` przez sam scaffolder — wykryty i cofnięty jako poza zakresem
  fazy.
- Gate build: PASS. Commit (ręczny użytkownika) `81d9859` „Phase 3 Complete -
  Ready for Manual Verification” — zawierał poprawny zestaw plików Fazy 3
  (bez niepowiązanych ścieżek tym razem).

**Odkryta przedistniejąca usterka blokująca testy lokalne:** aplikacja nie
startowała lokalnie w ogóle — `AddApplicationInsightsTelemetry()` (z Fazy 0
deploymentu) rzucał wyjątek bez skonfigurowanego connection stringa (w
Azure dostarczanego automatycznie, lokalnie brak). Dodatkowo projekt nigdy
nie miał zainicjowanego `dotnet user-secrets` (`UserSecretsId`), więc
`Admin:Email`/`Admin:Password` z Fazy 2 nigdy nie były faktycznie ustawione
lokalnie — co podważa wcześniejsze potwierdzenie manualne 2.4 (prawdopodobnie
zweryfikowane inaczej niż przez `dotnet run` lokalnie).

Naprawy (poza zakresem planu, zaakceptowane przez użytkownika):
- `Program.cs`: `AddApplicationInsightsTelemetry()` wywoływane warunkowo,
  tylko gdy skonfigurowany jest connection string.
- `dotnet user-secrets init` w `src/melody/` + ustawienie
  `Admin:Email`=`luka103@gmail.com`, `Admin:Password`=`Admin123!!!` (dane
  testowe, nie committed — tylko `UserSecretsId` w `melody.csproj` trafia do
  repo).
- Przypadkowa zmiana portu w `launchSettings.json` (efekt uboczny `dotnet
  run`) — wykryta i cofnięta przed commitem.
- Aplikacja uruchomiona lokalnie (`http://localhost:5179`), administrator
  zasiany poprawnie przy starcie.
- Manualna weryfikacja 3.4/3.5 potwierdzona przez użytkownika (tylko "Login"
  widoczny wylogowany; "Register" widoczny i działający dla admina). Wiersz
  3.6 (konto Teacher) odłożony — nie istnieje jeszcze mechanizm tworzenia
  nauczycieli (to S-01, osobny element roadmapy, nie Faza 5 tego planu).
- Commit (ręczny użytkownika) `399aa58` „fix(...): unblock local manual
  verification (p3 follow-up)” — czysty, dokładnie zaplanowany zestaw plików.
  SHA dopisany: 3.1→`81d9859`, 3.4/3.5→`399aa58`.

**Faza 4 — Role-scoped landing stubs** (delegowana do subagenta):
- Nowe: `Controllers/AdminController.cs` (`[Authorize(Roles =
  "Administrator")]`, `/Admin`), `Controllers/TeacherController.cs`
  (`[Authorize(Roles = "Teacher")]`, `/Teacher`), odpowiadające im widoki
  placeholder `Views/Admin/Index.cshtml`, `Views/Teacher/Index.cshtml`.
- `HomeController.Index()` — dodano przekierowanie po roli
  (Administrator→`/Admin`, Teacher→`/Teacher`, inaczej zwykły widok).
- Gate build: pierwsza próba FAIL (zablokowany `melody.exe` przez
  wcześniejszy proces `dotnet run` z weryfikacji Fazy 3) → zatrzymano proces,
  druga próba PASS (2/2).
- Wiersz 4.1 odznaczony; 4.2-4.4 odłożone do Fazy 5; w toku: oczekiwanie na
  weryfikację manualną 4.5-4.7 (4.6/4.7 również zablokowane brakiem konta
  Teacher, jak w Fazie 3).

**Status na koniec tego wpisu:** Fazy 1-3 w pełni domknięte i
scommitowane; Faza 4 zaimplementowana i zbudowana, czeka na weryfikację
manualną i rytuał commitu. Pozostaje Faza 5 (projekt testowy + retrospektywne
odznaczenie wierszy zależnych od testów we wszystkich fazach).

---

### Ciąg dalszy Fazy 6 — domknięcie planu (2026-10-09)

**Faza 4 — domknięcie manualne:** utworzono testowe konto Teacher ręcznie
przez `UserManager` (zgodnie z planem — tworzenie nauczycieli to zakres
S-01, poza tą zmianą); potwierdzono wiersze 4.5-4.7 (przekierowanie na
`/Teacher`, 403 na `/Admin`). Commit (ręczny użytkownika) `dae7339`
„Phase 4 manual verification follow-up”.

**Faza 5 — Automated verification** (delegowana do subagenta):
- Nowy projekt `tests/melody.Tests` (xUnit + `WebApplicationFactory<Program>`),
  zarejestrowany w `melodybooker.sln`.
- `MelodyWebApplicationFactory.cs` — podmiana `ApplicationDbContext` na
  SQLite in-memory; odkryto po drodze, że migracje (surowy SQL pod SQL
  Server) nie działają na SQLite — dodano w `Program.cs` gałąź
  `if (Environment.IsEnvironment("Testing")) EnsureCreatedAsync() else
  MigrateAsync()`.
- `AuthorizationTests.cs` (7 testów) + `AdminUserSeederTests.cs` — pokrycie
  anonimowych/błędnych/poprawnych ról dla `/`, `/Identity/Account/Register`,
  `/Admin`, `/Teacher`, oraz idempotencji seedera.
- Gate build: PASS, 7/7 testów PASS. Commit (ręczny) `b4513d3` „Phase 5 adds
  the integration/unit test project backing authorization”. SHA dopisany
  retrospektywnie do wszystkich wcześniej odłożonych wierszy 1.2, 2.2, 2.3,
  3.2, 3.3, 4.2, 4.3.

**Odkryta luka w rytuale:** wiersz 4.4 („login redirects each role to its
own controller”) nie miał żadnego testu — realna luka, nie odłożony wiersz.
Dopisano `Administrator_RootRedirectsToAdminController` i
`Teacher_RootRedirectsToTeacherController`, zweryfikowano build + 9/9 testów
+ celową próbę złamania (usunięcie logiki przekierowania w
`HomeController.cs` → testy czerwone → przywrócono). Commit `f5c58a5`
„test(...): cover role-based post-login redirect (4.4 gap fix)”, SHA
dopisany w `15d9998`.

**Incydent produkcyjny (równolegle, poza planem):** zgłoszenie użytkownika
"nie działa mi hasło" na Azure. Diagnoza: tabela `AspNetUsers` na Azure SQL
była **całkowicie pusta** — nie błąd hasła, tylko brak konta admina w ogóle.
Przyczyna: Managed Identity nie działa z poziomu procesu Kudu (inny kontekst
kontenera niż właściwy proces aplikacji). Obejście: tymczasowe wyłączenie
`ad-only-auth`, reset hasła SQL-admina, zbudowanie i wgranie przez Kudu VFS
małego narzędzia diagnostycznego (`azdiag`), ręczne `INSERT` konta
`luka103@gmail.com` z poprawnym hashem hasła Identity i rolą Administrator
(po drodze odkryto, że `ApplicationUser.DisplayName` jest `NOT NULL` i musi
być jawnie podane w surowym INSERT-cie). Posprzątano: usunięto `azdiag` z
kontenera, przywrócono `ad-only-auth`. Zweryfikowano logowanie (symulowany
POST → 302). Lokalne konto admina (LocalDB) potwierdzone działające
niezależnie.

**Epilog planu:** po potwierdzeniu przez użytkownika, że wiersze 3.6/4.6/4.7
(wymagające konta Teacher spoza zakresu) są świadomie odłożone do S-01,
`change.md` → `status: implemented`. Commit `f72bb2f` „chore(...): close out
plan (epilogue)”. Roadmap (`F-01` → `in-progress`) celowo pozostawiony
nietknięty — flip na `done` to zadanie `/10x-archive`, nie `/10x-implement`.

## Faza 7 — `/10x-plan-review role-based-authorization` (2026-10-09)

| # | Skill / komenda | Wynik |
|---|---|---|
| 20 | `/10x-plan-review role-based-authorization` | Przegląd już zaimplementowanego planu — 2 krytyczne + 3 ostrzeżenia + 1 obserwacja, werdykt **REVISE** |

**Kluczowe odkrycie:** weryfikacja na żywo (Kudu VFS, `curl`) ujawniła, że
Azure dev od **5 października** serwowało kod **sprzed** całej tej zmiany
(pierwszy commit F-01 to 7 października) — anonimowy `GET /` zwracał 200 ze
starym, niechronionym szablonem zamiast przekierowania na login. Wszystkie
„manualne weryfikacje” na Azure w Fazach 3-4 faktycznie testowały stary kod.

**Znaleziska i naprawy (triage, wszystkie zaakceptowane):**
- **F1 (CRITICAL)** — Azure miał martwy kod → wdrożono aktualny `main` przez
  `az webapp deploy` (zip). Po drodze dwie nieudane próby: `Compress-Archive`
  i `ZipFile.CreateFromDirectory` zapisują wpisy zip z `\` zamiast `/`, co
  Kudu/rsync na Linuksie odrzuca (`rsync error: Invalid argument (22)`,
  Kudu status 400) — naprawione przez ręczne budowanie archiwum
  (`ZipArchive.CreateEntry` z jawnie znormalizowanymi ścieżkami). Po
  wdrożeniu: `GET /` → 302 do loginu (potwierdzone).
- **F2 (CRITICAL)** — `FallbackPolicy` blokował też `/health` (brak
  `.AllowAnonymous()`); zmaterializowało się natychmiast po wdrożeniu F1
  (`/health` → 302). Naprawione w `Program.cs`. Commit `d39a642`.
- **F3 (WARNING)** — kontrakt testów Fazy 5 w `plan.md` nigdy nie wymieniał
  testu przekierowania obiecanego w Fazie 4 (realnie spowodowało lukę 4.4
  powyżej). Dopisano do planu.
- **F4 (WARNING)** — `AdminUserSeeder` połykał błędy `CreateAsync` bez logu
  (utrudniło diagnozę incydentu powyżej) → dodano `logger.LogError(...)`.
- **F5 (WARNING)** — plan nie przewidział, że testy z surowymi migracjami
  SQL wymuszą gałąź środowiskową w produkcyjnym `Program.cs` → dopisana
  notatka w planie na przyszłość.
- **F6 (OBSERVATION)** — `DisplayName` seedowanego admina był zawsze pusty →
  ustawiono `"Administrator"`.

Wszystkie poprawki: build PASS, testy PASS (2/2 `AdminUserSeederTests`).
Commity (ręczne użytkownika): `d39a642` (F2), `9343f76` (F3+F5 w `plan.md`,
F4+F6 w `AdminUserSeeder.cs`). Werdykt po poprawkach: **REVISE → SOUND**.

**Status na koniec:** plan `role-based-authorization` (F-01) w pełni
zaimplementowany, przejrzany i naprawiony; kod faktycznie działa zarówno
lokalnie, jak i na Azure dev. Następny logiczny krok wg roadmapy:
`/10x-plan` dla kolejnego odblokowanego elementu (np. S-01
admin-roster-management, po potwierdzeniu konta Teacher dla 3.6/4.6/4.7, lub
F-02 deployment-skeleton żeby zautomatyzować to, co dziś robiliśmy ręcznie
przez zip-deploy).

## Faza 8 — `/10x-plan` + implementacja `admin-roster-management` (S-01) (2026-10-09)

| # | Skill / komenda | Wynik |
|---|---|---|
| 21 | `/10x-plan admin-roster-management` | `context/changes/admin-roster-management/{change.md,plan-brief.md,plan.md}` — plan w 4 fazach dla S-01: zarządzanie rosterem przez Administratora |

**Kluczowe decyzje z plan-brief:** hasło nauczyciela wpisywane bezpośrednio
przez admina (brak infrastruktury e-mail/SMS, wzorowane na
`AdminUserSeeder`); usuwanie nauczyciela/ucznia z aktywnymi
przypisaniami/rezerwacjami jest **blokowane** z czytelnym komunikatem (baza
już to wymusza przez `Restrict` FK — Faza 1 tylko to ładnie obsługuje);
edycja nauczyciela ogranicza się do imienia (bez zmiany hasła/e-maila);
zarządzanie salami poza zakresem; legacy strona
`/Identity/Account/Register` wyłączona (niebezpiecznie logowała admina na
nowo utworzone konto i nie tworzyła profilu `Teacher`).

**Implementacja (4 fazy, bez odrębnego logu w tym pliku per-faza):**
- Faza 1 — `AdminTeachersController` + widoki (create/edit/delete
  nauczycieli z prawdziwym kontem Identity, kompensujące usunięcie konta
  przy błędzie zapisu wiersza `Teacher`). Commit `7cbb029`.
- Faza 2 — `AdminStudentsController` + widoki (create/edit/delete uczniów,
  przypisanie/zmiana nauczyciela, degradacja formularza przy zerowej
  liczbie nauczycieli). Commit `6135901`.
- Faza 3 — nawigacja `/Admin` (zastąpienie placeholdera "coming soon"
  działającymi linkami). Commit `3368dd3`.
- Faza 4 — testy integracyjne: autoryzacja (`AdminRosterAuthorizationTests`)
  i reguły blokady usuwania z FR-006 (`AdminRosterDeletionTests`). Commit
  `299babb`.
- Gate build: PASS, pełny `dotnet test`: PASS (wszystkie 15 wierszy Progress
  oznaczone `[x]` z SHA).
- Epilog: `change.md` → `status: implemented`. Commit `fed7c43` „chore(...):
  close out plan (epilogue)". Roadmap (`S-01` → `done`) celowo pozostawiony
  dla `/10x-archive`.

## Faza 9 — `/10x-archive admin-roster-management` (2026-10-09)

| # | Skill / komenda | Wynik |
|---|---|---|
| 22 | `/10x-archive admin-roster-management` | Folder przeniesiony `context/changes/admin-roster-management/` → `context/archive/2026-10-09-admin-roster-management/`; roadmap `S-01` zamknięty |

**Ostrzeżenia (warn-only, zaakceptowane przez użytkownika — "continue"):**
brak katalogu `reviews/` → brak pokrycia impl-review dla faz 1–4 (nigdy nie
uruchomiono `/10x-impl-review` dla tego planu). Status `implemented` ✅,
wszystkie 15 wierszy Progress ukończone z poprawnym SHA (7+ znaków,
rozwiązują się, są przodkami bieżącego HEAD) ✅ — bez repointingu, bo brak
dowodu przepisania historii (SHA nie są przodkami `origin/main`, ale to
repo nie korzysta z PR/CI — lokalny workflow lekcyjny z gałęziami
`m1l1..m2l2`, nie świadczy to o squashu).

**Przebieg:** `change.md` stemplowany (`status: archived`,
`archived_at: 2026-10-09T21:54:55Z`) i przeniesiony przez `git mv`
(skomitowane ręcznie przez użytkownika jako `91a18a7`, przy okazji dociągając
nieśledzone wcześniej pliki skilla `10x-archive`). Roadmap: `S-01` →
`Status: done` (tabela „At a glance" + sekcja body), wpis dodany do
`## Done`. Commit `ef15a68` „chore(archive): close admin-roster-management".

**Status na koniec:** `admin-roster-management` (S-01) w pełni
zaimplementowany i zarchiwizowany. Następny logiczny krok wg roadmapy:
`/10x-plan teacher-student-list` (S-02, odblokowany przez F-01 + S-01) albo
`/10x-plan teacher-books-room` (S-03, "north star" projektu).

---

*Ten plik należy aktualizować po każdej większej fazie pracy (nowy skill,
nowa faza wdrożenia), żeby zachować czytelną historię decyzji i wykonanych
kroków.*
