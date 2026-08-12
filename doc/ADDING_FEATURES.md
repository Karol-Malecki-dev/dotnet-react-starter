# Adding Features

Ten dokument opisuje, jak rozwijać ten starter w sposób spójny z obecną architekturą.

## When To Read This Document

Czytaj ten plik, gdy chcesz ustalić:

- od czego zacząć dodawanie nowego feature'a
- gdzie w projekcie powinna trafić nowa logika
- w jakiej kolejności ruszać backend, frontend, bazę i dokumentację
- jak nie zepsuć istniejących wzorców auth, runtime config i routingu

Jeśli najpierw potrzebujesz zrozumieć obecną architekturę projektu, zacznij od `doc/ARCHITECTURE.md`.

## General Rule

Najpierw ustal, jakiego typu jest nowy feature.

Najczęstsze przypadki:

- nowy ekran lub nowy flow UI
- nowy endpoint backendowy
- rozszerzenie istniejącego auth flow
- nowa flaga runtime
- nowa tabela lub nowa relacja w bazie
- nowa sekcja admina

Nie zaczynaj od przypadkowego dopisywania kodu w widoku lub kontrolerze.
Najpierw ustal źródło prawdy i granicę odpowiedzialności.

## Adding a New Frontend Feature

Jeśli dodajesz nowy feature po stronie UI:

1. Ustal, czy to nowa strona, nowa sekcja istniejącej strony czy nowy komponent shella.
2. Dodaj lub rozszerz typy w `frontend/src/types/`, jeśli zmienia się kontrakt z API.
3. Dodaj lub rozszerz klienta API w `frontend/src/services/api/`.
4. Dodaj schemat walidacji w `frontend/src/utils/`, jeśli feature ma formularz.
5. Dodaj logikę do contextu lub hooka, jeśli stan ma być współdzielony.
6. Dodaj routing w `frontend/src/components/AppRoutes.tsx`, jeśli to nowy ekran.
7. Dodaj testy komponentu, hooka lub routingu adekwatne do zmiany.

## Adding a New Backend Feature

Jeśli dodajesz nowy feature po stronie backendu:

1. Zacznij od domeny, jeśli pojawia się nowe pojęcie biznesowe.
2. Dodaj DTO, interfejsy i walidację w `backend/Application/`.
3. Dodaj implementację persistence lub integracji w `backend/Infrastructure/`.
4. Dodaj lub rozszerz endpoint w `backend/API/Controllers/`.
5. Zarejestruj nowe usługi w `backend/API/Services/AddProjectServices.cs`, jeśli to potrzebne.
6. Dodaj testy jednostkowe i integracyjne.

## Adding a New Runtime Feature Flag

To jest ważny, powtarzalny wzorzec w tym projekcie.

Jeśli chcesz dodać nową flagę runtime:

1. Dodaj pole w backendowym settings, zwykle w `Shared/Settings/UiFeatureSettings.cs` albo innym odpowiednim settings class.
2. Dodaj wartość w `backend/API/appsettings.json` i `backend/API/appsettings.Development.json`.
3. Dodaj pole do `backend/Shared/Dtos/AppRuntimeConfigurationDto.cs`.
4. Zmapuj je w `backend/API/Controllers/RuntimeConfigController.cs`.
5. Dodaj odpowiadające pole do `frontend/src/types/runtimeConfig.ts`.
6. Dodaj fallback i normalizację w `frontend/src/context/RuntimeConfigContext.tsx`.
7. Wystaw prosty boolean przez `frontend/src/hooks/useFeatureAvailability.ts`.
8. Użyj flagi w `Navbar`, `AppRoutes`, `AppBootstrapGate` albo odpowiednim komponencie.
9. Dodaj testy dla runtime config contextu i miejsc, które są przez flagę sterowane.

## Where to Put New Logic

Kilka prostych reguł:

- decyzje o widoczności trasy wkładaj do routingu
- decyzje o widoczności linków i sekcji shella wkładaj do shella lub navbara
- logikę pobierania i transformacji danych wkładaj do API clienta, contextu albo hooka
- logikę walidacji formularzy trzymaj w `utils/` obok innych schematów
- logikę bezpieczeństwa i autoryzacji trzymaj po stronie backendu

## Adding a New Database Table or Relation

Jeśli feature wymaga nowej tabeli:

1. Dodaj encję w `backend/Domain/Entities/`.
2. Dodaj `DbSet` i konfigurację relacji w `ApplicationDbContext`.
3. Wygeneruj migrację EF Core.
4. Dodaj lub rozszerz serwis infrastrukturalny, który pracuje na tych danych.
5. Dodaj testy integracyjne sprawdzające zapis i odczyt.

Warto pilnować, żeby relacje i indeksy były jawne i czytelne, szczególnie dla rekordów tokenów, challenge i danych użytkownika.

## Modular Monolith Feature Boundary

Dla funkcji należących do zarządzania projektami stosuj granicę feature zamiast
bezpośredniego używania `ApplicationDbContext` w serwisie aplikacyjnym.

Rekomendowany podział:

1. Kontrakty przypadków użycia umieść w `Application/Features/<Feature>/`.
2. Reguły domenowe trzymaj w encji lub agregacie w `Domain/`.
3. Zdefiniuj małe porty persistence opisujące konkretne potrzeby funkcji.
4. Implementacje portów EF umieść w `Infrastructure/<Feature>/`.
5. Zarejestruj porty i implementacje w composition root.
6. Testuj serwis aplikacyjny przez mocki portów, a zapis i zapytania EF przez testy integracyjne.

Dla `ProjectManagement.Tasks` aktualne porty to:

- `IProjectTaskAccess` - aktywna rola użytkownika i pobranie zadania z etykietami,
- `IProjectTaskQueryStore` - filtrowanie, sortowanie i paginacja listy zadań,
- `IProjectTaskCommandStore` - zapis zadania, etykiet, aktywności i zmian.

Nie twórz generycznego `IRepository<T>` tylko po to, aby ukryć EF Core. Port powinien
wynikać z przypadku użycia i przyjmować typy oraz operacje potrzebne konkretnej
funkcji. Nie dodawaj MediatR, event busa ani brokera wiadomości bez wymagania
wynikającego z rzeczywistego przypadku biznesowego.

## Naming Conventions

Praktyczne zasady:

- ekran: nazwa rzeczownikowa lub flow-oriented, np. `ResetPassword`, `UserList`
- hook: `use` + odpowiedzialność, np. `useFeatureAvailability`
- context: obszar stanu + `Context`, np. `RuntimeConfigContext`
- API client: obszar + `Api`, np. `RuntimeConfigApi`
- backend settings: obszar + `Settings`, np. `EmailTwoFactorSettings`
- request/response DTO: cel + `RequestDto` lub `ResponseDto`, gdy to pomaga odróżnić kierunek

## Common Mistakes to Avoid

- nie dodawaj feature flags tylko po stronie frontendu, jeśli źródłem prawdy ma być backend
- nie duplikuj tego samego kontraktu w kilku plikach pod inną nazwą
- nie rozsiewaj logiki auth po wielu komponentach
- nie wrzucaj logiki infrastrukturalnej do kontrolerów
- nie traktuj ukrycia przycisku w UI jako zabezpieczenia
- nie aktualizuj tylko jednego `appsettings`, jeśli feature ma działać spójnie w różnych środowiskach

## Recommended Change Order

Najbezpieczniejsza kolejność przy większych zmianach:

1. kontrakt i model danych
2. backendowa implementacja
3. frontendowy klient API
4. frontendowy stan i hooki
5. routing i UI
6. testy
7. dokumentacja

## Documentation Rule

Jeśli feature wprowadza nowy przepływ, nowy typ danych albo nowy wzorzec architektoniczny, zaktualizuj dokumentację w `doc/` od razu po wdrożeniu zmiany.

## Project And ProjectTask Feature

Feature zarządzania projektami składa się z dwóch powiązanych pojęć domenowych:

- `Project` jest aggregate rootem i należy do jednego użytkownika (`OwnerId`),
- `ProjectTask` należy do dokładnie jednego projektu (`ProjectId`),
- jeden projekt może mieć wiele zadań,
- zadanie może być opcjonalnie przypisane do aktywnego użytkownika (`AssignedUserId`).
- zadanie może mieć do 10 etykiet (`ProjectTaskLabel`), unikalnych w ramach zadania.

Relacja ma następującą postać:

```text
User 1 ---- * Project 1 ---- * ProjectTask
										\---- * ProjectTask ---- 0..1 User (AssignedUser)
```

### Backend Contract

Projekty:

```text
GET    /api/projects
POST   /api/projects
GET    /api/projects/{projectId}
PUT    /api/projects/{projectId}
DELETE /api/projects/{projectId}
```

`DELETE /api/projects/{projectId}` jest soft delete i ustawia `IsArchived = true`.
Archiwalne projekty są pomijane domyślnie. Właściciel może pobrać je jawnie przez
`includeArchived=true`. Zarchiwizowany projekt nie przyjmuje nowych zmian ani zadań.

Zadania:

```text
GET    /api/projects/{projectId}/tasks
POST   /api/projects/{projectId}/tasks
GET    /api/projects/{projectId}/tasks/{taskId}
PUT    /api/projects/{projectId}/tasks/{taskId}
PATCH  /api/projects/{projectId}/tasks/{taskId}/status
DELETE /api/projects/{projectId}/tasks/{taskId}
```

Etykiety są przekazywane w polu `labels` żądań tworzenia i edycji zadania. Każda
etykieta ma maksymalnie 40 znaków; API usuwa białe znaki brzegowe, normalizuje
nazwy do małych liter, usuwa duplikaty i zwraca je w kolejności alfabetycznej.

Przypomnienia o terminach są tworzone przez worker uruchamiany co godzinę.
Przypisany aktywny użytkownik dostaje jedno powiadomienie, gdy aktywne zadanie
ma termin w ciągu 24 godzin, oraz osobne powiadomienie po terminie. Rekord
`ProjectTaskDeadlineReminder` deduplikuje powiadomienia według zadania,
odbiorcy, rodzaju i terminu.

Powiadomienia dotyczące zadań przekazują również `ProjectId` obok
`ResourceType = "ProjectTask"` i `ResourceId` zadania. Frontend używa tych
danych do przejścia do `/projects` oraz przewinięcia do wskazanego zadania,
także gdy zadanie nie znajduje się na aktualnej stronie listy.

Każdy endpoint zadań najpierw sprawdza, czy zalogowany użytkownik jest właścicielem
aktywnego projektu. Samo posiadanie identyfikatora projektu lub zadania nie daje dostępu.

### Database Relation

Relację należy konfigurować jawnie w `ApplicationDbContext`:

- `ProjectTask.ProjectId` jest wymaganym kluczem obcym do `Project.Id` i używa cascade delete,
- `ProjectTask.AssignedUserId` jest opcjonalnym kluczem obcym do `User.Id` i używa `SetNull`,
- `Status` i `Priority` są enumami zapisywanymi jako tekst,
- `ProjectTaskLabel` używa cascade delete z zadaniem i unikalnego indeksu na `(ProjectTaskId, Name)`,
- `ProjectTaskDeadlineReminder` używa cascade delete z zadaniem i unikalnego indeksu na `(ProjectTaskId, RecipientUserId, Type, DueDate)`,
- po zmianie modelu należy wygenerować migrację z katalogu `backend/`.

Przykład komendy:

```powershell
dotnet ef migrations add AddProjectTasks `
	--project Infrastructure `
	--startup-project API `
	--context ApplicationDbContext `
	--output-dir Data\Migrations
```

### Recommended Implementation Order

1. Domena: `Project`, `ProjectTask` i enumy.
2. `DbSet`, indeksy, klucze obce i migracja.
3. DTO, walidatory i kontrakty serwisów.
4. Serwis z kontrolą właściciela projektu.
5. Nested controller pod `/api/projects/{projectId}/tasks`.
6. Testy cyklu życia zadania, własności, archiwizacji i walidacji.
7. Dopiero po stabilizacji API integracja z frontendem.

Po pierwszym wdrożeniu funkcji wykonaj również:

8. Testy jednostkowe serwisów aplikacyjnych przez porty.
9. Testy integracyjne z rzeczywistym providerem, jeśli zmiana dotyka persistence.
10. Aktualizację dokumentacji granicy modułu i przepływu zależności.

## Which Document To Open Next

W zależności od typu zmiany przejdź dalej do odpowiedniego pliku:

- `doc/ARCHITECTURE.md` - jeśli chcesz najpierw zrozumieć szeroki obraz projektu
- `doc/BACKEND_SETUP.md` - jeśli dodajesz endpoint, persistence albo konfigurację backendu
- `doc/FRONTEND_SETUP.md` - jeśli dodajesz ekran, routing, context albo integrację UI
- `doc/JWT_ARCHITECTURE.md` - jeśli zmiana dotyka sesji, JWT, refresh tokenów albo `/me`
- `doc/EMAIL_2FA_FLOWS.md` - jeśli zmiana dotyka confirm email, 2FA albo resetu hasła

## See Also

- `doc/ARCHITECTURE.md` - mapa odpowiedzialności i głównych przepływów informacji
- `doc/BACKEND_SETUP.md` - warstwy backendu, konfiguracja i wzorce rozszerzania API
- `doc/FRONTEND_SETUP.md` - bootstrap UI, routing i warstwa klienta API
- `doc/JWT_ARCHITECTURE.md` - model sesji i bezpieczeństwo tokenów
- `doc/EMAIL_2FA_FLOWS.md` - szczegółowe flow email confirmation, 2FA i password reset
