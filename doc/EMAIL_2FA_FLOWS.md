# Email and 2FA Flows

Ten dokument rozbija auth na mniejsze przepływy związane z emailem, potwierdzeniem konta, 2FA i resetem hasła.

## When To Read This Document

Czytaj ten plik, gdy chcesz zrozumieć:

- jak działa confirm email po rejestracji
- jak backend tworzy i odnawia challenge 2FA
- jak działa forgot password i reset password
- które flow są specjalnie zaprojektowane pod neutralne komunikaty i ochronę przed user enumeration

Jeśli interesuje Cię głównie model sesji, cookie refresh tokenu i rotacja JWT, zacznij od `doc/JWT_ARCHITECTURE.md`.

## Scope

Ten plik skupia się na flow, które używają wiadomości email lub kodów wysyłanych na email:

- email confirmation po rejestracji
- resend confirmation
- email-based two-factor authentication
- resend 2FA code
- forgot password
- reset password

Nie opisuje szczegółowo JWT i refresh token rotation. To jest opisane w `doc/JWT_ARCHITECTURE.md`.

## Core Backend Files

Najważniejsze pliki backendowe dla tych flow:

- `backend/API/Controllers/AuthController.cs`
- `backend/Infrastructure/Services/DatabaseAuthService.cs`
- `backend/Application/Interfaces/IAccountEmailSender.cs`
- `backend/Infrastructure/Services/MailKitAccountEmailSender.cs`
- `backend/Infrastructure/Services/LoggingAccountEmailSender.cs`
- `backend/Shared/Settings/EmailConfirmationSettings.cs`
- `backend/Shared/Settings/EmailTwoFactorSettings.cs`
- `backend/Shared/Settings/EmailDeliverySettings.cs`

## Core Frontend Files

Najważniejsze pliki frontendowe dla tych flow:

- `frontend/src/pages/ConfirmEmail.tsx`
- `frontend/src/pages/VerifyTwoFactor.tsx`
- `frontend/src/pages/ForgotPassword.tsx`
- `frontend/src/pages/ResetPassword.tsx`
- `frontend/src/services/api/AuthApi.ts`
- `frontend/src/context/AuthContext.tsx`
- `frontend/src/utils/authSchemas.ts`

## Email Delivery Modes

Projekt ma dwa tryby zachowania dla wiadomości wychodzących:

### Real delivery mode

Gdy `EmailDelivery.Enabled = true`:

- backend używa `MailKitAccountEmailSender`
- wiadomości są wysyłane przez skonfigurowany SMTP
- wymagane są poprawne ustawienia hosta, portu i nadawcy

### Safe local mode

Gdy `EmailDelivery.Enabled = false`:

- backend używa `LoggingAccountEmailSender`
- aplikacja nadal przechodzi przez flow emailowe
- zamiast realnej wysyłki logowana jest treść wiadomości lub uruchamiany jest lokalny sink

To pozwala rozwijać flow bez prawdziwego providera email.

## Email Confirmation Flow

### Cel

Nowe konto ma zostać aktywowane dopiero po potwierdzeniu adresu email.

### Backend flow

1. `POST /api/auth/register` tworzy użytkownika.
2. Użytkownik dostaje `IsEmailConfirmed = false`.
3. `DatabaseAuthService.GenerateEmailConfirmationTokenAsync()` tworzy nowy token potwierdzający.
4. Stare aktywne tokeny potwierdzające dla tego użytkownika są cofane.
5. `AuthController` buduje link potwierdzający z `userId` i `token`.
6. `IAccountEmailSender.SendEmailConfirmationAsync()` wysyła wiadomość.

### Frontend flow

1. Użytkownik przechodzi do `/confirm-email` z parametrami z linku.
2. `ConfirmEmail.tsx` odczytuje `userId` i `token` z query string.
3. Frontend wywołuje `authApi.confirmEmail()`.
4. Przy sukcesie pokazuje potwierdzenie aktywacji.
5. Jeśli token jest niepoprawny lub wygasły, użytkownik może użyć formularza resend.

### Ważne zachowanie

- użytkownik nie zaloguje się bez potwierdzonego adresu email
- token potwierdzenia jest jednorazowy
- starsze aktywne tokeny są cofane po wygenerowaniu nowego

## Resend Confirmation Flow

### Cel

Umożliwić ponowne wysłanie linku aktywacyjnego, jeśli poprzedni zginął lub wygasł.

### Backend flow

1. Frontend wywołuje `POST /api/auth/resend-confirmation`.
2. Backend szuka użytkownika po emailu.
3. Jeśli konto istnieje i nie jest jeszcze potwierdzone, backend generuje nowy token.
4. Backend wysyła nową wiadomość z linkiem.
5. Response jest celowo ogólny, aby nie ujawniać, czy konto istnieje.

### Frontend flow

1. `ConfirmEmail.tsx` pokazuje formularz resend confirmation.
2. Użytkownik podaje email.
3. Frontend wywołuje `authApi.resendConfirmation()`.
4. UI pokazuje neutralny komunikat sukcesu.

## Email 2FA Flow

### Cel

Po poprawnym loginie użytkownik ma dodatkowo potwierdzić dostęp kodem wysłanym na email.

### Kiedy 2FA jest używane

2FA zależy od:

- `EmailTwoFactorSettings.Enabled`
- ustawienia `user.IsTwoFactorEnabled`

Dla nowych użytkowników `IsTwoFactorEnabled` może zostać ustawione podczas rejestracji na podstawie `EnableForNewUsers`.

### Backend login flow with 2FA

1. Użytkownik wysyła email i hasło przez `POST /api/auth/login`.
2. Backend potwierdza poprawność hasła.
3. Jeśli 2FA jest wymagane, backend nie wystawia jeszcze tokenów JWT.
4. `DatabaseAuthService.CreateEmailTwoFactorChallengeAsync()` generuje kod i challenge.
5. Challenge jest zapisywany w bazie z `ExpiresAt`, `FailedAttempts` i powiązaniem do użytkownika.
6. `IAccountEmailSender.SendTwoFactorCodeAsync()` wysyła kod.
7. Backend zwraca `202 Accepted` z `challengeId`, `destinationHint` i `expiresAt`.

### Frontend flow

1. Frontend dostaje odpowiedź typu two-factor-required.
2. Challenge jest przechowywany po stronie klienta jako tymczasowy stan.
3. Użytkownik trafia na `/verify-2fa`.
4. `VerifyTwoFactor.tsx` pokazuje formularz kodu i czas wygaśnięcia.
5. Frontend wywołuje `authApi.verifyTwoFactor()`.
6. Dopiero wtedy backend zwraca access token i ustawia refresh cookie.

### Ważne zachowanie

- sam login z hasłem nie daje jeszcze sesji, jeśli 2FA jest aktywne
- challenge ma własny czas życia
- challenge może wygasnąć niezależnie od dalszego flow JWT

## Resend 2FA Code Flow

### Cel

Użytkownik ma dostać nowy kod, jeśli poprzedni wygasł albo nie dotarł.

### Backend flow

1. Frontend wywołuje `POST /api/auth/resend-2fa` z `challengeId`.
2. Backend weryfikuje, czy challenge nadal jest poprawny do odnowienia.
3. Backend generuje nowy kod i aktualizuje challenge.
4. Backend wysyła nową wiadomość email.
5. Frontend dostaje odświeżone `expiresAt` i `destinationHint`.

### Frontend flow

1. `VerifyTwoFactor.tsx` ma przycisk resend.
2. Użytkownik może poprosić o nowy kod bez wracania do loginu.
3. Frontend aktualizuje lokalny challenge po udanym resend.

## Forgot Password Flow

### Cel

Pozwolić użytkownikowi rozpocząć reset hasła bez ujawniania, czy konto istnieje.

### Obecny wariant

Aktualnie backend wspiera link-based reset password.

To znaczy:

- frontend wysyła `ResetType.Link`
- backend nie obsługuje obecnie pełnego code-based reset password dla publicznego flow

### Backend flow

1. Frontend wywołuje `POST /api/auth/forgot-password` z emailem.
2. Backend normalizuje email.
3. Jeśli użytkownik istnieje, tworzy `PasswordResetRequest`.
4. Stare aktywne requesty resetu dla użytkownika są cofane.
5. Tworzony jest bezpieczny token, którego hash trafia do bazy.
6. Response dla klienta pozostaje neutralny.

### Ważne zachowanie

- flow ma ograniczać user enumeration
- surowy token resetu nie jest przechowywany w bazie
- starsze aktywne requesty resetu są unieważniane

## Reset Password Flow

### Cel

Pozwolić użytkownikowi ustawić nowe hasło na podstawie poprawnego linku resetującego.

### Backend flow

1. Frontend wywołuje `POST /api/auth/reset-password`.
2. Backend wyszukuje użytkownika po emailu.
3. Backend hash'uje token z requestu.
4. Szukany jest aktywny `PasswordResetRequest` typu `Link`.
5. Jeśli token jest poprawny i niewygasły, hasło użytkownika zostaje zmienione.
6. Użyty request dostaje `ConsumedAt`.
7. Pozostałe aktywne requesty resetu dla tego użytkownika są cofane.

### Frontend flow

1. `ResetPassword.tsx` zbiera email, token, hasło i confirmPassword.
2. Token jest zwykle pobierany z linku resetującego.
3. Frontend wywołuje `authApi.resetPassword()`.
4. Przy sukcesie użytkownik może wrócić do loginu.

## Failure Modes Worth Knowing

Najczęstsze przypadki błędów w tych flow:

- email confirmation token wygasł
- email confirmation token został cofnięty po wygenerowaniu nowego
- 2FA challenge wygasł
- 2FA code jest błędny
- 2FA challenge przekroczył limit prób
- password reset token wygasł
- password reset token został zużyty
- password reset token został cofnięty przez nowszy request
- email delivery jest wyłączone lub źle skonfigurowane

## Data Objects Behind the Flows

Najważniejsze rekordy persistence wspierające te przepływy:

- `EmailConfirmationToken`
- `EmailTwoFactorChallenge`
- `PasswordResetRequest`

Wspólne cechy tych rekordów:

- są powiązane z użytkownikiem
- mają daty wygaśnięcia
- mogą być consumed lub revoked
- są projektowane jako elementy jednorazowe lub krótkowieczne

## UI Entry Points

Najważniejsze ekrany po stronie frontendu:

- `/confirm-email`
- `/verify-2fa`
- `/forgot-password`
- `/reset-password`

To są osobne etapy flow, a nie jeden wspólny ekran auth.

## Extension Guidelines

Jeśli rozwijasz któryś z tych flow, trzymaj się tych zasad:

1. Nie mieszaj etapu email confirmation z etapem login session.
2. Nie generuj JWT przed zakończeniem wymaganego 2FA.
3. Zachowuj neutralne komunikaty tam, gdzie flow nie powinien ujawniać istnienia konta.
4. Każdy nowy token albo code-based flow powinien mieć `ExpiresAt`, możliwość revocation i czytelny status zużycia.
5. Jeśli dodajesz nowy email template albo nowy kanał dostarczania, nie zmieniaj publicznego kontraktu endpointów bez potrzeby.

## Recommended Reading After This Document

Po tym pliku najlepiej czytać:

1. `doc/JWT_ARCHITECTURE.md`
2. `doc/BACKEND_SETUP.md`
3. `doc/FRONTEND_SETUP.md`
4. `doc/ADDING_FEATURES.md`

## See Also

- `doc/ARCHITECTURE.md` - ogólna mapa projektu i głównych przepływów
- `doc/JWT_ARCHITECTURE.md` - model sesji, refresh token cookie i rotacja tokenów
- `doc/BACKEND_SETUP.md` - endpointy auth, persistence i konfiguracja backendu
- `doc/FRONTEND_SETUP.md` - ekrany auth, bootstrap UI i feature gating
