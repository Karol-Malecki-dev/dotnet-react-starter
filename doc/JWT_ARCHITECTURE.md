# Authentication and JWT Architecture

Ten dokument opisuje szczegółowo, jak działa auth flow, JWT i refresh token rotation w tym starterze.

## When To Read This Document

Czytaj ten plik, gdy chcesz zrozumieć:

- jak powstaje sesja po loginie
- gdzie są przechowywane access token i refresh token
- jak działa refresh token rotation
- kiedy backend generuje JWT, a kiedy jeszcze ich nie wystawia

Jeśli interesują Cię głównie confirm email, 2FA i reset hasła, zacznij od `doc/EMAIL_2FA_FLOWS.md`.

## High-Level Model

Projekt używa modelu rozdzielonej sesji:

- access token jest krótkowiecznym JWT używanym do autoryzacji requestów API
- refresh token jest dłużej żyjącym sekretem trzymanym wyłącznie w HttpOnly cookie
- frontend przechowuje tylko access token i snapshot użytkownika
- backend pozostaje źródłem prawdy dla ważności sesji i rotacji refresh tokenów

To podejście zmniejsza ekspozycję refresh tokenu na JavaScript i upraszcza bootstrap sesji po stronie frontendu.

## Core Files

Najważniejsze pliki backendowe:

- `backend/API/Controllers/AuthController.cs`
- `backend/API/Configurations/JwtBearerOptionsSetup.cs`
- `backend/Infrastructure/Services/JwtTokenService.cs`
- `backend/Infrastructure/Services/DatabaseAuthService.cs`
- `backend/Infrastructure/Data/ApplicationDbContext.cs`
- `backend/Shared/Settings/JwtSettings.cs`
- `backend/Domain/Entities/JWT/RefreshToken.cs`
- `backend/Domain/Interfaces/IJwtTokenService.cs`

Najważniejsze pliki frontendowe:

- `frontend/src/context/AuthContext.tsx`
- `frontend/src/services/api/AuthApi.ts`
- `frontend/src/services/api/HttpClient.ts`
- `frontend/src/services/api/TokenManager.ts`
- `frontend/src/components/UI/ProtectedRoute.tsx`

## What Is Stored Where

### Backend

Backend przechowuje w bazie:

- użytkownika
- refresh token hash
- concurrency stamp used to protect rotation updates
- snapshot podstawowych danych użytkownika powiązany z refresh tokenem
- metadane tokenu, takie jak `CreatedAt`, `ExpiresAt`, `LastUsedAt`, `RevokedAt`, `CreatedByIp`, `LastUsedByIp`

Backend nie przechowuje surowej wartości refresh tokenu.

### Frontend

Frontend przechowuje:

- access token w localStorage
- user snapshot w localStorage
- tymczasowy stan 2FA w sessionStorage

Frontend nie ma dostępu do refresh tokenu, bo ten jest ustawiany w cookie z `HttpOnly=true`.

## JWT Configuration Model

JWT i refresh cookie są konfigurowane przez `JwtSettings`.

Najważniejsze ustawienia:

- `Secret`
- `Issuer`
- `Audience`
- `AccessTokenExpiresInMinutes`
- `RefreshTokenExpiresInDays`
- `RefreshTokenCookieName`
- `RefreshTokenCookiePath`
- `RefreshTokenCookieSameSite`
- `RefreshTokenCookieSecurePolicy`
- `RefreshTokenCookieDomain`
- `RefreshTokenCookieIsEssential`

Domyślny model w tym starterze:

- access token wygasa szybko, domyślnie po 15 minutach
- refresh token wygasa po 7 dniach
- refresh cookie jest ograniczone ścieżką `/api/auth`

## Claims Model

Access token zawiera między innymi te claims:

- `sub` - identyfikator użytkownika
- `jti` - unikalny identyfikator tokenu
- `nameid` - duplikacja identyfikatora dla zgodności z częścią kodu
- `email`
- `name`
- `role`
- `IsEmailConfirmed`

Ważne szczegóły implementacyjne:

- `JwtBearerOptionsSetup` ustawia `MapInboundClaims = false`
- dzięki temu zachowywane są oryginalne nazwy claimów, np. `sub`
- `NameClaimType = sub`
- `RoleClaimType = role`
- `ClockSkew = TimeSpan.Zero`

To oznacza, że token nie ma dodatkowej tolerancji czasowej i jest interpretowany możliwie dosłownie.

## Authentication Flow

### Registration

1. Frontend wywołuje `POST /api/auth/register`.
2. Backend tworzy użytkownika z `IsEmailConfirmed = false`.
3. Backend generuje token potwierdzenia emaila.
4. Backend wysyła link potwierdzający.
5. Użytkownik nie może się zalogować, dopóki email nie zostanie potwierdzony.

### Confirm Email

1. Frontend otwiera ekran potwierdzenia z `userId` i `token` z URL.
2. Frontend wywołuje `POST /api/auth/confirm-email`.
3. Backend waliduje token potwierdzenia.
4. Jeśli token jest poprawny i niewygasły, użytkownik dostaje `IsEmailConfirmed = true`.

### Login Without 2FA

1. Frontend wywołuje `POST /api/auth/login` z emailem i hasłem.
2. `DatabaseAuthService` uwierzytelnia użytkownika.
3. Jeśli dane są poprawne i 2FA nie jest wymagane, `JwtTokenService` generuje:
   - access token
   - refresh token
4. Backend zapisuje hash refresh tokenu w bazie.
5. Backend ustawia refresh token cookie.
6. Backend zwraca access token i `expiresIn` w response body.
7. Frontend zapisuje access token lokalnie i pobiera `/api/auth/me`.

### Login With 2FA

1. Frontend wywołuje `POST /api/auth/login`.
2. Backend uwierzytelnia email i hasło.
3. Jeśli użytkownik ma aktywne email 2FA, backend nie zwraca jeszcze tokenów.
4. Backend tworzy `EmailTwoFactorChallenge` i wysyła kod na email.
5. Backend zwraca `202 Accepted` z `challengeId`, `destinationHint` i `expiresAt`.
6. Frontend zapisuje pending challenge i przekierowuje do ekranu weryfikacji 2FA.
7. Frontend wywołuje `POST /api/auth/verify-2fa`.
8. Dopiero po poprawnej weryfikacji backend generuje access token i refresh token.

## Refresh Token Rotation Flow

To jest jeden z najważniejszych wzorców bezpieczeństwa w tym projekcie.

### Jak działa refresh

1. Frontend wywołuje `POST /api/auth/refresh-token` bez ręcznego podawania tokenu.
2. Backend czyta refresh token z cookie.
3. `JwtTokenService.RefreshTokensAsync()` hash'uje wartość i szuka tokenu w bazie.
4. Jeśli token jest aktywny, backend generuje nową parę tokenów.
5. Stary refresh token zostaje oznaczony jako revoked z powodem `TokenRotated`.
6. Stary rekord dostaje `ReplacedByTokenHash` nowego tokenu.
7. Backend ustawia nowe cookie i zwraca nowy access token.

### Co to daje

- refresh token jest jednorazowy w praktyce rotacyjnej
- każde odświeżenie zostawia ślad audytowy
- można wykrywać zużyte lub ponownie użyte tokeny
- sesja jest odnawiana bez ekspozycji refresh tokenu na frontend

### Current-account and concurrency rules

Before rotation, the backend reads the current `User` record. The refresh-token snapshot remains useful for audit, but it is not used as the source of truth for account activity, role, email confirmation, or profile data.

The token family keeps one stable `FamilyId`. The previous row is marked `TokenRotated` and points to the successor through `ReplacedByTokenHash`. `ConcurrencyStamp` is an EF Core concurrency token, so two requests cannot both successfully rotate the same row. The losing request is rejected.

If a rotated token is presented again, the request is treated as refresh-token replay. Active tokens in the family, including the successor, are revoked with `RefreshTokenReplay`. This intentionally favors terminating the family over silently accepting an ambiguous session.

Refresh rejects a missing user or an inactive user. A role change is read on the next refresh, while an already issued access token keeps its claims until its normal expiry.

The complete session-policy decision is recorded in [`doc/ROADMAP/09_ADR_AUTH_SESSION_POLICY.md`](ROADMAP/09_ADR_AUTH_SESSION_POLICY.md).

## Logout Flow

1. Frontend wywołuje `POST /api/auth/logout` jako użytkownik zalogowany.
2. Backend czyta refresh token z cookie.

3. `JwtTokenService.RevokeTokenAsync()` oznacza token jako revoked z powodem `UserLogout`.
4. Backend czyści refresh cookie.
5. Frontend czyści localStorage z access tokenu i snapshotu usera.

### Logout All and Credential Changes

`POST /api/auth/logout-all` revokes all active refresh sessions for the authenticated user and clears the current refresh cookie. It does not invalidate an already issued access token before its expiry.

After a successful password change, all active refresh sessions are revoked with `PasswordChanged`. After a successful password reset, they are revoked with `PasswordReset`. Both operations clear the current browser cookie after success, while other already issued access tokens remain valid until expiry.

## Session Bootstrap on the Frontend

Po restarcie aplikacji frontend próbuje odtworzyć sesję w `AuthContext`.

### Scenariusz 1: access token nadal ważny

1. `TokenManager` odczytuje access token z localStorage.
2. Jeśli token nie jest przeterminowany, frontend odtwarza stan auth lokalnie.

### Scenariusz 2: access token wygasł, ale refresh cookie nadal żyje

1. `AuthContext` wykrywa przeterminowany access token.
2. Frontend wywołuje `authApi.refreshToken()`.
3. Backend używa refresh cookie do wystawienia nowego access tokenu.
4. Frontend zapisuje nowy access token.
5. Frontend wywołuje `authApi.me()` i odświeża snapshot użytkownika.

### Scenariusz 3: sesji nie da się odnowić

1. Refresh token jest nieważny, wygasły albo cofnięty.
2. Backend zwraca `401` i czyści cookie.
3. Frontend czyści swój lokalny stan sesji.
4. Aplikacja wraca do stanu niezalogowanego.

## `/me` Endpoint

`GET /api/auth/me` jest chronionym endpointem do odczytu aktualnego użytkownika.

Po co istnieje:

- frontend nie ufa wyłącznie lokalnemu snapshotowi
- po loginie i refreshu można pobrać aktualne dane użytkownika
- zmiany w profilu i roli mogą zostać odświeżone z backendu

`AuthController` odczytuje user id z:

1. `sub`
2. fallback do `ClaimTypes.NameIdentifier`

To jest zgodne z aktualną konfiguracją claimów i testami integracyjnymi.

## Cookie Model

Refresh token cookie jest tworzone z opcjami zależnymi od `JwtSettings`.

Najważniejsze właściwości:

- `HttpOnly = true`
- `Path = /api/auth`
- `SameSite` konfigurowane przez settings
- `Secure` zależne od `RefreshTokenCookieSecurePolicy`
- opcjonalny `Domain`
- `IsEssential` dla polityk consentu

Praktyczna konsekwencja:

- przeglądarka automatycznie wysyła refresh token tylko do endpointów auth na właściwej ścieżce
- frontend nie musi nim ręcznie zarządzać

## Frontend HTTP Behavior

`HttpClient` po stronie frontendu:

- wysyła `credentials: 'include'`
- automatycznie dokłada `Authorization: Bearer <accessToken>` dla zwykłych requestów
- przy `401` może uruchomić próbę odświeżenia sesji
- po udanym refreshu powtarza pierwotny request jeden raz

To oznacza, że zwykłe komponenty i klienty API nie muszą implementować własnej logiki token refresh.

## Security Properties of This Design

Mocne strony tego podejścia:

- refresh token nie jest dostępny dla JavaScript
- refresh tokeny są hashowane w bazie
- rotacja ogranicza skutki wycieku pojedynczego refresh tokenu
- access token jest krótkowieczny
- backend prowadzi prosty audit trail użycia refresh tokenów
- równoległa rotacja jednego refresh tokenu kończy się najwyżej jednym zaakceptowanym następcą
- replay cofa aktywną rodzinę refresh tokenów

## Limits and Trade-Offs

Warto znać też kompromisy:

- access token nadal siedzi w localStorage, więc XSS pozostaje ważnym ryzykiem
- cookie-path jest ograniczone do `/api/auth`, więc inne endpointy nie mogą używać refresh tokenu bezpośrednio
- `ClockSkew = 0` poprawia przewidywalność, ale wymaga sensownej synchronizacji czasu
- snapshot danych usera przy refresh tokenie służy audytowi, ale bieżący rekord usera jest wymagany przy refreshu
- logout-all, zmiana/reset hasła i dezaktywacja nie cofają już wydanego access JWT; do tego potrzebny byłby token version, deny-list albo introspection
- po re-aktywacji konta niewygasły refresh token może ponownie stać się użyteczny, ponieważ ta wersja blokuje refresh podczas dezaktywacji, ale nie usuwa historycznych rekordów

## How To Extend Auth Safely

Jeśli rozwijasz auth flow, trzymaj się tych zasad:

1. Nie przenoś refresh tokenu do localStorage ani response state frontendu.
2. Zachowuj rotację refresh tokenów przy każdym odświeżeniu.
3. Jeśli dodajesz nowe claims, sprawdź wpływ na frontend i endpoint `/me`.
4. Jeśli dodajesz nowe cookie settings, waliduj je przy starcie aplikacji.
5. Jeśli rozszerzasz flow 2FA albo reset hasła, opisuj jasno, na którym etapie tokeny są już generowane, a na którym jeszcze nie.

## Recommended Reading After This Document

Po tym pliku najlepiej czytać:

1. `doc/EMAIL_2FA_FLOWS.md`
2. `doc/BACKEND_SETUP.md`
3. `doc/FRONTEND_SETUP.md`
4. `doc/ADDING_FEATURES.md`

## See Also

- `doc/ARCHITECTURE.md` - szeroki obraz projektu i przepływów informacji
- `doc/EMAIL_2FA_FLOWS.md` - confirm email, 2FA i reset hasła bez zagłębiania się w sesję JWT
- `doc/BACKEND_SETUP.md` - warstwy backendu, konfiguracja i endpointy auth
- `doc/FRONTEND_SETUP.md` - bootstrap sesji, routing i zachowanie klienta API
