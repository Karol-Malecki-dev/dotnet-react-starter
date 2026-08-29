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

### User Domain Boundary

`User` remains a single aggregate root mapped to the existing `Users` table. New instances
are created through `User.Create(...)`, and profile, account, credential, two-factor, and
lockout changes go through explicit domain methods instead of public setters. External
uniqueness checks and configuration-dependent policies remain in application and
infrastructure services. This boundary intentionally does not split persistence into
separate profile and security tables.

## Database Model

Główna baza jest obsługiwana przez EF Core w `backend/Infrastructure/Data/ApplicationDbContext.cs`.

Najważniejsze tabele/encje:

- `Users`
- `RefreshTokens`
- `EmailConfirmationTokens`
- `EmailTwoFactorChallenges`
- `PasswordResetRequests`
- `Projects`
- `ProjectMembers`
- `ProjectTasks`

Relacje są proste i czytelne:

- tokeny i challenge są przypisane do `User`
- usunięcie użytkownika kaskadowo usuwa powiązane rekordy auth
- tokeny mają indeksy na hashach i datach wygaśnięcia
- `Project` należy do jednego `User` przez `OwnerId`
- `Project` ma członków przez encję `ProjectMember`; właściciel jest dodawany automatycznie
- `ProjectMember` łączy projekt z aktywnym użytkownikiem i posiada unikalny indeks na `(ProjectId, UserId)`
- `Project` jest aggregate rootem dla `ProjectMember`
- `ProjectTask` jest osobnym aggregate rootem powiązanym z `Project` przez wymagany `ProjectId`
- `ProjectTask` może mieć opcjonalnego przypisanego członka projektu przez `AssignedUserId`
- `ProjectTask` może mieć etykiety, załączniki i termin wykonania
- przypomnienia terminów tworzą powiadomienia, a dostarczanie emailowe przechodzi przez outbox
- usunięcie projektu kaskadowo usuwa jego zadania
- usunięcie przypisanego użytkownika ustawia `AssignedUserId` na `NULL`

### Project And ProjectTask Domain

`Project` posiada podstawowe dane biznesowe, właściciela i stan archiwizacji.
Archiwizacja jest soft delete: aktywne odczyty pomijają projekt, a aktualizacje
i tworzenie zadań dla projektu archiwalnego są blokowane.

`Project` i `ProjectTask` są osobnymi aggregate rootami. `ProjectTask.ProjectId` jest
referencją tożsamościową i kluczem obcym w bazie, ale `Project` nie ładuje ani nie
mutuje kolekcji zadań jako części własnego modelu domenowego. Zagnieżdżenie endpointów
zadań pod projektem opisuje nawigację zasobu i kontrolę dostępu, a nie własność
agregatową.

`ProjectTask` posiada własny cykl życia niezależny od projektu:

- `Todo`, `InProgress`, `Done` jako `ProjectTaskStatus`,
- `Low`, `Normal`, `High` jako `ProjectTaskPriority`,
- opcjonalny termin wykonania,
- opcjonalne przypisanie do aktywnego użytkownika.

Bezpieczeństwo zadań jest sprawdzane w kontekście projektu przez application service.
Serwis zadaniowy nie wyszukuje zadania wyłącznie po `taskId`; każde zapytanie filtruje
jednocześnie po `projectId`, właścicielu lub członkostwie użytkownika i aktywnym stanie
projektu. Dzięki temu identyfikator zadania nie może ominąć kontroli dostępu.

### Project Invitation Acceptance, Transactions And Concurrency

`Project` jest aggregate rootem dla `ProjectMember`. Dodanie, zmiana roli i usunięcie
członka przechodzą przez metody domenowe projektu, a bezpośrednie tworzenie członków
jest ograniczone do fabryki encji i warstwy persistence. Dzięki temu właściciel
projektu nie może zostać dodany drugi raz, usunięty ani zdegradowany do innej roli.

Akceptacja zaproszenia jest wieloetapowym przypadkiem użycia. Dla providerów
relacyjnych `IProjectInvitationStore.BeginTransactionAsync` otwiera transakcję,
która obejmuje:

- dodanie członka przez `Project.AddMember`;
- zmianę statusu zaproszenia i wpis aktywności;
- zapis powiadomienia oraz `NotificationEmailOutboxMessage`.

Commit następuje dopiero po pomyślnym zapisie powiadomienia. Błąd powiadomienia
wycofuje więc całą akceptację, zamiast zostawić członkostwo bez informacji dla
użytkownika. Provider InMemory nie obsługuje transakcji relacyjnych, dlatego testy
jednostkowe używają tam zachowania bez transakcji, a atomowość jest weryfikowana
testami PostgreSQL.

Bezpośrednie dodanie członka przez właściciela używa tego samego portu
`IProjectTransaction`. Relacyjny commit następuje dopiero po zapisie członkostwa,
aktywności i notification/outbox, dlatego błąd notification nie zostawia częściowego
członkostwa ani aktywności. Ten rollback jest również sprawdzany w teście PostgreSQL.

Usunięcie członka również używa tego portu. Granica obejmuje pobranie i unassign jego
zadań, usunięcie membershipu oraz wpis aktywności, a commit następuje dopiero po
`SaveChangesAsync()`. Test PostgreSQL sprawdza, że po udanym workflowie członek nie
istnieje, jego zadania są nieprzypisane, a aktywność została zapisana.

`Project.ConcurrencyStamp` chroni równoległe zmiany agregatu, a
`ProjectInvitation.ConcurrencyStamp` chroni przejścia stanu zaproszenia. Dodatkowo
unikalny indeks `(ProjectId, UserId)` pozostaje obroną na poziomie bazy. Naruszenie
tego indeksu podczas równoległej akceptacji jest mapowane na wynik konfliktu,
który kontroler zwraca jako `409 Conflict`.

`ProjectTask.ConcurrencyStamp` chroni niezależne zmiany zadania. Aktualizacja danych,
zmiana statusu i usunięcie wymagają wersji odczytanej przez klienta; po udanym zapisie
token jest rotowany, a nieaktualna wersja jest mapowana na `409 Conflict` bez
nadpisania nowszego zapisu.

### ProjectManagement.Tasks As A Modular-Monolith Feature

`ProjectManagement.Tasks` jest pierwszą feature boundary rozwijaną w kierunku
modularnego monolitu. Moduł pozostaje częścią jednego procesu i jednej bazy danych,
ale jego przypadki użycia komunikują się z persistence przez wąskie porty zdefiniowane
w `Application`.

Przepływ dla odczytu listy zadań wygląda następująco:

```text
ProjectTasksController
	|
IProjectTaskQueryService
	|
IProjectTaskAccess + IProjectTaskQueryStore
	|
EfProjectTaskAccess + EfProjectTaskQueryStore
	|
ApplicationDbContext / PostgreSQL
```

Komendy używają analogicznego podziału:

```text
ProjectTasksController
	|
IProjectTaskCommandService
	|
IProjectTaskAccess + IProjectTaskCommandStore
	|
EfProjectTaskAccess + EfProjectTaskCommandStore
	|
ApplicationDbContext / PostgreSQL
```

`IProjectTaskAccess`, `IProjectTaskQueryStore`, `IProjectTaskCommandStore` oraz
`IProjectMembershipStore` są portami przypadków użycia, a nie generycznym repository.
Dzięki temu kontrakty
opisują rzeczywiste potrzeby funkcji: kontrolę dostępu, listowanie z filtrami oraz
zapis zmian zadania. Implementacje EF pozostają w `Infrastructure`, a kontrolery
nie znają `ApplicationDbContext`.

Rozdzielenie query i command nie oznacza wprowadzenia MediatR, RabbitMQ ani event
busa. Jest to lokalny podział odpowiedzialności w ramach modularnego monolitu,
który zachowuje istniejące endpointy, migracje i model relacyjny.

Przypisanie zadania jest dodatkowo walidowane względem aktywnego członkostwa
w projekcie. Zarządzanie członkami jest dostępne wyłącznie właścicielowi projektu,
a usunięcie członka czyści jego przypisania do zadań.

Endpointy zadań są zagnieżdżone pod projektem:

```text
GET    /api/projects/{projectId}/tasks
POST   /api/projects/{projectId}/tasks
GET    /api/projects/{projectId}/tasks/{taskId}
PUT    /api/projects/{projectId}/tasks/{taskId}
PATCH  /api/projects/{projectId}/tasks/{taskId}/status
DELETE /api/projects/{projectId}/tasks/{taskId}
```

Endpointy członków projektu:

```text
GET    /api/projects/{projectId}/members
GET    /api/projects/{projectId}/members/available
POST   /api/projects/{projectId}/members
DELETE /api/projects/{projectId}/members/{userId}
```

Szczegółowy workflow dodawania tej funkcji znajduje się w `doc/ADDING_FEATURES.md`.

Model obejmuje zarówno auth i konta użytkowników, jak i rozwijaną domenę zarządzania projektami oraz zadaniami. Kolejne funkcje powinny respektować osobne granice agregatów `Project` i `ProjectTask` oraz istniejące kontrole członkostwa i ról.

## Observability And Background Work

API używa middleware korelacji żądań oraz Seriloga. Nagłówek `X-Correlation-ID` jest zwracany klientowi i trafia do log contextu, co pozwala połączyć wpisy dotyczące jednego żądania.

Health endpoints mają rozdzielone odpowiedzialności:

- `/health/live` nie wykonuje zależności zewnętrznych i służy do liveness;
- `/health/ready` sprawdza połączenie z bazą;
- `/health` agreguje podstawową gotowość API i bazy, bez stanu workerów;
- `/health/workers` raportuje świeżość ostatnich cykli workerów.

Workery `NotificationEmailOutboxWorker` i `ProjectTaskDeadlineReminderWorker` są hostowanymi usługami infrastruktury. Ich stan jest przechowywany w pamięci procesu, więc endpoint worker health opisuje bieżącą instancję aplikacji i nie zastępuje trwałego monitoringu ani kolejki rozproszonej.

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
- `frontend/src/tests/` i testy przy komponentach - testy React + RTL/Vitest

Testy usług `ProjectTask` używają mockowanych portów Application i sprawdzają
reguły orkiestracji, statusy oraz kolejność decyzji dostępu. Testy integracyjne
sprawdzają rzeczywistą konfigurację API i persistence. Test PostgreSQL wymaga
działającego Docker Desktop, ponieważ uruchamia kontener `postgres:16-alpine`
przez Testcontainers.

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