# Artifact 2 — Struktura (graf zależności, zaadaptowane do C#/.NET)

> **Uwaga metodologiczna:** oryginalny prompt zakłada `dependency-cruiser` i strukturę katalogów
> innego projektu (JS/TS monorepo w stylu Mattermost: `channels/src`, `platform/client`,
> `platform/types`). `dependency-cruiser` analizuje tylko JS/TS/CoffeeScript, a MelodyBooker to
> ASP.NET Core MVC w C# (jedyny kod TS to `tests/e2e`, nieistotny strukturalnie). Zamiast
> dependency-cruiser użyto ręcznej analizy grafu referencji (`using`, wstrzykiwane typy w
> konstruktorach, wywołania statyczne) po `src/melody` — repo ma tylko 7 kontrolerów, więc pełny
> graf mieści się w jednej sesji bez narzędzia. Pytania z promptu (cykle w aktywnych obszarach,
> granice warstw, ryzyka testowalności) zostały zachowane.

## 3-5 najważniejszych obserwacji

1. **Brak warstwy pośredniej (repozytorium/serwis)** — wszystkie 5 nietrywialnych kontrolerów
   (`AdminReservationsController`, `AdminStudentsController`, `AdminTeachersController`,
   `TeacherController`, `TeacherReservationsController`) wstrzykuje `ApplicationDbContext`
   bezpośrednio i same piszą LINQ. To nie jest "źle" przy tej skali, ale oznacza, że **każdy**
   kontroler-test musi mówić EF/SQLite, nie da się podmienić jednej warstwy dostępu do danych.
2. **`Program.cs` to jedyny plik konfiguracyjny** dla DI, Identity, cookie-auth, authorization
   fallback policy, App Insights, health checks, routing, ORAZ orkiestracji migracji+seedingu
   przy starcie — 8 commitów w artefakcie 1 potwierdza, że to punkt, w którym ląduje prawie
   każda zmiana dotycząca infrastruktury/auth. Zmiana w jednym miejscu (np. dodanie nowej
   polityki autoryzacji) ryzykuje konflikt z inną równoległą zmianą (np. health checks) w tym
   samym pliku.
3. **Reguła biznesowa żyje jako static method na encji EF** — `Reservation.Overlaps(...)` i
   `Reservation.IsInstrumentMismatch(...)` (`Models/Domain/Reservation.cs`) implementują FR-010/
   FR-011, a wywołują je niezależnie `AdminReservationsController` i
   `TeacherReservationsController` — bez wspólnej klasy bazowej czy serwisu. To **realne
   sprzężenie przez współdzieloną funkcję statyczną**, nie przez dziedziczenie: zmiana sygnatury
   `Overlaps`/`IsInstrumentMismatch` dotyka od razu dwóch niezależnych kontrolerów.
4. **Nawigacyjne cykle Teacher↔Student↔Reservation↔Room wyglądają groźnie, ale to standardowy
   wzorzec EF Core** (dwukierunkowe właściwości nawigacyjne wymagane przez
   `HasOne().WithMany()`) — nie są to cykle importów/logiki, tylko relacje ORM. Patrz sekcja
   "Wygląda groźnie, ale nie jest" niżej.
5. **`ApplicationDbContext` ma wysoki fan-in** (10 plików je referencuje: 5 kontrolerów, 3
   seedery, `Program.cs`, migracje) — to oczekiwane dla tej skali repo (brak warstwy
   repozytorium, patrz #1), ale potwierdza, że to jedyny prawdziwy "hub" w grafie zależności.

## Cykle w aktywnych obszarach

Najaktywniejsze obszary z `artifact-1-territory.md`: `src/melody/Views` (42), `src/melody/Models`
(24), `src/melody/Controllers` (18), `src/melody/(root)` = `Program.cs` (18).

| Obszar | Co znalazłem | Dowód | Dlaczego ważne przy zmianie | Związek z artifact-1 | Co sprawdzić dalej |
|---|---|---|---|---|---|
| `Models/Domain` | Cykl nawigacyjny `Teacher ⇄ Student ⇄ Reservation ⇄ Room` (wszystkie 4 klasy wzajemnie się referencują przez właściwości nawigacyjne) | `Teacher.cs:14-15`, `Student.cs:21-24`, `Reservation.cs:12-19`, konfiguracja w `ApplicationDbContext.OnModelCreating` | To jest **cykl EF, nie cykl logiki** — bezpieczny do rozszerzania encji, ale lazy/eager-loading całego grafu przy niechlujnym `Include()` może przypadkiem ładować więcej niż trzeba | `Models` ma 24 commity — druga najaktywniejsza warstwa po `Views`; każda nowa encja prawdopodobnie dołoży kolejną krawędź do tego samego cyklu | Sprawdzić, czy przyszłe zapytania używają `.Include()` selektywnie, czy ciągną cały graf |
| `Controllers` (Admin/TeacherReservations) | Brak cyklu importów, ale **sprzężenie przez wspólną funkcję statyczną** `Reservation.Overlaps`/`IsInstrumentMismatch` wywoływaną niezależnie w obu kontrolerach | `grep Reservation\.(Overlaps|IsInstrumentMismatch)` → trafienia tylko w `AdminReservationsController.cs` i `TeacherReservationsController.cs` | Zmiana reguły podwójnej rezerwacji/niedopasowania instrumentu wymaga przejrzenia **obu** kontrolerów ręcznie — nic nie wymusi aktualizacji drugiego miejsca przy edycji pierwszego | `Controllers` ma 18 commitów, oba pliki są w top-6 najczęściej zmienianych plików z artifact-1 | Rozważyć wydzielenie tej reguły do jednego serwisu/klasy domenowej wywoływanej przez oba kontrolery |
| `Program.cs` | Brak cyklu (plik jednokierunkowo zależy od `Data`/`Models`), ale **jeden plik skupia 6 niezależnych odpowiedzialności** (DI, Identity, cookies, authz policy, telemetry, routing+seeding) | treść pliku, linie 1-95 | Nie jest to cykl, ale "fałszywy hub" — wiele niepowiązanych zmian przechodzi przez ten sam plik, zwiększając szansę konfliktu scalania, mimo że logicznie się nie dotykają | 8 commitów w top-plikach z artifact-1 | Przy kolejnej zmianie w auth/telemetry rozważyć, czy da się wydzielić blok `Extensions` (np. `AddAppIdentity()`, `AddAppTelemetry()`) bez zmiany zachowania |

## Granice warstw

Warstwy w tym repo (odpowiednik `types`/`client`/`channels` z oryginalnego promptu):
`Models/Domain` (fundament) → `Data` (EF Context + seedery) → `Controllers` → `Views`
(+ równoległa warstwa `Models/ViewModels`, używana tylko przez `Controllers`↔`Views`).

| Sprawdzana granica | Wynik | Dowód | Dlaczego ważne przy zmianie | Związek z artifact-1 | Co sprawdzić dalej |
|---|---|---|---|---|---|
| `Models/Domain` nie powinien zależeć od `Controllers`/`Data` | **Respektowana**, z jednym wyjątkiem stylu: `Reservation.IsInstrumentMismatch` przyjmuje `Room`/`Student` jako parametry (czysta zależność wewnątrz tej samej warstwy, nie w górę) | brak `using melody.Controllers` / `using melody.Data` w żadnym pliku `Models/Domain/*.cs` | Gdyby złamano tę granicę, zmiana w kontrolerze mogłaby przypadkiem zepsuć encję EF współdzieloną przez cały system | `Models/Domain` to 8 z 24 commitów `Models` | — |
| `Views` nie powinien duplikować logiki biznesowej (np. przeliczać mismatch sam) | **Respektowana** — `Views/TeacherReservations/Create.cshtml` tylko wyświetla `Model.ShowMismatchWarning`/`Model.MismatchWarningMessage`, prekalkulowane w kontrolerze/ViewModel | `grep Instrument|Mismatch` w `Views/` → wszystkie trafienia to odczyt gotowej flagi, zero logiki warunkowej liczącej mismatch od nowa | Gdyby Razor przeliczał regułę sam, zmiana FR-011 wymagałaby edycji w trzecim miejscu (obok dwóch kontrolerów) | `Views` to najaktywniejsza warstwa (42 commity) — dobrze, że nie niesie logiki | — |
| `Controllers` → `Data` tylko przez `ApplicationDbContext` (brak repozytorium) | **Zgodne z resztą repo, ale to słaba granica** — nie ma interfejsu między kontrolerem a EF, więc "granica" istnieje tylko umownie | fan-in `ApplicationDbContext`: 5 kontrolerów + 3 seedery + `Program.cs` bezpośrednio | Brak abstrakcji nie przeszkadza dziś (mały zespół, jeden autor), ale każdy przyszły refaktor "wymień bazę/dodaj cache" dotknie 5 plików na raz | `Controllers` (18) + `Data` (9) razem pokrywają rdzeń logiki aplikacji | Jeśli repo urośnie, to pierwszy kandydat do wydzielenia warstwy serwisowej |

## Ryzyka testowalności

### Podsumowanie

Repo już ma odpowiedź na większość tych ryzyk: **testy integracyjne przez
`WebApplicationFactory<Program>` + SQLite in-memory** (zamiast mockowania `ApplicationDbContext`),
co ten sesja sama rozszerzyła o `FaultInjectingWebApplicationFactory` dla wstrzykiwania awarii.
To dobry wybór przy obecnym braku warstwy repozytorium — mockowanie samego EF `DbContext` byłoby
kruche i tak nie testowałoby realnych ograniczeń unikalności (`HasIndex(...).IsUnique()`).

### Lista ryzyk testowych

| Moduł | Ryzyko | Dlaczego |
|---|---|---|
| `AdminTeachersController.Create` | Trudny do izolowanego unit-testu | Łączy `UserManager<ApplicationUser>.CreateAsync` (Identity) + własny zapis EF + kompensującą transakcję (`DeleteAsync` przy błędzie) — wymaga pełnego stosu Identity+EF, nie samej logiki |
| `TeacherReservationsController` / `AdminReservationsController` | Wymaga danych z 3 powiązanych tabel (`Room`, `Student`/`Teacher`, istniejące `Reservation`) żeby przetestować `Overlaps`/`IsInstrumentMismatch` | Logika konfliktu czasowego zależy od realnego stanu bazy — test jednostkowy bez bazy musiałby zmockować zapytanie LINQ, co nic by nie udowodniło |
| `Program.cs` (seeding przy starcie) | Nie da się przetestować w izolacji — uruchamia się tylko przy starcie hosta | Już obejście: środowisko `Testing` używa `EnsureCreatedAsync` zamiast `MigrateAsync`, co samo w sobie jest rozbieżnością prod/test warte świadomego utrzymywania |
| `Reservation.Overlaps` / `IsInstrumentMismatch` | **Niskie ryzyko** — to czyste, statyczne funkcje bez zależności, łatwe do unit-testu w izolacji | Pozytywny kontrprzykład: mimo że są wywoływane z dwóch kontrolerów (sprzężenie #3 wyżej), same w sobie są testowalne bez żadnego mocka |

### Najbardziej podejrzane moduły

1. `AdminTeachersController.Create` — najwięcej splecionych efektów ubocznych (Identity + EF +
   rollback) w jednej metodzie.
2. `Program.cs` — logika startowa (migracje/seeding) nietestowalna inaczej niż przez pełny
   `WebApplicationFactory` (co już się robi).

### Wygląda groźnie, ale nie jest

- **Cykl `Teacher ⇄ Student ⇄ Reservation ⇄ Room`** — to są relacje nawigacyjne EF Core wymagane
  przez konfigurację `HasOne().WithMany()` w `OnModelCreating`, nie cykl logiki/importów. Każdy
  model EF z relacjami 1-do-wielu w obie strony wygląda tak samo w grafie.
- **`ApplicationDbContext` jako "wszystko do wszystkiego"** — wysoki fan-in jest tu architekturalną
  konsekwencją braku warstwy repozytorium w małej aplikacji, a nie przypadkowym rozrostem zależności
  w czasie; nie ma potrzeby traktować tego jak dług, dopóki zespół zostaje jednoosobowy/mały.
- **Views z dużą liczbą plików w `Views/AdminStudents` (12 commitów)** — to echo artefaktu 1
  (praca skupiona na rosterze administratora), nie oznaka niestabilności strukturalnej; sama
  struktura katalogu jest płaska i przewidywalna (Index/Create/Edit/Delete + partial).

### Opcjonalny kolejny krok: graf

Nie renderowano SVG/Graphviz — graf repo (35 plików `.cs`, 7 kontrolerów) jest czytelny jako tabela
tekstowa powyżej; render miałby sens dopiero przy znacznie większym repo.

## Ograniczenia tej analizy

- Brak narzędzia class-dependency dla C# (odpowiednika dependency-cruiser) — graf zbudowany ręcznie
  przez grep/odczyt kodu, nie automatycznie; przy dużo większym repo to przestałoby się skalować.
- Nie analizowano `Views/*.cshtml` jako grafu zależności (Razor nie ma statycznego importu w stylu
  JS) — oceniono je jakościowo przez wyszukiwanie konkretnych wzorców (mismatch/instrument), nie
  wyczerpująco.
- `Areas/Identity/Pages/Account/Register.cshtml.cs` (scaffoldowany kod Identity UI) pominięty —
  wygenerowany przez `dotnet new`, poza zakresem ręcznie pisanej logiki aplikacji.
