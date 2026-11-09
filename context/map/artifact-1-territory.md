# Artifact 1 — Terytorium (historia gita)

> Zakres: cała historia repo (**2026-10-02 → 2026-11-09, ~5 tygodni, 114 commitów**) — krótsza niż 12
> miesięcy, więc wzięto całość. Jeden autor commitów (`lukaszzyglowicz`); tylko 5/114 commitów niesie
> jawny trailer `Co-authored-by: Copilot` (konwencja przyjęta dopiero w połowie historii), choć z
> treści commitów wynika, że większość pracy produktowej powstała przez agenta AI (skille `/10x-*`) —
> to ograniczenie metody, nie realny spadek udziału AI w czasie.

## Aktywność — gdzie projekt był realnie dotykany

### a) Top 10 folderów/modułów (po liczbie commitów dotykających dowolny plik w nich)

| # | Obszar | Commity | Funkcja produktu |
|---|--------|---------|-------------------|
| 1 | `context/changes/*` | 116 | planowanie/wykonanie zmian (artefakty `/10x-plan`, `/10x-implement`) — proces, nie produkt |
| 2 | `.github/skills/*` | 112 | toolkit agenta AI (skille 10x-cli) — infrastruktura narzędziowa, nie kod aplikacji |
| 3 | `src/melody/*` (kod appki) | 183 | aplikacja MVC: rezerwacje, roster, auth |
| 4 | `tests/melody.Tests/*` | 31 | testy integracyjne/jednostkowe |
| 5 | `context/foundation/*` | 25 | PRD, tech-stack, roadmap, test-plan |
| 6 | `context/archive/*` | 23 | zarchiwizowane zmiany (efekt `/10x-archive`, nie nowa praca) |
| 7 | `.github/prompts/*` | 8 | notatki-prompty z kursu (ten plik właśnie czytasz) |
| 8 | `infra/modules/*` | 5 | Terraform/IaC dla Azure |
| 9 | `.github/hooks/*` | 3 | hooki jakości (build+test per edycję / na koniec tury) |
| 10 | `.github/workflows/*` | 3 | CI (GitHub Actions) |

Po rozbiciu `src/melody` na drugi poziom (bo "src" samo w sobie jest zbyt ogólne):

| Podobszar | Commity | Funkcja |
|---|---|---|
| `src/melody/Views` | 42 | UI Razor |
| `src/melody/Models` | 24 | domena + ViewModels |
| `src/melody/Controllers` | 18 | logika żądań (rezerwacje, roster, admin) |
| `src/melody/(root)` | 18 | `Program.cs`, konfiguracja hostingu/auth |
| `src/melody/Data` | 9 | `ApplicationDbContext`, seederzy |
| `src/melody/wwwroot` | 64 | **fałszywy alarm** — patrz niżej |

`src/melody/wwwroot` wygląda jak najaktywniejszy podobszar appki (64 commity), ale to **jeden
commit scaffoldowy** (`dotnet new mvc`), który za jednym zamachem dodał ~60 plików
bibliotek frontendowych (Bootstrap, jQuery, jquery-validation) — każdy policzony jako "zmiana w
commicie", ale żaden nie był odtąd ręcznie dotykany poza `site.css` (2 commity). To nie jest
obszar aktywności, to jednorazowy import wendora.

Po rozbiciu `src/melody/Views` niżej: `AdminStudents` (12), `Shared` (8, layout + partial), `AdminReservations`
(4), `Admin` (4), `AdminTeachers` (4), `TeacherReservations` (3), `Teacher` (3) — praca skupia się
wyraźnie na **widokach administratora** (roster uczniów/nauczycieli), nie na widoku nauczyciela.

Top pliki pojedyncze (odfiltrowane: brak lockfile'i/snapshoty w tym repo):

| Plik | Commity | Komentarz |
|---|---|---|
| `context/foundation/roadmap.md` | 15 | ręcznie aktualizowany ledger (nie generowany) |
| `context/changes/role-based-authorization/plan.md` | 10 | najdłużej iterowany plan |
| `executions.txt` | 9 | notatki robocze w root repo (nie kod) |
| `src/melody/Program.cs` | 8 | hosting + DI + auth + routing — **jeden plik, wiele odpowiedzialności** |
| `HISTORY.md` | 7 | ręczny dziennik zmian |
| `context/changes/admin-roster-management/plan.md` | 7 | |
| `src/melody/Controllers/TeacherReservationsController.cs` | 6 | |

`.github/skills/*` (112 commitów) to **zsynchronizowany toolkit 10x-cli**, nie ręcznie pisany kod —
aktualizowany przez `/10x-cli-setup`/sync, a nie przez edycję linia-po-linii. Policzony tu jako
aktywność repo, ale innej natury niż `src/melody`.

### Podział w czasie (tygodnie — historia krótsza niż kwartał)

| Tydzień | Commity | Trend | Co się działo |
|---|---|---|---|
| 2026-W40 | 9 | start | scaffold projektu, `/10x-init`, PRD |
| 2026-W41 | 37 | **skok** | kampania: `teacher-student-list`, `admin-roster-management`, `role-based-authorization`, roadmap, pierwszy deploy |
| 2026-W42 | 3 | wygasający | przerwa |
| 2026-W43 | 37 | **skok** | kampania: `teacher-books-room`, `specialist-instrument-warning`, `admin-reservation-oversight`, `admin-students-ui-polish` — rdzeń produktu |
| 2026-W44 | 4 | wygasający | przerwa |
| 2026-W45 | 24 | rosnący | kampania: hardening — testy (duration/authorization/booking-conflict), migration-safety-gate, hooki jakości, E2E, audyt observability + logowanie |
| 2026-W46 | 1 | (trwa) | za wcześnie na ocenę |

Wzorzec jest **sezonowy/wybuchowy**: dwa wyraźne tygodnie budowy funkcji (W41, W43) przedzielone
cichymi tygodniami, a ostatnia fala (W45) to już nie nowe funkcje tylko utwardzanie (testy, CI,
observability) istniejącego zakresu — sensowna kolejność dla młodego projektu.

## Poprawki — ważne czy ciągle się psuje

6/114 commitów (~5%) to fix/revert, bez pojedynczego dużego revertu:

| Obszar | Poprawki | Natura |
|---|---|---|
| `tests/melody.Tests/AgentHooksTests.cs` | 3 | **ciągle się psuje** — test `CheckEditedFile_DotnetMissingFromPath...` łapany na żywo w tej sesji (flaky w CI, zielony lokalnie) |
| `role-based-authorization` (Program.cs + plan.md) | 3 | typowe doszlifowanie świeżo budowanej funkcji (anonymous `/health`, redirect po loginie) — nie "psuje się", tylko dojrzewało w trakcie jednego planu |

Brak merged-ale-problematycznych PR-ów: `gh pr list --state closed --search "is:unmerged"` nie
zwrócił nic. Jedno otwarte zgłoszenie: **issue #2 `[F-02] Stand up Azure App Service deploy and
GitHub Actions CI`**, oznaczone `status:blocked` — pasuje do tego, że `infra/*` ma tylko 5 commitów
i wygląda na niedokończone.

## Współzmiany — co zmienia się razem

| Para | Razem | % mniej aktywnego obszaru | Skąd wiadomo |
|---|---|---|---|
| `context` ↔ `src` | 29 | 29/31 = 94% commitów `src` | historia gita — naturalne: plan + implementacja w tym samym commicie (konwencja `/10x-implement`) |
| `context` ↔ `tests` | 17 | 17/21 = 81% commitów `tests` | jw. — testy i plan commitowane razem |
| `src` ↔ `tests` | 6 | 6/21 = 29% | jw., słabsze niż z `context` |
| `(root)` ↔ `context` | 8 | — | pliki `HISTORY.md`/`executions.txt` w root aktualizowane przy okazji zmian w `context` |

**"Wspólny mianownik" repo:** `context/foundation/roadmap.md` (15 commitów) jest jedynym plikiem
zmieniającym się obok niemal każdego innego obszaru — ale to sprzężenie **ręczne i celowe** (skill
`/10x-roadmap`/`/10x-archive` aktualizuje go jako krok procesu), nie efekt generatora/builda. Koszt
zmiany: niski (jedna sekcja na zmianę), ale warto wiedzieć, że "wszystko" koreluje z tym plikiem
tylko dlatego, że to ledger, a nie dlatego, że kod jest sprzężony.

Sprzężenie `context` ↔ `src`/`tests` to **nie jest realne sprzężenie kodu** — to artefakt
konwencji commitowania całych faz (plan + kod + testy w jednym commicie). Graf importów (artefakt
2) powie, czy *kod* sam w sobie ma realne sprzężenia między kontrolerami/widokami/modelami.

Wszystkie pliki wymienione powyżej jako mocno sprzężone/aktywne istnieją nadal w repo (zweryfikowano
`Test-Path` / listing w trakcie analizy — żaden nie został usunięty ani przeniesiony poza
`context/archive/*`, co jest oczekiwanym, udokumentowanym przeniesieniem przez `/10x-archive`).

## Ograniczenia tej analizy

- Okno: cała historia repo, ale to tylko ~5 tygodni — za krótko, by mówić o długoterminowych
  trendach; "sezonowość" tygodniowa może się nie powtórzyć.
- Jeden autor-commiter przez całą historię — nie da się tu zmierzyć "kogo zapytać" przez
  różnorodność autorów; artefakt 3 (kontrybutorzy) będzie musiał to nazwać wprost jako ograniczenie,
  nie zmyślać wielu osób.
- Udział AI liczony tylko po jawnym trailerze (`Co-authored-by: Copilot`) jest zaniżony — trailer
  pojawił się dopiero w trakcie historii; nie jest to wiarygodny sygnał dla wcześniejszych tygodni.
- `.github/skills` i `.github/prompts` liczą się jako "aktywność repo", ale to synchronizowany
  toolkit/notatki kursowe, nie kod produktu — pominięte przy wyznaczaniu stref ryzyka w dalszych
  krokach.
