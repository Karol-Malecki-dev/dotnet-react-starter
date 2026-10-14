# .NET React Starter

[![CI](https://github.com/Karol-Malecki-dev/dotnet-react-starter/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Karol-Malecki-dev/dotnet-react-starter/actions/workflows/ci.yml)
[![Licencja: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React 19](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=20232A)](https://react.dev/)

Produkcyjnie ukierunkowany starter full-stack dla aplikacji, które potrzebują
realnego modelu uwierzytelniania, modularnego backendu, typowanego frontendu
React, powtarzalnego środowiska lokalnego oraz jawnej ścieżki do stagingu.

Repozytorium jest wykonywalną architekturą referencyjną i bazą do nauki. Nie
jest gotowym produktem SaaS, a domyślny stack Docker Compose nie jest
konfiguracją produkcyjną.

**English documentation:** [README.md](README.md)

## Co zawiera projekt

### Tożsamość i bezpieczeństwo kont

- rejestrację, potwierdzenie adresu email, ponowne wysłanie potwierdzenia,
  reset i zmianę hasła;
- krótkotrwałe access tokeny JWT z rotacją refresh tokenów w ciasteczku
  HttpOnly;
- e-mailowe 2FA oraz 2FA przez aplikację authenticatora z kodami odzyskiwania;
- autoryzację zależną od ról dla użytkowników, administratorów, projektów i
  zadań;
- rate limiting, blokadę po nieudanych logowaniach, zdarzenia bezpieczeństwa
  konta oraz neutralne odpowiedzi dla wrażliwych wyszukiwań kont;
- ASP.NET Core Data Protection z trwałym key ringiem dla lokalnego Compose i
  środowisk wdrożeniowych.

### Współpraca i moduły biznesowe

- projekty z rolami owner/member/viewer, zaproszeniami, zmianami członkostwa i
  archiwizacją;
- zadania projektowe z paginacją, statusami, przypisaniem, etykietami,
  terminami, komentarzami, osią aktywności i prywatnymi załącznikami;
- powiadomienia, liczniki nieprzeczytanych, preferencje email oraz trwały
  outbox wiadomości;
- backend-driven runtime configuration i feature flags;
- wyszukiwanie w workspace, dane dashboardu, widoki administracyjne i
  zarządzanie użytkownikami.

### Niezawodność i operacje

- PostgreSQL przez Entity Framework Core i rzeczywiste migracje;
- lokalny filesystem albo storage zgodny z S3; lokalny Compose używa MinIO,
  a topologia produkcyjna dodaje skanowanie ClamAV;
- workery do dostarczania emaili z outboxa, przypomnień o terminach oraz
  czyszczenia i rekonsyliacji załączników;
- strukturalne logi Serilog, korelację żądań przez `X-Correlation-ID`,
  osobne health endpointy i metryki outboxa zgodne z Prometheus;
- Docker Compose do developmentu lokalnego i smoke testów;
- GitHub Actions CI, publikowanie niezmiennych obrazów do GHCR, przypięty skan
  obrazów Trivy, chroniony deployment stagingu i runbook operacyjny VPS.

### Reusable starter V8

Repozytorium zawiera również ograniczony source template oraz generatory
PowerShell do tworzenia nowych consumerów i szkieletów vertical slice'ów.
Template jest dystrybuowany przez kopiowanie i ma jawną politykę aktualizacji
`regenerate-and-review`; nie jest runtime'owym instalatorem modułów ani
uniwersalnym frameworkiem.

Zakres i wymagania proofu opisuje
[V8_RELEASE_GATE.md](doc/V8_RELEASE_GATE.md).

## Architektura

Rozwiązanie jest modularnym monolitem z jawnymi granicami warstw backendu i
feature-oriented slices po stronie frontendu.

```text
Przeglądarka
  |
  v
React + TypeScript + Vite
  |
  v
ASP.NET Core Web API
  |-- PostgreSQL przez EF Core
  |-- MinIO lub storage zgodny z S3
  |-- SMTP lub Mailpit
  `-- Workery w tle i health checks
```

### Granice backendu

- `API` odpowiada za binding HTTP, kontrolery, middleware, walidację,
  autoryzację, health endpointy i composition root;
- `Application` posiada use case'y, command/query, rezultaty, focused ports i
  orkiestrację aplikacyjną;
- `Domain` posiada encje, value objects, enumy, agregaty i inwarianty domenowe;
- `Infrastructure` posiada EF Core, migracje PostgreSQL, adaptery persistence,
  storage, dostarczanie emaili, workery i integracje zewnętrzne;
- `Shared` zawiera świadomie współdzielone settings, DTO i helpery odpowiedzi.

Nowe command/query slices korzystają z kanonicznej ścieżki MediatR:
`ISender`, `IRequest<TResult>` i `IRequestHandler<TRequest, TResult>`.
Istniejące obszary są migrowane inkrementalnie, bez repozytoryjnego rewrite'u.

### Granice frontendu

- `context/` i `hooks/` posiadają współdzielony stan sesji i konfiguracji
  runtime;
- `services/api/` zawiera wspólny klient HTTP i adaptery API;
- `features/` zawiera lokalne klienty API, typy, stan, strony i komponenty;
- `components/` zawiera shell aplikacji i współdzielone UI;
- `pages/` zawiera ekrany aplikacji i trasy;
- `tests/` zawiera testy React, klienta API, contextów i stron.

Frontend czeka na zakończenie bootstrapu konfiguracji runtime i autoryzacji,
zanim wyrenderuje chroniony interfejs. Backend pozostaje źródłem prawdy dla
autoryzacji i dostępności feature'ów.

## Stack technologiczny

| Obszar | Technologie |
| --- | --- |
| Backend | .NET 9, ASP.NET Core Web API, EF Core, Npgsql, PostgreSQL 16, MediatR, FluentValidation, Serilog, Swagger |
| Frontend | React 19, TypeScript, Vite, React Router 7, React Hook Form, Zod, Vitest, Testing Library |
| Runtime | Docker, Docker Compose, Nginx, Caddy |
| Integracje | storage zgodny z S3/MinIO, MailKit/SMTP, Mailpit, ClamAV |
| Operacje | GitHub Actions, GHCR, Trivy, Prometheus, Alertmanager, Grafana, Blackbox Exporter |
| Testy | xUnit, Moq, Testcontainers dla PostgreSQL, smoke testy Playwright |

Repozytorium przypina SDK .NET przez [global.json](global.json). CI i obrazy
kontenerowe korzystają z Node.js 22.

## Struktura repozytorium

```text
.
├── backend/
│   ├── API/                  # adaptery HTTP, middleware, startup, auth
│   ├── Application/          # use case'y, porty, DTO i moduły
│   ├── Domain/               # encje, agregaty, value objects, inwarianty
│   ├── Infrastructure/       # EF Core, migracje, storage, email, workery
│   ├── Shared/               # współdzielone settings, response i DTO
│   ├── UnitTests/            # testy jednostkowe i architektury
│   ├── IntegrationTests/     # API, persistence, PostgreSQL i obserwowalność
│   ├── E2ETests/             # smoke testy działającej aplikacji
│   └── Dockerfile            # obraz API do wdrożenia
├── frontend/
│   ├── src/features/projects/ # feature-oriented UI projektów
│   ├── src/components/        # shell aplikacji i współdzielone UI
│   ├── src/services/          # współdzielona infrastruktura frontendu
│   └── Dockerfile             # produkcyjny obraz React/Nginx
├── deploy/vps/                # Compose staging/produkcja, TLS, monitoring, backupy
├── scripts/                   # orkiestracja testów, evidence i generatory V8
├── templates/v8-consumer/     # kanoniczny reusable source template
├── doc/                       # architektura, setup, bezpieczeństwo, operacje, roadmap
├── docker-compose.yml         # lokalny stack development/smoke testów
└── .github/workflows/         # workflowy CI i CD
```

## Szybki start

### Wymagania

- Git;
- .NET 9 SDK;
- Node.js 22 lub nowszy;
- Docker Desktop albo Docker Engine z pluginem Compose.

### Zalecane: uruchomienie pełnego lokalnego stacku

W katalogu głównym repozytorium:

```powershell
git clone https://github.com/Karol-Malecki-dev/dotnet-react-starter.git
Set-Location dotnet-react-starter
Copy-Item .env.example .env
docker compose up --build
```

Lokalny stack uruchamia PostgreSQL, Mailpit, MinIO, API ASP.NET Core oraz
frontend React/Nginx.

| Komponent | URL | Uwagi |
| --- | --- | --- |
| Frontend | `http://localhost:3000` | Aplikacja React serwowana przez Nginx |
| API | `http://localhost:5000` | Compose HTTP endpoint |
| Swagger UI | `http://localhost:5000/swagger` | Tylko w środowisku Development |
| API health | `http://localhost:5000/health` | API i zależności z tagiem ready, bez workerów |
| Mailpit | `http://localhost:8025` | Lokalna skrzynka i podgląd wiadomości |
| MinIO API | `http://localhost:9000` | Lokalny endpoint zgodny z S3 |
| Konsola MinIO | `http://localhost:9001` | Konsola developmentowa |

Wiadomości transakcyjne pozostają w Mailpit. Można w nim odczytać linki
potwierdzające konto, wiadomości resetu hasła i kody 2FA bez wysyłania
prawdziwych emaili.

Zatrzymanie stacku bez usuwania danych:

```powershell
docker compose down
```

`docker compose down -v` usuwa również wolumeny PostgreSQL, MinIO i Data
Protection. Usunięcie `data-protection-keys` unieważnia key ring używany do
ochrony sekretów authenticatora, więc tej komendy należy używać wyłącznie przy
świadomym pełnym resecie lokalnych danych.

### Uruchomienie API i frontendu lokalnie

Lokalny profil API używa portu `5031`. Najpierw skonfiguruj connection string do
PostgreSQL i developerski sekret JWT przez User Secrets albo zmienne
środowiskowe. Szczegóły znajdują się w
[BACKEND_SETUP.md](doc/BACKEND_SETUP.md).

Backend:

```powershell
dotnet restore backend\backend.slnx
dotnet run --project backend\API\API.csproj --launch-profile http
```

Frontend, w drugim terminalu:

```powershell
Copy-Item frontend\.env.example frontend\.env.development.local
Set-Location frontend
npm ci
npm start
```

Dla tego profilu w `frontend\.env.development.local` pozostaw:

```env
VITE_API_URL=http://localhost:5031
```

Serwer developerski Vite jest domyślnie dostępny pod
`http://localhost:3000`. Jeżeli API działa na innym porcie, zmień
`VITE_API_URL` oraz odpowiadający mu origin CORS.

## Konfiguracja i bezpieczeństwo

Konfiguracja ma dwa główne punkty wejścia:

1. główny plik `.env` dla Docker Compose i wartości runtime backendu;
2. pliki `frontend\.env.*` dla publicznych wartości Vite dostępnych podczas
   budowania.

Zacznij od śledzonych przykładów:

```powershell
Copy-Item .env.example .env
Copy-Item frontend\.env.example frontend\.env.development.local
```

Najważniejsze zasady:

- nigdy nie commituj `.env`, credentials, sekretów JWT, haseł SMTP ani kluczy
  storage;
- nigdy nie umieszczaj sekretów w zmiennych `VITE_*`, ponieważ trafiają do
  publicznych assetów frontendu;
- używaj `/api` jako `VITE_API_URL`, gdy frontend działa za produkcyjnym
  reverse proxy;
- przed użyciem środowiska innego niż lokalne zastąp wszystkie przykładowe
  hasła i sekrety;
- trzymaj key ring Data Protection na trwałym storage;
- skonfiguruj `ForwardedHeaders` na podstawie rzeczywistej zaufanej sieci
  proxy, zamiast ufać dowolnym adresom;
- w produkcji używaj HTTPS, bezpiecznych refresh cookies z polityką `Always`,
  jawnych originów CORS, zewnętrznego SMTP, prywatnego object storage i
  wymaganego skanowania malware.

Główny plik Compose jest celowo wygodny dla developmentu i używa wartości
developerskich. Osobna konfiguracja staging/produkcja jest opisana w
[V5_DEPLOYMENT_RUNBOOK.md](doc/V5_DEPLOYMENT_RUNBOOK.md).

## Endpointy runtime i uwierzytelnianie

### Endpointy operacyjne

| Endpoint | Odpowiedzialność |
| --- | --- |
| `GET /health` | Podstawowy health API, bazy i zależności ready; bez workerów |
| `GET /health/live` | Liveness procesu bez sprawdzania zewnętrznych zależności |
| `GET /health/ready` | Readiness bazy, object storage i skanera malware, jeśli skonfigurowane |
| `GET /health/workers` | Świeżość i ostatni stan hostowanych workerów |
| `GET /health/storage` | Stan storage załączników |
| `GET /health/malware-scanner` | Stan skanera malware załączników |
| `GET /health/email` | Stan dostarczania emaili |
| `GET /metrics` | Metryki outboxa powiadomień zgodne z Prometheus |
| `GET /api/runtime-config` | Bezpieczna, sterowana backendem konfiguracja UI |

Każda odpowiedź API zawiera `X-Correlation-ID`. Klient może przekazać własną
wartość, aby skorelować żądanie z logami strukturalnymi; jeśli jej brakuje,
API wygeneruje identyfikator.

### Flow uwierzytelniania

1. `POST /api/auth/register` tworzy konto i rozpoczyna potwierdzenie emaila.
2. `POST /api/auth/confirm-email` aktywuje konto.
3. `POST /api/auth/login` kończy logowanie albo rozpoczyna wyzwanie email 2FA
   lub 2FA przez aplikację authenticatora.
4. `POST /api/auth/verify-2fa` kończy wyzwanie i wydaje access token oraz
   refresh-token cookie.
5. `POST /api/auth/refresh-token` rotuje refresh token i zwraca nowy access
   token.
6. `POST /api/auth/logout` albo `POST /api/auth/logout-all` unieważnia sesje.
7. `POST /api/auth/forgot-password` i `POST /api/auth/reset-password` obsługują
   odzyskiwanie hasła.
8. `GET /api/auth/me` zwraca profil zalogowanego użytkownika.

Frontend przechowuje lokalnie access token i snapshot użytkownika. Refresh token
jest przechowywany wyłącznie w ciasteczku HttpOnly i nie jest dostępny dla
JavaScriptu. Sekrety authenticatora chroni ASP.NET Core Data Protection.

## Testy i bramy jakości

Najpierw uruchom najmniejszy test dotyczący zmiany, a przed mergem pełną bramę.

### Backend

```powershell
dotnet restore backend\backend.slnx
dotnet build backend\backend.slnx --configuration Release
dotnet test backend\UnitTests\UnitTests.csproj --configuration Release
dotnet test backend\IntegrationTests\IntegrationTests.csproj --configuration Release
```

Większość testów integracyjnych używa kontrolowanego setupu in-memory. Zestaw
PostgreSQL uruchamia prawdziwy kontener `postgres:16-alpine` przez Testcontainers,
stosuje rzeczywiste migracje EF Core i dlatego wymaga działającego Docker Engine:

```powershell
docker info
dotnet test backend\IntegrationTests\IntegrationTests.csproj `
  --filter "FullyQualifiedName~PostgreSqlIntegrationTests"
```

`backend\E2ETests` zawiera smoke testy działającego stacku. Użyj poniższej
pełnej lokalnej bramy smoke albo ustaw `SMOKE_API_URL` i
`SMOKE_FRONTEND_URL` przed uruchomieniem tego projektu bezpośrednio.

### Frontend

```powershell
Set-Location frontend
npm ci
npm run test:once
npm run build
```

### Pełna lokalna brama smoke

Skrypt buduje tymczasowy stack Compose, uruchamia testy backendu i frontendu,
wykonuje browser smoke tests oraz sprząta własny stack:

```powershell
.\scripts\Invoke-E2ETests.ps1
```

Zestawy `E2ETests` i Playwright są smoke testami wdrożeniowymi. Sprawdzają
kluczowe ścieżki i dostępność usług, ale nie oznaczają pełnego pokrycia reguł
biznesowych.

## CI/CD i deployment

### CI

[`ci.yml`](.github/workflows/ci.yml) uruchamia się dla pull requestów, pushy do
`main` i `feature/**` oraz ręcznego dispatchu. Weryfikuje:

- restore backendu, build Release, testy jednostkowe i integracyjne;
- instalację zależności frontendu, testy Vitest i produkcyjny build;
- produkcyjny i stagingowy model Compose, skrypty shell, składnię PowerShell
  oraz konfigurację Caddy, Prometheus i Alertmanager;
- obrazy aplikacji w Docker Compose i browser smoke tests.

### CD

[`cd.yml`](.github/workflows/cd.yml) uruchamia się po udanym CI na `main` albo
przez ręczny dispatch. Workflow:

1. buduje obrazy backendu i frontendu;
2. skanuje je przypiętym obrazem Trivy pod kątem podatności HIGH i CRITICAL;
3. publikuje niezmienne tagi z pełnym SHA commita do GHCR;
4. może wdrożyć dokładny SHA do chronionego środowiska staging na VPS;
5. uruchamia publiczne testy browser smoke przez HTTPS i zapisuje release evidence.

Publikacja obrazu nie jest równoznaczna z wdrożeniem produkcji. Promocja
produkcji pozostaje jawną decyzją operatora i wymaga release gates, dowodów
backup/restore, walidacji dostarczenia alertu oraz celu rollbacku opisanych w
[CI_CD.md](doc/CI_CD.md) i
[V5_DEPLOYMENT_RUNBOOK.md](doc/V5_DEPLOYMENT_RUNBOOK.md).

### Topologia produkcyjna

Osobna ścieżka `deploy\vps` zakłada jeden zabezpieczony Linux VPS z Docker
Compose:

- Caddy kończy publiczny TLS, a frontend i API pozostają prywatne;
- jednorazowy kontener migracji uruchamia się przed API;
- PostgreSQL i MinIO korzystają z trwałych wolumenów;
- ClamAV skanuje załączniki, zanim staną się dostępne;
- Prometheus, Alertmanager, Blackbox Exporter, Node Exporter i Grafana
  dostarczają health, alerting i obserwowalność hosta;
- szyfrowane backupy, restore drills i ręczny rollback są częścią kontraktu
  operacyjnego.

Nie używaj głównego lokalnego pliku Compose ani jego przykładowych credentials
jako procedury wdrożenia produkcyjnego.

## Scaffolding V8

Wygenerowanie ograniczonego projektu consumer:

```powershell
.\scripts\New-V8StarterProject.ps1 `
  -Destination .\artifacts\v8\consumer `
  -Variant Minimal
```

Dodanie szkieletu query lub command:

```powershell
.\scripts\New-VsaSlice.ps1 `
  -RootPath .\artifacts\v8\consumer `
  -Module Catalog `
  -UseCase ListItems `
  -Kind Query
```

Powtarzalny proof obu wariantów template'u:

```powershell
.\scripts\Invoke-V8ScaffoldingProof.ps1 `
  -OutputDirectory .\artifacts\v8\release-proof
```

Wygenerowane handlery są jawnie oznaczonymi szkieletami i rzucają wyjątek, dopóki
consumer nie zaimplementuje reguły domenowej, autoryzacji, portów, persistence,
transakcji, mapowania błędów i testów. Przed traktowaniem wygenerowanych plików
jak kodu aplikacji przeczytaj
[templates/v8-consumer/Common/README.md](templates/v8-consumer/Common/README.md).

## Mapa dokumentacji

| Gdy chcesz... | Przeczytaj |
| --- | --- |
| Uruchomić projekt po raz pierwszy | [GETTING_STARTED.md](doc/GETTING_STARTED.md) |
| Zrozumieć granice modułów i warstw | [ARCHITECTURE.md](doc/ARCHITECTURE.md) |
| Skonfigurować backend i persistence | [BACKEND_SETUP.md](doc/BACKEND_SETUP.md) |
| Zrozumieć bootstrap frontendu i klienta API | [FRONTEND_SETUP.md](doc/FRONTEND_SETUP.md) |
| Poznać JWT, sesję i rotację refresh tokenów | [JWT_ARCHITECTURE.md](doc/JWT_ARCHITECTURE.md) |
| Poznać confirm email, 2FA i reset hasła | [EMAIL_2FA_FLOWS.md](doc/EMAIL_2FA_FLOWS.md) |
| Dodać nową funkcję backendową lub frontendową | [ADDING_FEATURES.md](doc/ADDING_FEATURES.md) |
| Zrozumieć lokalny Docker Compose | [DOCKER_COMPOSE.md](docker/DOCKER_COMPOSE.md) |
| Zrozumieć CI, CD i staging | [CI_CD.md](doc/CI_CD.md) |
| Operować deploymentem VPS | [V5_DEPLOYMENT_RUNBOOK.md](doc/V5_DEPLOYMENT_RUNBOOK.md) |
| Skonfigurować zmienne środowiskowe i sekrety | [CONFIGURATION_REFERENCE.md](doc/CONFIGURATION_REFERENCE.md) |
| Rozwiązać problemy lokalne, testowe i wdrożeniowe | [TROUBLESHOOTING.md](doc/TROUBLESHOOTING.md) |
| Zintegrować się z API HTTP | [API_REFERENCE.md](doc/API_REFERENCE.md) |
| Kontrybuować kod, dokumentację lub template'y | [CONTRIBUTING.md](CONTRIBUTING.md) |
| Zgłosić podatność bezpieczeństwa | [SECURITY.md](SECURITY.md) |
| Przejrzeć historię wydań repozytorium | [CHANGELOG.md](CHANGELOG.md) |
| Przejrzeć bramy wydajności i niezawodności | [V6_RELEASE_GATE.md](doc/V6_RELEASE_GATE.md) |
| Przejrzeć release reusable startera | [V8_RELEASE_GATE.md](doc/V8_RELEASE_GATE.md) |
| Poznać szerszy roadmap produktu | [ROADMAP/00_ROADMAP_OVERVIEW.md](doc/ROADMAP/00_ROADMAP_OVERVIEW.md) |
| Przejrzeć zapisane evidence release'u | [RELEASE_READINESS_AUDIT.md](doc/Audits/RELEASE_READINESS_AUDIT.md) |

Zasady inżynierskie repozytorium opisują
[.github/copilot-instructions.md](.github/copilot-instructions.md) oraz
backend-specific [backend.instructions.md](.github/instructions/backend.instructions.md).

## Zakres i ograniczenia

- Główny Compose służy do lokalnego developmentu i testów. Jego credentials,
  konfiguracja HTTP i wystawione porty są przykładowe.
- Ścieżka wdrożeniowa jest celowo kontrolowanym modelem pojedynczego VPS, a nie
  automatyczną platformą multi-region ani dystrybucją Kubernetes.
- Template V8 jest bazą kopiowaną jako source. Consumerzy posiadają własny kod
  i aktualizują go przez review.
- Smoke testy obejmują krytyczną dostępność i ścieżki użytkownika, ale nie
  wszystkie reguły biznesowe ani profile ruchu produkcyjnego.

## Licencja

MIT. Szczegóły znajdują się w [LICENSE](LICENSE).
