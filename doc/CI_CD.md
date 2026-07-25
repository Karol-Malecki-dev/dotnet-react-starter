# CI/CD

Projekt używa GitHub Actions. Pipeline jest podzielony na CI, które sprawdza kod, oraz CD, które publikuje obrazy Docker.

## CI

Workflow znajduje się w `.github/workflows/ci.yml`.

Uruchamia się dla:

- Pull Requestów;
- pushy do `main`;
- pushy do branchy `feature/**`;
- ręcznego uruchomienia z zakładki Actions.

CI składa się z trzech jobów:

1. **Backend build and tests**
   - instaluje .NET 9;
   - przywraca paczki NuGet;
   - buduje `backend/backend.slnx` w konfiguracji Release;
   - uruchamia testy jednostkowe i integracyjne;
   - zapisuje pliki `.trx` jako artefakt.

2. **Frontend build and tests**
   - instaluje Node.js 20;
   - wykonuje `npm ci`, czyli instalację dokładnie według lockfile;
   - uruchamia `npm run test:once`;
   - uruchamia `npm run build`.

3. **Docker Compose smoke tests**
   - czeka na przejście backendu i frontendu;
   - buduje i uruchamia cały stack przez Docker Compose;
   - sprawdza health endpoint backendu i frontend przez testy `E2ETests`;
   - przy błędzie zapisuje logi kontenerów;
   - zawsze zatrzymuje i usuwa kontenery oraz wolumeny testowe.

Jeżeli dowolny wymagany job zakończy się błędem, Pull Request nie powinien być mergowany.

## CD

Workflow znajduje się w `.github/workflows/cd.yml`.

Uruchamia się po pomyślnym zakończeniu CI dla `main` albo ręcznie. Buduje i publikuje dwa obrazy do GitHub Container Registry:

```text
ghcr.io/<owner>/dotnet-react-starter-backend:<commit-sha>
ghcr.io/<owner>/dotnet-react-starter-frontend:<commit-sha>
```

Dodatkowo każdy udany push do `main` aktualizuje tag `latest`.

CD **nie wdraża jeszcze aplikacji na konkretny hosting**. To celowe: sposób uruchomienia zależy od wybranej platformy, na przykład Azure Container Apps, App Service, VPS albo Kubernetes. Obraz z tagiem SHA jest niezmienny i może zostać użyty jako wersja do wdrożenia.

## Bezpieczeństwo

- `GITHUB_TOKEN` jest używany tylko do logowania do GHCR;
- workflow ma minimalne uprawnienia `contents: read` i `packages: write`;
- sekretów aplikacji nie należy wpisywać do workflow;
- sekrety runtime, takie jak `JWT_SECRET` i connection string, powinny być konfigurowane w środowisku docelowym;
- `REACT_APP_API_URL` nie jest sekretem, ponieważ trafia do publicznego buildu frontendu.

## Jak czytać pipeline

- **CI** odpowiada na pytanie: „Czy zmiana działa i nie psuje projektu?”
- **CD** odpowiada na pytanie: „Czy gotowy artefakt został opublikowany i może być wdrożony?”
- Build obrazu Docker nie oznacza jeszcze wdrożenia na produkcję.
- Tag SHA pozwala wskazać dokładny kod, który został zbudowany.
- Tag `latest` jest wygodny do testów, ale w produkcji lepiej wdrażać konkretny tag SHA.

## Lokalna odpowiednik pipeline'u

Backend i frontend:

```powershell
dotnet restore backend/backend.slnx
dotnet build backend/backend.slnx --configuration Release --no-restore
dotnet test backend/UnitTests/UnitTests.csproj --configuration Release --no-build --no-restore
dotnet test backend/IntegrationTests/IntegrationTests.csproj --configuration Release --no-build --no-restore

Set-Location frontend
npm ci
npm run test:once
npm run build
Set-Location ..
```

Docker smoke test:

```powershell
./scripts/Invoke-E2ETests.ps1
```

## Następny etap CD

Po wybraniu hostingu można dodać osobny job wdrożeniowy, który:

1. pobiera obraz oznaczony konkretnym SHA;
2. używa sekretów środowiska staging lub production;
3. wykonuje migracje bazy zgodnie z ustaloną strategią;
4. wdraża aplikację;
5. sprawdza health endpoint;
6. zatrzymuje wdrożenie przy nieudanym smoke teście.
