# Snapshot faktow projektu

## Cel pliku

Ten plik zbiera dane ze struktury repozytorium, kodu, konfiguracji i istniejących testów
na potrzeby późniejszej analizy rozwoju produktu. Nie jest backlogiem, raportem gotowości
produkcyjnej ani rekomendacją implementacji.

## Metadane zebrania

- Projekt: `dotnet-react-starter`
- Workspace: That is private information 
- Data snapshotu: `2026-09-21`
- Zakres: backend ASP.NET Core/.NET, frontend React/TypeScript, testy, Compose i deployment VPS.
- Tryb: analiza statyczna plików repozytorium.
- Uruchomienie Docker Compose: nie wykonano podczas zbierania tego snapshotu.
- Build, testy i browser E2E: nie wykonano podczas zbierania tego snapshotu.
- Sekrety i rzeczywiste dane środowiskowe: nie są kopiowane do tego pliku.

## 1. Kształt rozwiązania

### Backend

- Backend jest podzielony na projekty `API`, `Application`, `Domain`, `Infrastructure` i `Shared`.
- Testy backendu są w osobnych projektach `UnitTests`, `IntegrationTests` i `E2ETests`.
- Docelowy framework projektów backendowych: `.NET 9`.
- Persistence: Entity Framework Core i PostgreSQL.
- Główny kontekst persistence: `Infrastructure.Data.ApplicationDbContext`.
- Organizacja kodu łączy warstwy techniczne z modułami i vertical slices.
- Zarejestrowane moduły infrastrukturalne obejmują `Projects`, `ProjectTasks` i `Notifications`.
- Search workspace jest osobnym obszarem pod `Infrastructure.Modules.Workspace.SearchWorkspace`.

Źródła:

- `backend/backend.slnx`
- `backend/API/API.csproj`
- `backend/Application/Application.csproj`
- `backend/Domain/Domain.csproj`
- `backend/Infrastructure/Infrastructure.csproj`
- `backend/Shared/Shared.csproj`
- `backend/Infrastructure/Data/ApplicationDbContext.cs`
- `backend/Infrastructure/Modules/Projects/ProjectsModule.cs`
- `backend/Infrastructure/Modules/ProjectTasks/ProjectTasksModule.cs`
- `backend/Infrastructure/Modules/Notifications/NotificationsModule.cs`

### Frontend

- Frontend używa React `19.2.x`, TypeScript `5.7.x`, Vite `6.3.x` i React Router `7.18.x`.
- Testy komponentów i stron używają Vitest oraz Testing Library.
- Browser E2E używa Playwright `1.62.x`.
- Główne konteksty stanu obejmują auth, projekty, powiadomienia i konfigurację runtime.

Źródła:

- `frontend/package.json`
- `frontend/src/App.tsx`
- `frontend/src/components/AppRoutes.tsx`
- `frontend/src/context/AuthContext.tsx`
- `frontend/src/context/ProjectsContext.tsx`
- `frontend/src/context/NotificationsContext.tsx`

## 2. Model domeny

### Encje

- `User`
- `RefreshToken`
- `AuthenticatorLoginChallenge`
- `AuthenticatorRecoveryCode`
- `Project`
- `ProjectMember`
- `ProjectInvitation`
- `ProjectTask`
- `ProjectTaskLabel`
- `ProjectTaskComment`
- `ProjectTaskAttachment`
- `ProjectTaskAttachmentCleanupMessage`
- `ProjectTaskDeadlineReminder`
- `ProjectActivity`
- `Notification`
- `NotificationEmailPreference`
- `NotificationEmailOutboxMessage`
- `AccountSecurityEvent`
- `BaseEntity`

Źródła: `backend/Domain/Entities/` oraz `backend/Domain/Entities/JWT/RefreshToken.cs`.

### Role i statusy

- Role systemowe: `User`, `Admin`.
- Role członka projektu: `Owner`, `Member`, `Viewer`.
- Statusy zaproszenia projektu są modelowane przez `ProjectInvitationStatus`.
- Statusy zadań są modelowane przez `ProjectTaskStatus`.
- Priorytety zadań są modelowane przez `ProjectTaskPriority`.
- Typy przypomnienia terminu zadania są modelowane przez `ProjectTaskDeadlineReminderType`.
- Powody unieważnienia sesji są modelowane przez `RevocationReason`.

Źródła: `backend/Domain/Enums/`.

### Granice agregatów i zachowania domenowe zaobserwowane w kodzie

- `Project` przechowuje właściciela, członków, zaproszenia, archiwizację, nazwę, opis i daty zmian.
- `Project` udostępnia operacje dodania członka, zmiany roli, usunięcia członka, zmiany nazwy, zmiany opisu i archiwizacji.
- Właściciel projektu jest tworzony jako członek z rolą `Owner`.
- Właściciela nie można usunąć ani zmienić mu roli przez metody domenowe projektu.
- `ProjectTask` przechowuje tytuł, opis, status, priorytet, termin, autora, przypisanego użytkownika i etykiety.
- `ProjectTask` udostępnia operacje zmiany tytułu, opisu, priorytetu, statusu, przypisania, terminu i etykiet.
- Etykiety zadania są normalizowane, deduplikowane, sortowane i ograniczone do 10 elementów.
- `User` zawiera dane konta, rolę systemową, status aktywności, potwierdzenie emaila, ustawienia 2FA,
  dane lockout i stempel współbieżności.

Źródła:

- `backend/Domain/Entities/Project.cs`
- `backend/Domain/Entities/ProjectTask.cs`
- `backend/Domain/Entities/User.cs`
- `backend/Domain/Entities/ProjectMember.cs`

## 3. Tożsamość i autoryzacja

### Zaimplementowane przepływy backendowe

- rejestracja;
- potwierdzenie adresu email;
- ponowne wysłanie potwierdzenia;
- logowanie hasłem;
- email 2FA;
- TOTP/authenticator 2FA;
- recovery codes;
- refresh-token rotation;
- wykrywanie replay refresh tokenu;
- logout pojedynczej sesji;
- wylogowanie wszystkich sesji;
- zmiana hasła;
- forgot password;
- reset password;
- odczyt bieżącego użytkownika;
- zarządzanie użytkownikami przez administratora;
- aktywacja i dezaktywacja konta;
- lockout po nieudanych próbach logowania.

### Widoczne poziomy dostępu

- Endpointy publiczne auth mają `AllowAnonymous`, gdy wymagają tego przepływy rejestracji,
  potwierdzenia lub odzyskiwania konta.
- Pozostałe endpointy auth wymagają zalogowania.
- Endpointy administracyjne używają roli systemowej `Admin`.
- Operacje projektu i zadań wymagają zalogowania, a dodatkowe reguły członkostwa są sprawdzane
  w handlerach i store'ach modułów.
- Frontend ma `ProtectedRoute` oraz ochronę tras administracyjnych przez `allowedRoles`.
- Frontendowe zabezpieczenie tras nie jest jedyną granicą bezpieczeństwa; backend posiada własną autoryzację.

Źródła:

- `backend/API/Controllers/AuthController.cs`
- `backend/API/Controllers/AdminController.cs`
- `backend/API/Controllers/UsersController.cs`
- `backend/API/Modules/Projects/`
- `backend/API/Modules/ProjectTasks/`
- `frontend/src/components/UI/ProtectedRoute.tsx`
- `frontend/src/components/AppRoutes.tsx`

## 4. Powierzchnia API

Poniżej zebrano rodziny tras widoczne w kontrolerach. Wszystkie trasy z wyjątkiem jawnie
oznaczonych jako publiczne są chronione przez `Authorize` albo przez reguły modułu.

### Auth i konto

- `POST /api/auth/login` - publiczne logowanie.
- `POST /api/auth/register` - publiczna rejestracja.
- `POST /api/auth/confirm-email` - publiczne potwierdzenie emaila.
- `POST /api/auth/resend-confirmation` - publiczne ponowienie potwierdzenia.
- `POST /api/auth/verify-2fa` - publiczna weryfikacja wyzwania 2FA.
- `POST /api/auth/resend-2fa` - publiczne ponowienie wyzwania 2FA.
- `POST /api/auth/authenticator/setup` - konfiguracja authenticator dla zalogowanego użytkownika.
- `POST /api/auth/authenticator/confirm` - potwierdzenie authenticator.
- `POST /api/auth/authenticator/disable` - wyłączenie authenticator.
- `POST /api/auth/authenticator/recovery-codes` - wygenerowanie recovery codes.
- `POST /api/auth/refresh-token` - odświeżenie sesji.
- `POST /api/auth/logout` - wylogowanie bieżącej sesji.
- `POST /api/auth/logout-all` - unieważnienie wszystkich sesji.
- `GET /api/auth/me` - bieżący użytkownik.
- `POST /api/auth/verify-token` - weryfikacja tokenu.
- `POST /api/auth/change-password` - zmiana hasła.
- `POST /api/auth/forgot-password` - rozpoczęcie resetu hasła.
- `POST /api/auth/reset-password` - zakończenie resetu hasła.

### Administracja i użytkownicy

- `GET /api/admin/dashboard-stats`
- `GET /api/admin/users`
- `GET /api/admin/security-events`
- `GET /api/admin/users/{userId}`
- `GET /api/admin/users/by-email`
- `PUT /api/admin/users/{userId}`
- `PUT /api/admin/users/{userId}/role`
- `PUT /api/admin/users/{userId}/activate`
- `PUT /api/admin/users/{userId}/deactivate`
- `DELETE /api/admin/users/{userId}`
- `GET /api/users/{id}`
- `GET /api/users`
- `GET /api/users/me`
- `GET /api/users/count`
- `DELETE /api/users/{id}`
- `PUT /api/users/{id}/display-name`
- `PUT /api/users/{id}/role`
- `PUT /api/users/me`
- `GET /api/users/me/security`
- `PATCH /api/users/me/security/two-factor`

### Projekty i członkostwo

- `POST /api/projects`
- `GET /api/projects`
- `GET /api/projects/{projectId}`
- `PUT /api/projects/{projectId}`
- `DELETE /api/projects/{projectId}`
- `GET /api/projects/{projectId}/dashboard`
- `GET /api/projects/{projectId}/activity`
- `GET /api/projects/{projectId}/members`
- `GET /api/projects/{projectId}/members/available`
- `POST /api/projects/{projectId}/members`
- `PATCH /api/projects/{projectId}/members/{userId}/role`
- `DELETE /api/projects/{projectId}/members/{userId}`
- `GET /api/projects/{projectId}/invitations`
- `POST /api/projects/{projectId}/invitations`
- `GET /api/project-invitations/mine`
- `POST /api/project-invitations/accept`
- `POST /api/project-invitations/decline`

### Zadania, komentarze i załączniki

- `GET /api/projects/{projectId}/tasks`
- `POST /api/projects/{projectId}/tasks`
- `GET /api/projects/{projectId}/tasks/{taskId}`
- `PUT /api/projects/{projectId}/tasks/{taskId}`
- `PATCH /api/projects/{projectId}/tasks/{taskId}/status`
- `DELETE /api/projects/{projectId}/tasks/{taskId}`
- `GET /api/projects/{projectId}/tasks/{taskId}/comments`
- `POST /api/projects/{projectId}/tasks/{taskId}/comments`
- `DELETE /api/projects/{projectId}/tasks/{taskId}/comments/{commentId}`
- `GET /api/projects/{projectId}/tasks/{taskId}/attachments`
- `POST /api/projects/{projectId}/tasks/{taskId}/attachments`
- `GET /api/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}/download`
- `DELETE /api/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}`

### Powiadomienia

- `GET /api/notifications`
- `GET /api/notifications/unread-count`
- `GET /api/notifications/email-preference`
- `PATCH /api/notifications/{id}/read`
- `PATCH /api/notifications/read-all`
- `PATCH /api/notifications/email-preference`

### Workspace i konfiguracja

- `GET /api/workspace/search`
- `GET /api/runtime-config` - publiczna konfiguracja runtime frontendowej aplikacji.
- `GET /health`
- `GET /health/live`
- `GET /health/ready`
- `GET /health/workers`
- `GET /health/storage`
- `GET /health/malware-scanner`
- `GET /health/email`

Źródła tras:

- `backend/API/Controllers/`
- `backend/API/Modules/`
- `backend/API/Program.cs`

## 5. Powiadomienia, activity i dostawa email

### Trwały model powiadomienia

Encja `Notification` przechowuje między innymi:

- `UserId` odbiorcy;
- `NotificationType`;
- tytuł i wiadomość;
- opcjonalny `ResourceType` i `ResourceId`;
- opcjonalny `ProjectId`;
- opcjonalny `DeduplicationKey`;
- `CreatedAt`;
- opcjonalny `ReadAt`.

Backendowy enum `NotificationType` zawiera 12 wartości:

- `ProjectInvitation`;
- `TaskAssigned`;
- `SecurityAlert`;
- `System`;
- `TaskDeadlineApproaching`;
- `TaskOverdue`;
- `ProjectMemberRemoved`;
- `ProjectMemberRoleChanged`;
- `TaskStatusChanged`;
- `TaskCommented`;
- `TaskAttachmentAdded`;
- `TaskAttachmentRemoved`.

Frontendowy enum `NotificationType` zawiera obecnie sześć pierwszych wartości z tej listy:
`ProjectInvitation`, `TaskAssigned`, `SecurityAlert`, `System`, `TaskDeadlineApproaching`
i `TaskOverdue`.

### Frontendowy model obsługi

- `NotificationsContext` pobiera listę powiadomień przez API.
- Context przechowuje listę, `unreadCount`, stan ładowania i błąd.
- Dostępne operacje klienta to odświeżenie, oznaczenie pojedynczego powiadomienia jako przeczytanego
  i oznaczenie wszystkich jako przeczytanych.
- W inspected source nie ma klienta SignalR ani SSE.
- Nie ma w tym przepływie widocznego mechanizmu real-time push ani replay po reconnect.

### Email outbox

`NotificationEmailOutboxMessage` przechowuje między innymi:

- identyfikator wiadomości i powiadomienia;
- odbiorcę;
- czas utworzenia i `NextAttemptAt`;
- `AttemptCount`;
- `ProcessedAt`;
- ostatni błąd.

`NotificationEmailOutboxWorker`:

- działa jako `BackgroundService`;
- sprawdza oczekujące wiadomości co około 15 sekund;
- pobiera maksymalnie 20 wiadomości w jednej iteracji;
- obsługuje maksymalnie 3 próby;
- przesuwa następne podejście po błędzie;
- zapisuje stan przetworzenia lub ostatni błąd;
- raportuje stan do health checks workerów.

### Activity i audyt bezpieczeństwa

- Istnieje encja `ProjectActivity` dla aktywności produktu.
- Istnieje encja `AccountSecurityEvent` dla zdarzeń bezpieczeństwa.
- Są endpointy odczytu activity projektu i administracyjnego odczytu security events.

Źródła:

- `backend/Domain/Entities/Notification.cs`
- `backend/Domain/Entities/NotificationEmailOutboxMessage.cs`
- `backend/Domain/Entities/ProjectActivity.cs`
- `backend/Domain/Entities/AccountSecurityEvent.cs`
- `backend/Infrastructure/Services/NotificationEmailOutboxWorker.cs`
- `frontend/src/context/NotificationsContext.tsx`
- `frontend/src/types/notifications.ts`

## 6. Optimistic concurrency

### Encje i konfiguracja

`ConcurrencyStamp` jest widoczny w modelach i konfiguracji EF Core dla:

- `User`;
- `Project`;
- `ProjectTask`;
- `ProjectInvitation`;
- `RefreshToken`.

Dla wybranych encji stempel jest konfigurowany jako `IsConcurrencyToken()`.
`Project` i `ProjectTask` zmieniają stempel przy zmianach domenowych.

### Kontrakty i zachowanie

- Odpowiedzi projektu i zadania zwracają `ConcurrencyStamp`.
- Requesty aktualizacji projektu i zadania przyjmują oczekiwany stempel.
- Aktualizacja statusu zadania również przyjmuje oczekiwany stempel.
- Usunięcie zadania przyjmuje stempel przez query string.
- Handler może odrzucić brakujący lub nieaktualny stempel przed zapisem.
- Konflikt persystencji jest mapowany na wynik konfliktu i HTTP `409` przez warstwę API/middleware.
- Testy PostgreSQL sprawdzają konflikt równoległej zmiany projektu i zadania.
- Testy sprawdzają, że przegrana zmiana nie nadpisuje zwycięskiej wartości.

Źródła:

- `backend/Domain/Entities/Project.cs`
- `backend/Domain/Entities/ProjectTask.cs`
- `backend/Infrastructure/Data/Configurations/ProjectConfiguration.cs`
- `backend/Infrastructure/Data/Configurations/ProjectTaskConfiguration.cs`
- `backend/API/Contracts/Projects/ProjectRequests.cs`
- `backend/API/Contracts/Projects/ProjectResponses.cs`
- `backend/API/Contracts/Projects/ProjectTaskResponses.cs`
- `backend/API/Middleware/ExceptionHandlingMiddleware.cs`
- `backend/IntegrationTests/PostgreSqlIntegrationTests.cs`
- `backend/IntegrationTests/ProjectTasksApiIntegrationTests.cs`

## 7. Załączniki i storage

- Załączniki są powiązane z zadaniami.
- Istnieją operacje upload, list, download i delete.
- Dostęp do załączników jest sprawdzany przez kontekst projektu/zadania.
- Istnieje walidacja zawartości i rozpoznawania typu pliku.
- Dostępne implementacje storage to lokalny filesystem i S3-compatible storage.
- Docker Compose używa MinIO jako lokalnego S3-compatible storage.
- Produkcyjny Compose konfiguruje S3 storage oraz osobny serwis ClamAV.
- Istnieją kolejka cleanupu, worker cleanupu oraz reconciliation service.
- Konfiguracja obejmuje provider, bucket, service URL, credentials, limit timeoutu skanera i przełącznik wymaganego skanu malware.

Źródła:

- `backend/Infrastructure/Modules/ProjectTasks/ProjectTasksModule.cs`
- `backend/Infrastructure/Services/LocalProjectTaskAttachmentStorage.cs`
- `backend/Infrastructure/Services/S3ProjectTaskAttachmentStorage.cs`
- `backend/Infrastructure/Services/AttachmentStorageHealthCheck.cs`
- `backend/deploy/vps/compose.production.yml`
- `docker-compose.yml`

## 8. Frontendowe workflowy i widoki

### Trasy publiczne

- `/`
- `/login`
- `/register`
- `/confirm-email`
- `/forgot-password`
- `/reset-password`
- `/verify-2fa`

### Trasy chronione

- `/dashboard`
- `/profile`
- `/notifications`
- `/projects`
- `/project-invitation`

### Trasy administracyjne

- `/admin`
- `/admin/users`
- `/users` jako przekierowanie zależne od konfiguracji.

### Widoki obecne w `frontend/src/pages`

- Home, Login, Register, ConfirmEmail, ForgotPassword, ResetPassword, VerifyTwoFactor;
- Dashboard, Profile, Notifications, Projects, ProjectInvitation;
- AdminPanel, users/UserList, users/CreateUser, users/UpdateUser;
- Forbidden i NotFound.

## 9. Testy zapisane w repozytorium

### Testy backendowe

- `UnitTests` zawiera testy domeny, walidatorów, usług, kontrolerów i vertical slices.
- `IntegrationTests` zawiera testy auth, users, projects, invitations, tasks, notifications,
  observability, architektury modułów i PostgreSQL.
- `PostgreSqlWebApplicationFactory` oraz Testcontainers PostgreSQL są obecne.
- `E2ETests/SmokeTests.cs` jest osobnym projektem testowym .NET.
- Projekty testowe targetują `.NET 9`.

### Frontend unit/component tests

W repozytorium są testy między innymi dla:

- `AppRoutes`;
- bootstrap gate i protected route;
- navbar;
- login i register;
- profile;
- notifications;
- admin panel i user list;
- helpers oraz contextów auth i runtime config.

### Browser E2E

Playwright ma `testDir: './e2e'`, `workers: 1`, trace/screenshot/video retain-on-failure
i bazowy URL domyślnie `http://localhost:3000`.

Obecne scenariusze w `frontend/e2e` obejmują:

- rejestrację, potwierdzenie emaila, 2FA i logout;
- utworzenie projektu oraz workflow zadania;
- zaproszenie użytkownika z rolą viewer i sprawdzenie read-only;
- odrzucenie nieaktualnej edycji projektu z równoległego kontekstu przeglądarki.

Źródła:

- `backend/UnitTests/`
- `backend/IntegrationTests/`
- `backend/E2ETests/SmokeTests.cs`
- `frontend/src/tests/`
- `frontend/e2e/`
- `frontend/playwright.config.ts`

## 10. Uruchamianie i operacje

### Lokalny Docker Compose

Lokalny `docker-compose.yml` definiuje między innymi:

- PostgreSQL;
- Mailpit;
- MinIO;
- `minio-init`;
- backend API;
- frontend React.

Backend ma health check zależny od bazy, Mailpit i inicjalizacji MinIO. Frontend zależy od
zdrowego backendu.

### Produkcyjny/stagingowy kierunek VPS

`deploy/vps` zawiera między innymi:

- Compose production i staging;
- Caddy;
- osobny kontener migracji;
- MinIO;
- ClamAV;
- Prometheus;
- Grafana;
- Alertmanager;
- blackbox exporter;
- skrypty deploy, rollback, backup i restore;
- konfigurację systemd dla backupu.

### Observability i health

- Serilog zapisuje logi strukturalne do konsoli i plików rotowanych.
- Middleware correlation ID jest częścią pipeline'u API.
- Dostępne są health checks aplikacji, gotowości, workerów, storage, malware scanner i email.
- Produkcyjny Compose zawiera limity logów kontenerów oraz konfigurację Prometheus/Grafana/Alertmanager.

Źródła:

- `docker-compose.yml`
- `backend/API/Program.cs`
- `deploy/vps/compose.production.yml`
- `deploy/vps/compose.staging.yml`
- `deploy/vps/backup.sh`
- `deploy/vps/restore.sh`
- `deploy/vps/rollback.sh`
- `deploy/vps/observability/`

## 11. Fakty wymagające późniejszej weryfikacji runtime

Poniższe elementy są widoczne w konfiguracji lub kodzie, ale nie zostały potwierdzone uruchomieniem
w ramach tego snapshotu:

- start i zdrowie lokalnego Docker Compose;
- dostępność API, frontendu, PostgreSQL, Mailpit i MinIO;
- zastosowanie migracji i aktualny stan bazy;
- przejście pełnych testów backendu i frontendu;
- przejście Playwright E2E przeciwko uruchomionemu stackowi;
- rzeczywiste zachowanie PostgreSQL przy konkurencyjnych zapisach;
- działanie email outbox, retry i limitu prób;
- działanie storage S3/MinIO oraz reconciliation cleanupu;
- działanie ClamAV przy skanowaniu załączników;
- backup, restore, rollback i monitoring na środowisku stagingowym;
- zgodność aktualnych dokumentów roadmapy z bieżącym stanem kodu;
- zachowanie aplikacji przy wielu instancjach backendu.

## 12. Pliki źródłowe do dalszej analizy

- `doc/ROADMAP/00_ROADMAP_OVERVIEW.md`
- `doc/ROADMAP/03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md`
- `doc/ROADMAP/04_V4_PRODUCT_COMPLETENESS.md`
- `doc/ROADMAP/05_V5_DEPLOYMENT_AND_OPERATIONS.md`
- `doc/ROADMAP/06_V6_PERFORMANCE_AND_RELIABILITY.md`
- `doc/ROADMAP/07_V7_OPTIONAL_EVOLUTION.md`
- `doc/PRODUCT_EVOLUTION/README.md`
- `doc/PRODUCT_EVOLUTION/FEATURE_BACKLOG.md`
- `backend/DEVELOPMENT_ROADMAP.md`
- `doc/ARCHITECTURE.md`
