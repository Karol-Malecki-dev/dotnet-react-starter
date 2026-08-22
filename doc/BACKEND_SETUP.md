# Backend Setup

Ten dokument opisuje aktualny backend startera i sposób rozwijania nowych funkcji bez psucia istniejącej struktury.

## When To Read This Document

Czytaj ten plik, gdy zmiana dotyczy API, konfiguracji, persistence, serwisów backendowych, walidacji albo testów backendu.

## Current Backend Responsibilities

Backend odpowiada za:

- autoryzację i uwierzytelnianie użytkownika
- generowanie JWT i rotację refresh tokenów
- email confirmation i email-based 2FA
- password reset
- role-based authorization
- dostarczanie runtime feature flags dla frontendu
- persistence przez EF Core i PostgreSQL
- projekty, zadania, członkowie, załączniki, etykiety, terminy i powiadomienia
- health checks, korelacja żądań i cykliczne workery infrastrukturalne

## Backend Layers

### API

Warstwa `API/` zawiera:

- kontrolery HTTP
- middleware
- konfigurację hosta
- rejestrację usług

Najważniejsze pliki:

- `API/Program.cs`
- `API/Services/AddProjectServices.cs`
- `API/Controllers/AuthController.cs`
- `API/Controllers/UsersController.cs`
- `API/Controllers/AdminController.cs`
- `API/Controllers/RuntimeConfigController.cs`

### Application

Warstwa `Application/` zawiera:

- DTO request/response
- interfejsy serwisów aplikacyjnych
- walidatory
- mapowania

To miejsce na kontrakty wejścia/wyjścia oraz logikę orkiestrującą, która nie powinna siedzieć w kontrolerach.

### Domain

Warstwa `Domain/` zawiera:

- encje
- enumy
- value objects
- interfejsy domenowe

To miejsce na model biznesowy niezależny od HTTP i infrastruktury.

### Infrastructure

Warstwa `Infrastructure/` zawiera:

- `ApplicationDbContext`
- migracje EF Core
- implementacje serwisów opartych o bazę danych
- implementacje usług infrastrukturalnych, np. email senderów

Najważniejszy plik auth po stronie persistence:

- `Infrastructure/Services/DatabaseAuthService.cs`

### Shared

Warstwa `Shared/` zawiera:

- `Responses/` z ustandaryzowanym `ApiResponse`
- `Settings/` z klasami bindowanymi z konfiguracji
- DTO współdzielone między backendem i frontendem, np. runtime config

## Configuration Model

Backend korzysta z trzech warstw konfiguracji:

1. `appsettings.json`
2. `appsettings.Development.json`
3. environment variables / secrets

Najważniejsze sekcje konfiguracji:

- `Jwt`
- `Cors`
- `EmailConfirmation`
- `EmailTwoFactor`
- `EmailDelivery`
- `UiFeatures`

## Health Checks And Request Correlation

API udostępnia osobne sygnały operacyjne:

- `GET /health` sprawdza API i bazę danych, z pominięciem workerów;
- `GET /health/live` sprawdza liveness procesu;
- `GET /health/ready` uruchamia `DatabaseHealthCheck` i sprawdza możliwość połączenia z bazą;
- `GET /health/workers` sprawdza ostatni stan workerów outbox i przypomnień o terminach.

Middleware `CorrelationIdMiddleware` odczytuje `X-Correlation-ID` z żądania albo używa identyfikatora wygenerowanego przez ASP.NET Core. Wartość trafia do nagłówka odpowiedzi i kontekstu Seriloga, dzięki czemu można śledzić żądanie w logach.

Workery zapisują ostatni sukces lub błąd w singletonowym `BackgroundWorkerHealthState`. Brak pierwszego raportu albo przekroczenie maksymalnego wieku raportu oznacza stan niezdrowy na `/health/workers`; nie blokuje to liveness procesu.

Ważne zasady:

- sekrety nie trafiają do repozytorium
- CORS jest konfigurowany przez settings, nie hardcode w `Program.cs`
- walidacja opcji odbywa się przy starcie w `AddProjectServices.cs`
- `EmailDelivery.Enabled = false` pozwala lokalnie działać bez zewnętrznego SMTP

## Authentication Flow

Najważniejsze endpointy auth:

- `POST /api/auth/register`
- `POST /api/auth/confirm-email`
- `POST /api/auth/resend-confirmation`
- `POST /api/auth/login`
- `POST /api/auth/verify-2fa`
- `POST /api/auth/resend-2fa`
- `POST /api/auth/refresh-token`
- `POST /api/auth/logout`
- `POST /api/auth/logout-all`
- `GET /api/auth/me`
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`

Typowy flow logowania:

1. `AuthController` przyjmuje request.
2. `DatabaseAuthService` uwierzytelnia użytkownika po emailu i haśle.
3. Jeśli email niepotwierdzony, backend blokuje login.
4. Jeśli 2FA jest aktywne, backend tworzy challenge i odsyła `202 Accepted`.
5. Jeśli 2FA nie jest wymagane, backend zwraca access token i ustawia refresh token cookie.

## Database Model

Aktualny model bazy obejmuje auth, użytkowników oraz domenę projektów i zadań.

Najważniejsze DbSety:

- `Users`
- `RefreshTokens`
- `EmailConfirmationTokens`
- `EmailTwoFactorChallenges`
- `PasswordResetRequests`
- `Projects`
- `ProjectMembers`
- `ProjectTasks`
- `ProjectTaskAttachments`
- `ProjectTaskLabels`
- `Notifications`
- `NotificationEmailOutboxMessages`

Warto zwrócić uwagę na kilka decyzji:

- tokeny są przechowywane jako hashe, nie surowe wartości
- challenge i requesty resetu mają daty wygaśnięcia i liczniki prób
- enumy takie jak `ResetType` są mapowane jako stringi
- relacje do `User` mają `DeleteBehavior.Cascade`

## Runtime Config Endpoint

Frontend bootstrapuje się z endpointu:

- `GET /api/runtime-config`

Kontroler:

- czyta `EmailDeliverySettings`, `EmailTwoFactorSettings`, `UiFeatureSettings`
- składa `AppRuntimeConfigurationDto`
- zwraca tylko dane bezpieczne do ekspozycji w UI

To jest wzorzec projektowy, nie jednorazowy wyjątek.

## How To Add a New Backend Feature

Jeśli dodajesz nową funkcję backendową, trzymaj się tej kolejności:

1. Zacznij od domeny, jeśli feature wprowadza nowe pojęcie biznesowe.
2. Dodaj lub rozszerz DTO i interfejsy w `Application/`.
3. Dodaj implementację w `Infrastructure/`, jeśli feature dotyka bazy lub integracji.
4. Dodaj endpoint lub rozszerz istniejący kontroler w `API/`.
5. Dodaj testy jednostkowe i integracyjne adekwatne do zakresu.

## Naming Guidelines

Kilka praktycznych zasad nazewnictwa:

- kontrolery nazywaj zgodnie z granicą API, np. `AuthController`, `UsersController`
- DTO nazywaj według celu, np. `RegisterUserDto`, `ConfirmEmailRequestDto`
- settings kończ `Settings`, np. `EmailTwoFactorSettings`
- serwisy infrastrukturalne nazywaj po mechanice implementacji, np. `DatabaseAuthService`, `MailKitAccountEmailSender`
- typy współdzielone z frontendem trzymaj w `Shared/` tylko wtedy, gdy naprawdę są wspólnym kontraktem

## What Not To Do

- nie wkładaj logiki biznesowej bezpośrednio do kontrolera
- nie traktuj frontendu jako źródła prawdy dla auth lub ról
- nie dodawaj nowych sekcji konfiguracji bez walidacji przy starcie
- nie duplikuj tego samego kontraktu w kilku miejscach bez potrzeby

## See Also

- `doc/ARCHITECTURE.md` - całościowa architektura i przepływy między backendem i frontendem
- `doc/JWT_ARCHITECTURE.md` - szczegóły sesji, JWT i refresh token rotation
- `doc/EMAIL_2FA_FLOWS.md` - confirm email, email 2FA i reset hasła od strony flow
- `doc/FRONTEND_SETUP.md` - zachowanie klienta, routing i bootstrap po stronie UI
