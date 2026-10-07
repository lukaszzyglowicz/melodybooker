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

---

*Ten plik należy aktualizować po każdej większej fazie pracy (nowy skill,
nowa faza wdrożenia), żeby zachować czytelną historię decyzji i wykonanych
kroków.*
