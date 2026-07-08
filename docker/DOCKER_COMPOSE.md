# 🐳 Docker Compose - Wyjaśnienie

## Co to jest Docker Compose?

**Dockerfile** = buduje JEDEN kontener
**Docker Compose** = opisuje wiele kontenerów i jak się komunikują

To jak orkiestra - każdy instrument (kontener) zna swoją rolę i wie, kiedy grać!

## Skąd bierze konfigurację?

`docker-compose.yml` zakłada root plik `.env`.

Start:
```bash
copy .env.example .env
docker-compose up --build
```

Najważniejsze grupy zmiennych:

- PostgreSQL: `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
- Backend: `DEFAULT_CONNECTION`, `JWT_SECRET`, `JWT_*`, `CORS_*`, `EMAIL_CONFIRMATION_*`, `EMAIL_TWO_FACTOR_*`, `EMAIL_DELIVERY_*`
- Frontend build: `FRONTEND_REACT_APP_API_URL`
- Mailpit: `MAILPIT_SMTP_PORT`, `MAILPIT_HTTP_PORT`

## Struktura naszego docker-compose.yml

### Wersja i services
```yaml
version: '3.9'

services:
  db:
    ...
  backend:
    ...
  frontend:
    ...
```
- `version: '3.9'` = wersja compose (3.9 jest stara ale stabilna)
- `services:` = lista wszystkich kontenerów

## Backend Service

```yaml
backend:
  build:
    context: ./backend
    dockerfile: Dockerfile
```
- `build:` = zamiast pobierać z rejestru, budujemy lokalnie
- `context: ./backend` = folder z kodem
- `dockerfile: Dockerfile` = plik do budowania

```yaml
container_name: dotnet-react-backend
```
- Nazwa kontenera w systemie Docker
- Przydatna do debugowania: `docker logs dotnet-react-backend`

```yaml
ports:
  - "5000:5000"
```
- Port mapping: `host:container`
- Host (twoja maszyna): 5000
- Container (Docker): 5000
- Możesz: `http://localhost:5000` z maszyny

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
  - DefaultConnection=${DEFAULT_CONNECTION}
  - Jwt__Secret=${JWT_SECRET}
  - Cors__AllowedOrigins__0=${CORS_ALLOWED_ORIGIN_0}
  - EmailDelivery__Host=${EMAIL_DELIVERY_HOST}
```
- Zmienne środowiskowe są wstrzykiwane z root `.env`
- Backend nie trzyma sekretów w obrazie ani w `appsettings.json`

## Mailpit Service

```yaml
mailpit:
  image: axllent/mailpit:v1.27
  ports:
    - "1025:1025"
    - "8025:8025"
```

- Port `1025` = lokalny SMTP dla backendu
- Port `8025` = web UI do podglądu maili
- Dzięki temu potwierdzenia email i kody 2FA da się testować lokalnie bez zewnętrznego providera

```yaml
networks:
  - dotnet-react-network
```
- Kontener dołącza do sieci
- Dzięki temu frontend może się połączyć do backendu po nazwie "backend"!

```yaml
healthcheck:
  test: [ "CMD", "curl", "-f", "http://localhost:5000/health" ]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 40s
```
- **Sprawdza zdrowotność aplikacji**
- Co 30 sekund robić `curl http://localhost:5000/health`
- Jeśli 3 razy się nie powiedzie = kontener unhealthy
- `start_period: 40s` = czekaj 40s zanim zacząć sprawdzać

## Frontend Service

```yaml
db:
  image: postgres:16-alpine
```
- Compose uruchamia też PostgreSQL, żeby stack był samowystarczalny lokalnie
- Compose uruchamia też Mailpit, żeby backend miał od razu działającą skrzynkę testową dla email confirmation i 2FA

```yaml
frontend:
  build:
    context: ./frontend
    dockerfile: Dockerfile
    args:
      REACT_APP_API_URL: ${FRONTEND_REACT_APP_API_URL:-/api}
  container_name: dotnet-react-frontend
  ports:
    - "3000:3000"
```
- Podobnie jak backend
- Port 3000 na maszynie = port 3000 w kontenerze

```yaml
depends_on:
  - backend
```
- **WAŻNE!** Frontend czeka aż backend będzie uruchomiony
- Najpierw start backend, potem frontend
- Nie gwarantuje że backend jest gotowy, tylko że kontener się uruchomił

```yaml
networks:
  - dotnet-react-network
```
- Dołącza do sieci aby komunikować się z backendem

## Networks

```yaml
networks:
  dotnet-react-network:
    driver: bridge
```
- Tworzy wirtualną sieć dla kontenerów
- **Domyślnie kontenerami się komunikują po nazwach!**
- Np. z frontendu: `http://backend:5000` (nie localhost!)

### Dlaczego sieci?

**Bez sieci:**
```
Frontend: localhost:5000        ❌ (nie widzi backendu)
```

**Z siecią:**
```
Frontend: http://backend:5000   ✅ (vidzi backendu po nazwie)
```

Docker DNS automatycznie resolve `backend` do IP kontenera!

## Jak to uruchomić?

### Pierwsze uruchomienie:
```bash
docker-compose up
```
- Buduje obrazy (jeśli nie istnieją)
- Uruchamia kontenery
- Wyświetla logi na żywo

### W tle (daemon):
```bash
docker-compose up -d
```
- `-d` = detached mode
- Zwraca promptu, ale kontenery działają w tle

### Zatrzymanie:
```bash
docker-compose down
```
- Zatrzymuje wszystkie kontenery
- Usuwają sieci (ale obrazy zostają)

```bash
docker-compose down -v
```
- `-v` = remove volumes (usuwa dane/bazy)

### Przebudowanie po zmianie kodu:
```bash
docker-compose up --build
```
- Przebudowuje obrazy
- Uruchamia nowe kontenery

## Porządki

### Sprawdzić co działa:
```bash
docker-compose ps
```
- Wylistować uruchomione serwisy

### Logi:
```bash
docker-compose logs
```
- Wszystkie logi

```bash
docker-compose logs -f frontend
```
- `-f` = follow (na żywo)
- `frontend` = tylko tego serwisu

### Wejść do kontenera:
```bash
docker-compose exec backend bash
```
- `exec` = execute
- `backend` = nazwa serwisu
- `bash` = powłoka (polecenie do wykonania)

## Komunikacja między kontenerami

### Frontend (React) → Backend (API)
```javascript
// src/services/api.ts
const API_URL = process.env.REACT_APP_API_URL || '/api';

fetch(`${API_URL}/api/users`)
```

### W nginx.conf:
```nginx
upstream api {
    server backend:5000;  # rozwiązuje się po DNS sieci
}

location /api/ {
    proxy_pass http://api;
}
```

**Nginx** proxy'uje `/api/*` do backendu, więc w Dockerze najlepiej budować frontend z `FRONTEND_REACT_APP_API_URL=/api`.

## Zmienne środowiskowe

### Ustawić w docker-compose:
```yaml
environment:
  - DATABASE_URL=postgres://db:5432
  - LOG_LEVEL=debug
```

### Odczytać w aplikacji:
```csharp
// C# .NET
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
```

```javascript
// JavaScript
const logLevel = process.env.LOG_LEVEL;
```

## Production vs Development

### Production (docker-compose.yml):
- Multi-stage builds (mniejsze obrazy)
- Optimized builds
- Bez hot-reload

### Development (docker-compose.dev.yml - będziemy mieć):
- Volume mounts (zmiana kodu na żywo)
- Nie buduje, tylko mount'a folder
- Hot-reload (React/nodemon)
- Debug ports

## Checklist - co się dzieje przy `docker-compose up`:

1. ✅ Docker buduje backend (Dockerfile)
2. ✅ Docker buduje frontend (Dockerfile)
3. ✅ Docker tworzy network `dotnet-react-network`
4. ✅ Uruchamia backend (port 5000)
5. ✅ Czeka aż backend będzie healthy
6. ✅ Uruchamia frontend (port 3000)
7. ✅ Frontend proxy'uje API do backend:5000
8. ✅ Otwierasz `http://localhost:3000` 🎉

## Problemy i rozwiązania

### ❌ Port already in use
```bash
# Zmień port w docker-compose.yml
ports:
  - "3001:3000"  # zamiast 3000:3000
```

### ❌ Backend nie widać z frontendu
```bash
# Sprawdź network
docker network ls
docker network inspect dotnet-react-network

# Sprawdź czy backend w sieci
docker-compose ps
```

### ❌ "depends_on" czeka ale aplikacja nie gotowa
```yaml
# Zmień na health check (już mamy)
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
```

### ❌ Logi są duże, szukam specific service
```bash
docker-compose logs frontend --tail 50
# --tail 50 = ostatnie 50 linii
```

## Mniej znane rzeczy

### Override konfiguracji:
```bash
# Zmienić port na żywo
docker-compose -p myapp up

# Uruchomić tylko backend
docker-compose up backend
```

### Build bez cache (od zera):
```bash
docker-compose up --build --no-cache
```

### Scale serwisu (wiele instancji):
```bash
docker-compose up --scale frontend=3
```
