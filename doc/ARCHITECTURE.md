# Project Architecture

Ten starter to pełny projekt full-stack z backendem ASP.NET Core 9 i frontendem React 19 + TypeScript.
Najważniejszy cel projektu: być stabilną bazą pod aplikacje z autoryzacją, rolami, panelem użytkownika, panelem admina i dalszą rozbudową.

## When To Read This Document

Czytaj ten plik jako pierwszy dokument techniczny, gdy chcesz zrozumieć granice projektu, przepływ informacji oraz odpowiedzialność backendu i frontendu.

## High-Level Overview

Projekt składa się z dwóch głównych części:

- `backend/` - API, logika aplikacyjna, domena, baza danych i integracje
- `frontend/` - interfejs użytkownika, routing, stan sesji, komunikacja z API i feature gating

Do tego dochodzą:

- `doc/` - dokumentacja techniczna i przewodniki
- `docker/` oraz `docker-compose.yml` - uruchamianie środowiska kontenerowego

## Backend Structure

Backend jest podzielony na warstwy:

- `API/` - entry point, kontrolery HTTP, middleware, konfiguracja hosta
- `Application/` - DTO, serwisy aplikacyjne, walidacja, kontrakty
- `Domain/` - encje, enumy, value objects, reguły domenowe
- `Infrastructure/` - EF Core, `ApplicationDbContext`, implementacje serwisów i repozytoriów
- `Shared/` - wspólne response, settings, helpery i DTO współdzielone między backendem i frontendem

Najważniejszy punkt startowy backendu:

- `backend/API/Program.cs` - buduje host, aplikuje migracje, podłącza middleware i mapuje kontrolery
- `backend/API/Services/AddProjectServices.cs` - centralny composition root dla serwisów, opcji, auth, CORS i persistence

## Frontend Structure

Frontend jest zorganizowany według odpowiedzialności:

- `src/components/` - reużywalne komponenty UI i shell aplikacji
- `src/context/` - globalny stan, np. auth i runtime config
- `src/hooks/` - hooki opakowujące logikę dostępu do contextu i feature flags
- `src/pages/` - komponenty stron i ekranów
- `src/services/` - warstwa komunikacji z backendem
- `src/types/` - kontrakty TypeScript dla requestów, response i stanu
- `src/utils/` - walidacja i pomocnicza logika frontendu

Najważniejszy punkt startowy frontendu:

- `frontend/src/App.tsx`

Kolejność bootstrapu jest świadoma:

1. `RuntimeConfigProvider` ładuje konfigurację runtime z backendu.
2. `AuthProvider` odtwarza sesję i ewentualnie odświeża token.
3. `AppBootstrapGate` blokuje render powłoki aplikacji, dopóki bootstrap się nie zakończy.
4. `AppShell` renderuje navbar, powiadomienia i routing.

## Information Flow

Najważniejsze przepływy informacji w projekcie:

### Authentication flow

1. Frontend wysyła żądania auth do `/api/auth/*`.
2. `AuthController` koordynuje logowanie, rejestrację, confirm email, 2FA i refresh token.
3. `DatabaseAuthService` wykonuje logikę auth opartą o `ApplicationDbContext`.
4. Access token trafia do frontendu, a refresh token pozostaje w HttpOnly cookie.
5. `AuthContext` odtwarza sesję podczas startu aplikacji.

### Runtime config flow

1. Backend pobiera ustawienia z `appsettings*.json` i environment variables.
2. `RuntimeConfigController` zwraca bezpieczne feature flags przez `GET /api/runtime-config`.
3. `RuntimeConfigContext` pobiera te dane i normalizuje je do bezpiecznych booleanów.
4. `useFeatureAvailability()` udostępnia prosty interfejs dla komponentów UI.
5. `Navbar`, `AppRoutes`, `Home`, `Dashboard` i inne widoki podejmują decyzje o widoczności elementów.

### User and admin flow

1. Frontend korzysta z protected routes i roli użytkownika zapisanej w stanie auth.
2. Backend weryfikuje JWT i role po stronie API.
3. Frontend używa roli tylko do UX i routingu, nie jako źródła bezpieczeństwa.

## Database Model

Główna baza jest obsługiwana przez EF Core w `backend/Infrastructure/Data/ApplicationDbContext.cs`.

Najważniejsze tabele/encje:

- `Users`
- `RefreshTokens`
- `EmailConfirmationTokens`
- `EmailTwoFactorChallenges`
- `PasswordResetRequests`

Relacje są proste i czytelne:

- tokeny i challenge są przypisane do `User`
- usunięcie użytkownika kaskadowo usuwa powiązane rekordy auth
- tokeny mają indeksy na hashach i datach wygaśnięcia

To oznacza, że baza jest obecnie zorientowana głównie wokół auth i kont użytkowników, a nie rozbudowanej domeny biznesowej.

## Runtime Configuration as a Project Pattern

Ten projekt używa backend-driven UI feature flags.

To znaczy:

- frontend nie trzyma źródła prawdy o feature flags
- wartości przychodzą z backendu
- frontend tylko renderuje UI zgodnie z dostępną konfiguracją

To podejście jest używane do kontrolowania:

- global search
- dashboard overview
- admin navigation
- user management navigation
- email-related sections
- email delivery visibility
- email 2FA availability

## Naming and Organization Rules

W projekcie warto utrzymywać kilka prostych zasad:

- nazwy folderów powinny wynikać z odpowiedzialności, nie z technicznego przypadku użycia
- DTO na backendzie i typy na frontendzie powinny mieć spójne nazwy i podobny kształt
- feature-specific logika powinna być centralizowana, a nie rozrzucana po wielu komponentach
- routing i shell powinny sterować dostępnością sekcji aplikacji
- backend ma pozostać źródłem prawdy dla auth, ról i konfiguracji runtime

## Testing Layers

Projekt ma kilka poziomów testów:

- `backend/UnitTests/` - testy jednostkowe backendu
- `backend/IntegrationTests/` - testy integracyjne API i warstwy persistence
- `backend/E2ETests/` - smoke tests uruchamiane przeciw działającej aplikacji
- `frontend/src/tests/` i testy przy komponentach - testy React + RTL/Jest

## Recommended Reading Order

Jeśli zaczynasz pracę w tym starterze, czytaj w tej kolejności:

1. `README.md`
2. `doc/ARCHITECTURE.md`
3. `doc/JWT_ARCHITECTURE.md`
4. `doc/EMAIL_2FA_FLOWS.md`
5. `doc/BACKEND_SETUP.md`
6. `doc/FRONTEND_SETUP.md`
7. `doc/ADDING_FEATURES.md`

## See Also

- `doc/GETTING_STARTED.md` - uruchomienie projektu i podstawowe komendy
- `doc/BACKEND_SETUP.md` - szczegóły warstw backendu
- `doc/FRONTEND_SETUP.md` - szczegóły struktury frontendu
- `doc/JWT_ARCHITECTURE.md` - sesja, JWT i refresh token rotation
- `doc/EMAIL_2FA_FLOWS.md` - email confirmation, 2FA i reset hasła
- `doc/ADDING_FEATURES.md` - workflow dodawania nowych funkcji