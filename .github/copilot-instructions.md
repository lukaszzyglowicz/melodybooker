<!-- BEGIN @przeprogramowani/10x-cli -->

## Zestaw narzędzi AI 10xDevs — Moduł 1, Lekcja 2

Wybierz starter i stos dla PRD napisanego w Lekcji 1, z **łańcuchem stosu**:

```
(/10x-init  →  /10x-shape  →  /10x-prd)  →  /10x-tech-stack-selector  →  (bootstrapper)
```

Łańcuch PRD jest dostarczany z Lekcji 1 (ponownie uwzględniony w tej lekcji, aby można było poprawić PRD w trakcie pracy). `/10x-tech-stack-selector` jest głównym tematem lekcji; `/10x-bootstrapper` to następne ogniwo, omawiane w Lekcji 3.

### Router zadań — Od czego zacząć

| Umiejętność | Użyj jej, gdy |
| --- | --- |
| **Wybór stosu (temat lekcji)** | |
| `/10x-tech-stack-selector` | Masz PRD w `context/foundation/prd.md` i musisz wybrać starter. Rozpoczyna się od wyraźnego wyboru (przyjmij zalecaną domyślną opcję dla swojej komórki `(product_type, language_family)` albo zaprojektuj własną), przechodzi przez zestaw pytań uzupełniających, gdy projektujesz własną opcję, stosuje cztery przyjazne agentom bramki jakości, analizuje rejestr starterów uwzględniający język i zapisuje `context/foundation/tech-stack.md`. Opcjonalny argument `[path-to-prd]` pozwala wskazać niestandardową lokalizację PRD (np. `/10x-tech-stack-selector @context/foundation/prd-v2.md`); bez niego umiejętność domyślnie używa `context/foundation/prd.md`. Użyj PO `/10x-prd`, PRZED `/10x-bootstrapper`. |
| **W razie potrzeby uruchom ponownie wcześniejsze kroki** | |
| `/10x-init` / `/10x-shape` / `/10x-prd` | Zgrupowane, aby można było poprawić PRD w trakcie pracy. Jeśli `/10x-tech-stack-selector` ujawni lukę (np. Wymaganie funkcjonalne wymuszające funkcję, której nie obsługuje zalecany starter), uruchom ponownie `/10x-prd`, aby zmienić PRD przed wyborem stosu. |

### Jak łańcuch przekazuje pracę dalej

- `/10x-tech-stack-selector` odczytuje frontmatter `context/foundation/prd.md` (`product_type`, `target_scale`, `timeline_budget`) jako założenia wstępne. Jeśli PRD nie istnieje, odmawia działania z jednoliniowym przekierowaniem do `/10x-shape` — bez wbudowanego awaryjnego mini-PRD.
- Umiejętność zapisuje `context/foundation/tech-stack.md` z frontmatterem zawierającym 4 klucze (`starter_id`, `package_manager`, `project_name`, `hints`) oraz jednoakapitową treścią `## Why this stack`. Przekazanie jest celowo minimalne — bootstrapper nie analizuje uzasadnienia, tylko pola.
- `/10x-bootstrapper` (Lekcja 3) odczytuje `tech-stack.md` i rejestr, aby utworzyć szkielet projektu.

### Co przechwytuje tech-stack-selector (i czego NIE przechwytuje)

- **Przechwytywane**: wybór startera (w kształcie rejestru), rodzina językowa, menedżer pakietów (otwarty ciąg znaków dla ekosystemu — `pnpm`, `uv`, `bundle`, `cargo` itd.), wielkość zespołu, cel wdrożenia (pobrany z `deployment_defaults` wybranego startera), dostawca CI/CD + przepływ, pewność bootstrappera (`verified | first-class | best-effort`), obrana ścieżka (standard | custom), odpowiedzi samooceny (ścieżka custom), nadpisanie jakości (ustawiane, gdy użytkownik kontynuuje ze starterem, który nie przeszedł ≥1 bramki przyjaznej agentom), flagi funkcji (auth/payments/realtime/AI/background-jobs).
- **NIE przechwytywane (celowo)**: strategiczny plan testów, strategiczny plan wdrożenia, strategiczne decyzje implementacyjne. Są one dalszym etapem po wyborze stosu — zagadnieniem przyszłej technicznej roadmapy, jeszcze nieplanowanym. Tech-stack-selector odpowiada za *ukształtowane przez framework* wybory dotyczące testów/wdrożeń/CI, ponieważ są one nierozerwalne z wyborem stosu; odraczana jest *warstwa strategiczna* („stosujemy TDD na powierzchni X”, „środowisko podglądu dla każdego PR”).

### Początkowy wybór (kluczowy)

Pierwsze pytanie jest wyraźnym wyborem — nigdy niejawnym. Umiejętność od razu podaje zalecany starter dla Twojej komórki `(product_type, language_family)` i prosi o wyraźne potwierdzenie:

- **Ścieżka standardowa** — zaakceptuj zalecaną domyślną opcję. Umiejętność pomija audyt funkcji, profil zespołu, preferencje technologiczne i pytania dotyczące wariantu frameworka; zadaje jedynie pytania o wdrożenie, CI/CD i nazwę projektu. Przekazanie zapisuje `path_taken: standard` w `hints`.
- **Ścieżka custom** — zaprojektuj własną opcję. Umiejętność przechodzi przez pełny zestaw pytań uzupełniających (audyt funkcji, profil zespołu, preferencje technologiczne, wdrożenie, CI/CD, wariant frameworka), zagłębia się w pytanie o runner testów tylko wtedy, gdy wybrany starter pozostawia tę kwestię niejednoznaczną, i kończy 5-punktową samooceną gotowości (z lekcji przygotowawczej 4.1) przed zatwierdzeniem wyboru. Przekazanie zapisuje `path_taken: custom` i wypełnia `self_check_answers`.

Mapa zalecanych domyślnych opcji dla każdej komórki jest wielojęzykowa: web/JS i saas/JS oba → 10x-astro-starter (starter marki 10x prowadzi zawsze, gdy konkuruje w komórce JS); api/JS → hono; api/Python → fastapi; web/Python → django; web/Ruby → rails; api/Go → go; api/Rust → axum; mobile/Dart → flutter; desktop/Rust → tauri; itd. Komórki bez zweryfikowanej domyślnej opcji zawierają `<none>` i wymuszają ścieżkę custom.

### Bramki jakości (kryteria przyjazne agentom)

Każda karta startera zawiera cztery wartości logiczne, według których LLM filtruje:

1. **Typed** — jawne typy/schematy, na podstawie których agent może wnioskować bez uruchamiania programu.
2. **Convention-based** — silne opinie dotyczące układu, routingu, konfiguracji.
3. **Popular in training data** — oceniane *dla każdej rodziny językowej*, a nie globalnie (Django jest popularne w danych treningowych Pythona; Spring w Javie; itd.).
4. **Well-documented** — aktualna, przypięta do wersji dokumentacja, do której można podać link.

Kandydaci, którzy nie przejdą którejkolwiek bramki, są wykluczani ze zbioru rekomendacji bez dodatkowego pytania. Jeśli wyraźnie wskażesz nieprzechodzący starter jako swoją preferencję, umiejętność zakwestionuje ten wybór — przedstawiając najsilniejszą alternatywę spełniającą wyższe kryteria ORAZ ścieżkę kompensacji (instrukcje w pliku konfiguracji AI projektu (AGENTS.md), które uzupełniają luki) — i poprosi o potwierdzenie albo zmianę kierunku. Potwierdzenie wyboru ze znanymi utrudnieniami zapisuje nadpisanie w przekazaniu, aby bootstrapper mógł się dostosować.

### Pewność bootstrappera

Każda rekomendacja wyświetla `bootstrapper_confidence` dosłownie — nigdy nie jest ona po cichu pomijana:

- **`verified`** — bootstrapper został uruchomiony kompleksowo na tym stosie; tworzenie szkieletu będzie płynne.
- **`first-class`** — zarejestrowany z prawidłowym CLI, powinien działać, ale nie został sprawdzony bojowo; oczekuj w większości płynnego tworzenia szkieletu z okazjonalnymi krokami ręcznymi.
- **`best-effort`** — ograniczone wsparcie; kroki ręczne są prawdopodobne; spodziewaj się trudności (a generowanie pliku konfiguracji AI projektu (AGENTS.md) przez bootstrapper kompensuje je dodatkowym kontekstem specyficznym dla ekosystemu).

To uprzedzenie przed uruchomieniem `/10x-bootstrapper`, aby było wiadomo, czego się spodziewać.

### Ścieżki foundation używane przez tę lekcję

- `context/foundation/prd.md` — wejście (z Lekcji 1)
- `context/foundation/tech-stack.md` — wyjście (przekazanie w łańcuchu)
- `context/foundation/lessons.md` — powtarzające się zasady i pułapki
- `docs/reference/contract-surfaces.md` — rejestr kluczowych nazw

### Uniwersalny język

Dostarczona umiejętność nie zawiera odniesień do 10xDevs / kohort / certyfikacji. Rejestr zalecanych domyślnych opcji jest wielojęzykowy (JS, Python, Ruby, Java, Go, Rust, PHP, .NET, Dart), a kohortowy `10x-astro-starter` jest jedną kartą w komórce JS+web — nie „tą” zalecaną ścieżką dla wszystkich.

Umiejętności nie mogą zapisywać do `context/archive/`. Zarchiwizowane zmiany są niezmienne; jeśli rozpoznana ścieżka docelowa zaczyna się od `context/archive/`, przerwij z komunikatem: „Ta zmiana jest zarchiwizowana. Zamiast tego otwórz nową zmianę za pomocą `/10x-new`.”

<!-- END @przeprogramowani/10x-cli -->

# MelodyBooker — Repository Instructions

## What this is

MelodyBooker is a greenfield ASP.NET Core MVC app (net10.0) for a single music
school: teachers reserve rehearsal rooms for their students' weekly lessons,
and the system enforces that rooms with specialist non-portable instruments
(piano, xylophone, drums) are prioritized for students who play those
instruments. Full requirements, personas, and functional requirements live in
`context/foundation/prd.md`; the chosen stack and rationale are in
`context/foundation/tech-stack.md`. The repo is currently at the
default-template stage (`dotnet new mvc` output) — no auth, EF Core, database,
or booking logic has been implemented yet. Read the PRD before implementing
any feature so new code matches the documented FRs/roles.

## Build & run

- Solution: `melodybooker.sln`; the only project is `src\melody\melody.csproj`.
- Build: `dotnet build melodybooker.sln`
- Run: `dotnet run --project src\melody\melody.csproj`
- No test project exists yet. If you add one, use `dotnet test` conventions
  (e.g. `dotnet test --filter FullyQualifiedName~TestClassName.TestMethodName`
  to run a single test) and register it in `melodybooker.sln`.
- No linter/formatter config exists; prefer `dotnet format` if one is added.
- No CI workflows exist in `.github/workflows` yet; `tech-stack.md` records
  the intended target as GitHub Actions with auto-deploy-on-merge to Azure
  App Service, but nothing is wired up yet.

## Architecture & conventions

- Standard ASP.NET Core MVC layout: `Controllers/`, `Models/`, `Views/`,
  `wwwroot/`, configured in `Program.cs` using the default minimal hosting
  model (`WebApplication.CreateBuilder`), `AddControllersWithViews()`, and
  conventional routing (`{controller=Home}/{action=Index}/{id?}`).
  `appsettings.json` / `appsettings.Development.json` hold config; no
  connection string or EF Core `DbContext` is configured yet.
- `Nullable` and `ImplicitUsings` are both enabled in the `.csproj` — write
  nullable-aware C# and skip boilerplate `using` statements for BCL
  namespaces.
- Per the PRD, two roles will exist: **Administrator** (manages
  teachers/students/assignments, sees and edits all reservations) and
  **Teacher** (reserves rooms only for their own assigned students). Students
  have no login. When implementing auth/authorization, model these two roles
  explicitly and scope teacher-facing queries/actions to their own students.
- Key domain rules from the PRD to preserve when implementing booking logic:
  - Lesson duration is derived from the student's class, not user input:
    20 minutes for classes 1–4, 40 minutes for classes 5–8 (FR-009).
  - Bookings are weekly-recurring for the full school year; double-booking
    the same room/time slot must always be hard-blocked, with no override
    (FR-008, FR-010).
  - Booking a specialist room (piano/xylophone/drums) for a student who
    doesn't play that instrument is a **soft** warning, not a block (FR-011);
    hard-blocking mismatches is an explicit nice-to-have (FR-012), not MVP
    scope — don't implement it unless asked.
- `context/foundation/` is the source of truth for product scope; don't
  duplicate or re-derive requirements elsewhere — reference the PRD instead.
