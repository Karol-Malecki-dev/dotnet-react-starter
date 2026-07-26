# Getting Started

Ten dokument prowadzi przez uruchomienie aktualnej wersji startera i wskazuje właściwą kolejność czytania dokumentacji.

## When To Read This Document

Czytaj ten plik, gdy konfigurujesz środowisko lokalne, uruchamiasz projekt po raz pierwszy albo szukasz podstawowych komend developerskich.

## Prerequisites

Do uruchomienia projektu lokalnie potrzebujesz:

- .NET 9 SDK
- Node.js 20+
- Docker Desktop z Docker Compose, jeśli uruchamiasz pełny stack w kontenerach
- PostgreSQL lokalnie albo PostgreSQL uruchomionego przez Docker Compose

## Configuration

Backend czyta konfigurację w tej kolejności:

1. `backend/API/appsettings.json`
2. `backend/API/appsettings.Development.json`
3. zmienne środowiskowe, User Secrets albo konfigurację hostingu

Frontend używa publicznych wartości build-time z prefiksem `VITE_`.

Dla lokalnego uruchomienia frontendu względem API ustaw w `frontend/.env.development.local`:

```text
VITE_API_URL=http://localhost:5000
```

Dla frontendu za nginx reverse proxy użyj:

```text
VITE_API_URL=/api
```

Sekretów nie umieszczaj w plikach `VITE_*`.

## Run With Docker Compose

W katalogu głównym projektu:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

Domyślne adresy:

- frontend: `http://localhost:3000`
- API: `http://localhost:5000`
- health endpoint: `http://localhost:5000/health`
- Swagger UI: `http://localhost:5000/swagger`
- Mailpit: `http://localhost:8025`

Mailpit pozwala lokalnie odczytywać linki confirm email i kody 2FA bez wysyłania prawdziwych wiadomości.

Zatrzymanie środowiska:

```powershell
docker compose down
```

Usunięcie również danych z wolumenów:

```powershell
docker compose down -v
```

## Run Backend Locally

W katalogu głównym:

```powershell
dotnet restore backend/backend.slnx
dotnet run --project backend/API/API.csproj
```

Przed uruchomieniem zapewnij działający PostgreSQL oraz poprawne wartości `DefaultConnection` i `Jwt__Secret` w środowisku lokalnym lub User Secrets.

## Run Frontend Locally

W osobnym terminalu:

```powershell
Set-Location frontend
npm install
npm start
```

Frontend będzie dostępny pod `http://localhost:3000`.

## Test And Build Commands

Backend:

```powershell
dotnet build backend/backend.slnx
dotnet test backend/UnitTests/UnitTests.csproj
dotnet test backend/IntegrationTests/IntegrationTests.csproj
dotnet test backend/E2ETests/E2ETests.csproj
```

Testy E2E wymagają uruchomionej aplikacji i poprawnego środowiska testowego.

Frontend:

```powershell
Set-Location frontend
npm run test:once
npm run build
```

## Documentation Reading Order

Zalecana kolejność:

1. `README.md` - szybki opis projektu i główne komendy
2. `ARCHITECTURE.md` - granice warstw i przepływy informacji
3. `BACKEND_SETUP.md` - backend, konfiguracja i persistence
4. `FRONTEND_SETUP.md` - bootstrap, routing i warstwa API
5. `JWT_ARCHITECTURE.md` - sesja, JWT i refresh token rotation
6. `EMAIL_2FA_FLOWS.md` - confirm email, 2FA i reset hasła
7. `ADDING_FEATURES.md` - workflow rozszerzania projektu

## Troubleshooting Order

Gdy aplikacja nie startuje, sprawdź kolejno:

1. czy Docker, PostgreSQL, .NET SDK i Node.js są dostępne
2. czy backend ma poprawne sekrety i connection string
3. czy API odpowiada pod `http://localhost:5000/health`
4. czy frontend używa właściwego `VITE_API_URL`
5. logi kontenerów albo terminala procesu, który nie wystartował

## See Also

- `README.md` - aktualny opis funkcji, stacku i endpointów auth
- `ARCHITECTURE.md` - ogólny model projektu
- `BACKEND_SETUP.md` - szczegóły backendu
- `FRONTEND_SETUP.md` - szczegóły frontendu
- `JWT_ARCHITECTURE.md` - model sesji i tokenów
- `EMAIL_2FA_FLOWS.md` - flow emailowe i 2FA
- `ADDING_FEATURES.md` - zasady dodawania feature'ów
