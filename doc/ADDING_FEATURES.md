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

## VSA Quick Start

Dla nowego backendowego przypadku użycia wykonaj najpierw te kroki:

1. Nazwij rezultat z perspektywy użytkownika, na przykład `GetProjectDetails` albo
   `CreateProjectTask`.
2. Wskaż moduł będący właścicielem reguły i danych.
3. Określ, czy przypadek jest commandem, czy query.
4. Wybierz najbliższy działający wzorzec:
   - query: `Projects/GetProjectDetails`;
   - command z regułami domenowymi i efektami ubocznymi:
     `ProjectTasks/CreateProjectTask`.
5. Zapisz najtańszy test, który może wykazać błąd decyzji.
6. Dodaj tylko potrzebne elementy slice'a i zarejestruj je w entry poincie modułu.
7. Przejdź przez [`MODULAR_VSA_MODULE_CHECKLIST.md`](MODULAR_VSA_MODULE_CHECKLIST.md).

Minimalna mapa odpowiedzialności:

| Projekt | Element | Kiedy jest potrzebny |
| --- | --- | --- |
| `Application` | command/query, handler contract, focused port | zawsze kontrakt handlera; port tylko dla zewnętrznej zależności |
| `API` | request/response, validator, endpoint/controller | gdy slice jest publicznie dostępny przez HTTP |
| `Infrastructure` | handler i adapter EF/integracji | handler wykonawczy oraz tylko potrzebne adaptery |
| `UnitTests` | test handlera/validatora | dla reguł sukcesu i meaningful failure paths |
| `IntegrationTests` | test trasy i persistence | dla publicznego kontraktu, autoryzacji i zapisu |
| `frontend` | typy, API client, stan i UI | tylko gdy workflow jest dostępny w UI |

Nie każdy slice potrzebuje osobnego store'a, migracji, eventu, workera, ekranu ani
wszystkich katalogów. `N/A` jest poprawną decyzją, jeśli ma krótkie uzasadnienie.

Jeśli zmiana wprowadza nową granicę, workflow albo trwały kontrakt, zacznij od
[`PRODUCT_EVOLUTION/FEATURE_PROPOSAL_TEMPLATE.md`](PRODUCT_EVOLUTION/FEATURE_PROPOSAL_TEMPLATE.md).

## MediatR Transition Policy

MediatR jest zaakceptowanym docelowym dispatcherem backendowych command/query slices,
ale migracja jest inkrementalna:

1. pierwszy query pilot to `Projects/GetProjectDetails`;
2. pierwszy command pilot to `ProjectTasks/CreateProjectTask`;
3. pilot dodaje `ISender`, `IRequest<TResult>`, `IRequestHandler<TRequest, TResult>`
   oraz bezpieczny telemetry pipeline behavior;
4. po przejściu bramki nowe slice'y używają MediatR domyślnie;
5. istniejące moduły migrują kolejno: `Notifications`, `Projects`, `ProjectTasks`;
6. jeden slice nie może pozostawić dwóch aktywnych dispatch paths.

Podczas przejścia wybierz styl używany przez najbliższy obowiązujący wzorzec. Nie
migruj całego modułu przy okazji niepowiązanej poprawki. MediatR zmienia sposób
wywołania handlera, ale nie właściciela reguły, focused ports, transakcję,
autoryzację ani publiczny kontrakt.

`MediatR.INotification` nie zastępuje trwałego `Notification`, email outboxa ani
integration eventu. Szczegóły i bramki znajdują się w
[`ROADMAP/14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md`](ROADMAP/14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md).

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

1. Ustal jeden use case, jego aktora, właściciela modułu oraz wynik błędu.
2. Zacznij od domeny tylko wtedy, gdy pojawia się albo zmienia niezmiennik biznesowy.
3. Dodaj command/query i kontrakt handlera w
   `backend/Application/Modules/<BusinessModule>/<UseCase>/`.
4. Dodaj focused port wyłącznie dla potrzebnej operacji persistence lub integracji.
5. Dodaj request/response, walidację i endpoint w
   `backend/API/Modules/<BusinessModule>/<UseCase>/`.
6. Dodaj handler i wymagany adapter w
   `backend/Infrastructure/Modules/<BusinessModule>/<UseCase>/`.
7. Zarejestruj zależności przez rozszerzenie właściwego modułu.
8. Dodaj targeted unit test oraz integration test publicznego kontraktu.
9. Zaktualizuj frontend i browser E2E, jeśli use case jest częścią krytycznego
   workflowu użytkownika.

Dla nieprzeniesionego obszaru przejściowego najpierw sprawdź najbliższy wzorzec. Nie
przenoś całego modułu wyłącznie po to, aby dodać jeden feature, ale nie dodawaj nowej
metody do szerokiego serwisu, jeśli granica modułu jest już potwierdzona.

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

Dla nowych funkcji należących do modułu biznesowego stosuj granicę slice'a zamiast
dopisywania kolejnej metody do szerokiego serwisu aplikacyjnego. Handler powinien
korzystać z portu opisującego konkretną potrzebę, a nie z `ApplicationDbContext`.

Rekomendowany podział:

1. Dla nowych slice'ów umieść kontrakty w
   `Application/Modules/<BusinessModule>/<UseCase>/`. Przykładowo
   `GetProjectDetails` należy do `Application/Modules/Projects/GetProjectDetails/`.
   Istniejące, jeszcze nieprzeniesione przypadki mogą pozostać w
   `Application/Features/<Feature>/`.
2. Reguły domenowe trzymaj w encji lub agregacie w `Domain/`.
3. Zdefiniuj małe porty persistence opisujące konkretne potrzeby funkcji.
4. Dla nowych slice'ów implementacje portów EF umieść w
   `Infrastructure/Modules/<BusinessModule>/<UseCase>/`; istniejące adaptery mogą
   pozostać w dotychczasowej lokalizacji przejściowej.
5. Zarejestruj porty i implementacje przez rozszerzenie modułu; composition root
   powinien znać tylko punkt wejścia modułu.
6. Testuj handler przez mocki portów, a zapis i zapytania EF przez testy integracyjne.

Dla `ProjectTasks` aktualne porty współdzielone przez uzasadnione capability to:

- `IProjectTaskAccess` - aktywna rola użytkownika i pobranie zadania z etykietami,
- `IListProjectTasksQueryStore` - filtrowanie, sortowanie i paginacja listy zadań,
- `IProjectTaskCommandStore` - zapis zadania, etykiet, aktywności i zmian,
- `IProjectTaskMemberAssignmentWriter` - staging unassign przy usuwaniu członka,
- `IProjectTaskDashboardReader` - read model statystyk dla dashboardu `Projects`.

Każdy use case `Projects` ma focused store lub jawny port modułowy. Dashboard
rozdziela `IGetProjectDashboardStore` dla danych należących do `Projects` od
`IProjectTaskDashboardReader` dla danych należących do `ProjectTasks`. Kontroler
i handler nie znają `ApplicationDbContext`.

Nie twórz generycznego `IRepository<T>` tylko po to, aby ukryć EF Core. Port powinien
wynikać z przypadku użycia i przyjmować typy oraz operacje potrzebne konkretnej
funkcji. MediatR wdrażaj wyłącznie zgodnie z zaakceptowanym planem; nie traktuj go
jako event busa ani brokera wiadomości.

## Vertical Slice Standard

Nowe przypadki użycia w module biznesowym zaczynaj od katalogu slice'a, a nie od
dopisania kolejnej metody do dużego serwisu:

```text
Application/Modules/<BusinessModule>/<UseCase>/
API/Modules/<BusinessModule>/<UseCase>/
Infrastructure/Modules/<BusinessModule>/<UseCase>/
UnitTests/Modules/<BusinessModule>/<UseCase>/
```

Każdy slice powinien mieć, zależnie od potrzeb:

1. command lub query oraz kontrakt dispatch/handlera w `Application`;
2. handler koordynujący reguły przypadku użycia;
3. request/response i validator przy adapterze HTTP;
4. endpoint zachowujący istniejący kontrakt i statusy HTTP;
5. implementację portów persistence w `Infrastructure`;
6. testy handlera oraz test integracyjny dla trasy;
7. rejestrację w module, a nie bezpośredni wpis w composition root;
8. krótką dokumentację decyzji, zależności i zachowania przy błędzie.

Po aktywacji bramki MediatR command/query implementuje `IRequest<TResult>`, handler
implementuje `IRequestHandler<TRequest, TResult>`, a adapter HTTP używa `ISender`.
W pozostałych slice'ach przejściowych jawny interfejs handlera pozostaje poprawny do
czasu ich zaplanowanej migracji.

Po zakończeniu migracji CRUD `ProjectTasks` nie dodawaj nowych przypadków użycia do
dużego serwisu. Każda nowa komenda lub kwerenda powinna mieć własny slice oraz
rejestrację w entry poincie właściwego modułu, na przykład `ProjectTasksModule` albo
`ProjectsModule`. Przejściowe porty współdzielone przez kilka
slice'ów pozostają dopuszczalne tylko wtedy, gdy opisują rzeczywistą wspólną
potrzebę, a nie wygodę dostępu do całego `DbContext`.

Nie traktuj folderu jako granicy sam w sobie. Moduł jest granicą dopiero wtedy, gdy:

- API zależy od kontraktu handlera, a nie od `DbContext`;
- infrastruktura implementuje porty z `Application`;
- rejestracja zależności jest skupiona w module;
- testy potwierdzają zachowanie slice'a;
- zależności do innych modułów są jawne i ograniczone.

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

1. problem, aktor, właściciel reguły i test rozstrzygający
2. kontrakt, autoryzacja i model danych
3. reguła domenowa oraz backendowy handler
4. persistence, transakcja i integration test
5. frontendowy klient API
6. frontendowy stan, routing i UI
7. browser E2E dla krytycznego workflowu
8. dokumentacja i ADR, jeśli zmieniła się trwała decyzja

## Documentation Rule

Jeśli feature wprowadza nowy przepływ, nowy typ danych albo nowy wzorzec architektoniczny, zaktualizuj dokumentację w `doc/` od razu po wdrożeniu zmiany.

## Project And ProjectTask Feature

Feature zarządzania projektami składa się z dwóch powiązanych, ale osobnych agregatów:

- `Project` jest aggregate rootem dla członkostwa i należy do jednego użytkownika (`OwnerId`),
- `ProjectTask` jest osobnym aggregate rootem i przechowuje referencję do dokładnie jednego projektu (`ProjectId`),
- jeden projekt może mieć wiele zadań,
- zadanie może być opcjonalnie przypisane do aktywnego użytkownika (`AssignedUserId`).
- zadanie może mieć do 10 etykiet (`ProjectTaskLabel`), unikalnych w ramach zadania.

Relacja ma następującą postać:

```text
User 1 ---- * Project 1 ---- * ProjectMember
				 |
				 | ProjectId reference / FK
				 |
				 * ProjectTask ---- 0..1 User (AssignedUser)
```

Diagram pokazuje relację danych, a nie własność agregatową. `ProjectTask` nie jest
ładowany ani zmieniany przez `Project`; reguły wymagające danych z obu agregatów są
koordynowane przez handler przypadku użycia i jawne porty modułowe.

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
lub aktywnym członkiem projektu oraz czy projekt nie jest zarchiwizowany. Samo
posiadanie identyfikatora projektu lub zadania nie daje dostępu.

### Database Relation

Relację należy konfigurować jawnie w `ApplicationDbContext`:

- `ProjectTask.ProjectId` jest wymaganym kluczem obcym do `Project.Id` i używa cascade delete; FK zapewnia integralność referencyjną, ale nie zmienia granicy agregatu,
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
- `doc/PRODUCT_EVOLUTION/DEVELOPMENT_PLAN.md` - kolejność najbliższych inkrementów
- `doc/PRODUCT_EVOLUTION/FEATURE_PROPOSAL_TEMPLATE.md` - decyzja przed większym feature'em
- `doc/MODULAR_VSA_MODULE_CHECKLIST.md` - Definition of Done modułu i slice'a
- `doc/ROADMAP/14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md` - reguły i kolejność adopcji MediatR
