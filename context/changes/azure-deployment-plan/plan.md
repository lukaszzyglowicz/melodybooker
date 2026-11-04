# Plan wdrożenia: MelodyBooker na Azure App Service

> **Status: PLAN — nic z poniższego nie zostało jeszcze wykonane.** Żadne zasoby Azure
> nie istnieją, żadne komendy `az`/Bicep nie zostały uruchomione. Ten dokument jest
> punktem wyjścia do realizacji, zgodnym z `context/foundation/infrastructure.md`
> (rekomendacja: Azure App Service + Azure SQL Database) i `context/foundation/tech-stack.md`
> (.NET/ASP.NET Core MVC, SQL Server, GitHub Actions, auto-deploy-on-merge).

## Decyzje potwierdzone przez użytkownika

- **ORM**: Entity Framework Core (Code First + migracje) — zgodnie z `tech-stack.md`
  ("pairs naturally with this starter's Entity Framework integration").
- **Infrastructure as Code**: **Bicep** zamiast imperatywnych komend `az` — zasoby
  Azure będą deklaratywne, wersjonowane w repozytorium, odtwarzalne.
- **Subskrypcja**: Visual Studio Professional — oznacza dostęp do:
  - **50 USD/miesiąc kredytu Azure** (odnawialny, wyłącznie na obciążenia dev/test;
    usługi wstrzymują się po wyczerpaniu limitu, chyba że zniesiesz limit wydatków
    i przejdziesz na pay-as-you-go).
  - **Cennika Azure Dev/Test** — zniżki na App Service, Azure SQL Database i inne
    usługi dla obciążeń nieprodukcyjnych (np. Windows/SQL VM wycenione jak Linux VM).
  - **Ograniczenie**: kredyt i ceny dev/test dotyczą wyłącznie środowisk
    deweloperskich/testowych — **środowisko produkcyjne MVP (to, z którego faktycznie
    będą korzystać nauczyciele) powinno być rozliczane na zwykłej subskrypcji
    pay-as-you-go**, nie na kredycie dev/test, żeby uniknąć nagłego wstrzymania usługi
    po przekroczeniu 50 USD. Rekomendacja: użyj subskrypcji VS Professional + dev/test
    pricing dla środowiska *staging/testowego*, a dla *produkcji* załóż osobną
    subskrypcję (lub przynajmniej usuń spending cap) przed udostępnieniem aplikacji
    nauczycielom.

## Założenia do potwierdzenia przed realizacją

| Pytanie | Domyślna propozycja |
|---|---|
| Nazwa zasobów (resource group, web app, SQL server) | `melodybooker-rg`, `melodybooker-app`, `melodybooker-sql` |
| Region | `westeurope` (najbliższy dla szkoły w PL) |
| Warstwa App Service | **Basic B1** na start (bez slotów) — tańsze; przejście na Standard S1 (sloty + bezpieczny rollback) jako decyzja odroczona, patrz rejestr ryzyk w `infrastructure.md` |
| Warstwa Azure SQL Database | **Basic DTU** (~5 USD/mies.), nie Serverless (ryzyko kosztowe z `infrastructure.md`) |
| Środowiska | `dev` (kredyt VS Professional, dev/test pricing) → `prod` (osobna subskrypcja/bez cappingu) |

## Etapy planu

### Etap 0 — Przygotowanie aplikacji lokalnie (EF Core)

1. Dodać pakiety: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`,
   `Microsoft.EntityFrameworkCore.Tools`.
2. Zamodelować `ApplicationDbContext` zgodnie z encjami z PRD: `Teacher`, `Student`,
   `Room`, `Reservation`, role (`Administrator`, `Teacher`).
3. Skonfigurować `DbContext` w `Program.cs` z connection string czytanym z
   `IConfiguration` (`appsettings.json` → placeholder lokalnie, Key Vault reference
   w produkcji — **nigdy prawdziwy connection string w repo**).
4. Wygenerować pierwszą migrację: `dotnet ef migrations add InitialCreate`.
5. Zdecydować strategię stosowania migracji w produkcji: albo `dotnet ef database update`
   jako krok w CI/CD, albo EF Core **migration bundle** (`dotnet ef migrations bundle`)
   uruchamiany jako startup task/WebJob na App Service — **do ustalenia w Etapie 3**.
6. Dodać endpoint healthcheck (`/health`) do walidacji po wdrożeniu.

### Etap 1 — Infrastruktura jako kod (Bicep)

Utworzyć katalog `infra/` w repozytorium z modułami Bicep:

- `infra/main.bicep` — plik wejściowy, parametryzowany (`environment`: dev/prod,
  `location`, `appServiceSku`, `sqlSku`).
- `infra/modules/appServicePlan.bicep` — Linux App Service Plan.
- `infra/modules/webApp.bicep` — Web App z runtime `.NET 10`, systemowo przypisaną
  Managed Identity, WebSockets/Always On wg warstwy.
- `infra/modules/sqlDatabase.bicep` — Azure SQL Server + Database (Basic DTU),
  Azure AD-only authentication (bez hasła SQL — tylko Managed Identity/Entra ID),
  reguła firewalla "Allow Azure services".
- `infra/modules/keyVault.bicep` — Key Vault + access policy/RBAC dla Managed Identity
  Web App.
- `infra/main.parameters.dev.json` / `infra/main.parameters.prod.json` — osobne
  parametry dla środowisk dev (dev/test pricing) i prod.

Walidacja lokalna przed jakimkolwiek wdrożeniem (bez tworzenia zasobów):
```
az bicep build --file infra/main.bicep
az deployment group what-if --resource-group melodybooker-rg --template-file infra/main.bicep --parameters infra/main.parameters.dev.json
```
`what-if` pokazuje, co *zostałoby* utworzone — nie tworzy niczego. To ma być pierwszy
realny krok wykonawczy, dopiero po Twoim zatwierdzeniu.

### Etap 2 — Sekrety i tożsamość

- Connection string do SQL i inne sekrety wyłącznie w Key Vault, odczytywane przez
  App Service jako Key Vault references (`@Microsoft.KeyVault(...)`) z Managed Identity.
- Brak haseł SQL — autoryzacja przez Azure AD/Entra ID + Managed Identity (unika
  pułapki z whitelistą IP opisanej w `infrastructure.md`).
- GitHub Actions: Entra ID federated credential (OIDC) zamiast publish-profile —
  bez długotrwałego sekretu w GitHub Secrets.

### Etap 3 — CI/CD (GitHub Actions)

Pipeline (`.github/workflows/deploy.yml`), zgodny z `auto-deploy-on-merge`:

1. `dotnet build` + `dotnet test` (gdy powstaną testy).
2. `az bicep build` + `az deployment group create` (lub `what-if` na PR, realny
   deploy na merge do `main`) — infrastruktura jako część pipeline'u, nie ręczne
   klikanie w portalu.
3. Zastosowanie migracji EF Core (`dotnet ef database update` lub bundle — decyzja
   z Etapu 0).
4. `az webapp deploy` (publikacja artefaktu aplikacji) z autoryzacją OIDC.
5. Smoke test: curl `/health` po wdrożeniu.

### Etap 4 — Observability

- Application Insights podłączony od pierwszego wdrożenia (moduł Bicep
  `infra/modules/appInsights.bicep`).
- `az webapp log tail` jako kanał logów na żywo.

### Etap 5 — Walidacja i rollback

- Smoke test checklist: `/health` → 200, połączenie z SQL przez Managed Identity,
  logowanie nauczyciela działa.
- Rollback: na Basic — redeploy poprzedniego artefaktu; jeśli przejdziemy na
  Standard — `az webapp deployment slot swap` w odwrotną stronę.
- Migracje EF Core: backup point-in-time Azure SQL przed każdą migracją produkcyjną;
  pisać down-migration razem z up-migration.

## Co pozostaje otwarte (do decyzji przed realizacją)

1. Czy środowisko **dev** (na kredycie VS Professional) i **prod** mają być od razu
   dwiema osobnymi subskrypcjami Azure, czy jedną subskrypcją z dwoma resource group?
2. Czy stosować migracje EF Core bezpośrednio w pipeline (`database update`), czy
   przez migration bundle jako osobny krok uruchamiany ręcznie z aprobatą?
3. Potwierdzenie warstwy App Service (Basic vs Standard) — wpływa na strukturę
   modułu Bicep (czy od razu modelować sloty).

## Poza zakresem tego planu

- Pisanie logiki domenowej aplikacji (modele, kontrolery, widoki) — osobny temat.
- Konfiguracja obrazów Docker (aplikacja celuje w natywny runtime App Service, nie
  kontener).
- Architektura produkcyjna wieloregionowa/HA.

---
*Źródła: `context/foundation/infrastructure.md`, `context/foundation/tech-stack.md`,
badanie Azure Dev/Test benefit (visualstudio.microsoft.com/vs/pricing-details,
learn.microsoft.com/en-us/azure/devtest/offer/overview-what-is-devtest-offer-visual-studio),
przykłady Bicep (learn.microsoft.com/en-us/azure/app-service/samples-bicep,
learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references?tabs=bicep).*
