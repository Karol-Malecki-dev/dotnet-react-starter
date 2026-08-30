# V3: Domena, transakcje i współbieżność

## Cel

V3 ma przeprowadzić projekt od poprawnie działającego warstwowego monolitu modularnego do systemu z wyraźnymi granicami odpowiedzialności. Obejmuje również mały pilotaż hybrydy: moduł biznesowy zawierający vertical slices. Najważniejsze pytanie tego etapu brzmi:

> Która reguła należy do domeny, która do przypadku użycia, a która do infrastruktury?

Nie chodzi o mechaniczne dodanie wszystkich wzorców DDD ani przepisanie wszystkich kontrolerów do nowych folderów. Chodzi o uzasadnienie granic, ochronę najważniejszych niezmienników i sprawdzenie na jednym module, czy organizacja według przypadków użycia ogranicza pomijanie kontraktów, walidacji, rejestracji i testów.

## Stan wyjściowy

`ProjectTask` jest obecnie najlepiej zamodelowaną encją: posiada prywatny konstruktor, ograniczone settery i metody domenowe. `Project` ma częściową enkapsulację, a `User` używa już fabryki, prywatnych setterów i jawnych metod domenowych przy zachowaniu płaskiego mappingu EF Core. Moduł zadań używa już feature-specific ports, co daje dobry punkt wyjścia do dalszego rozdzielania odpowiedzialności.

## Status realizacji

Stan na: **2026-08-31**.

| Obszar | Postęp | Status i dowód |
|---|---:|---|
| 1. Granica agregatów projektu i zadań | 75% | `Project` chroni członkostwo przez metody domenowe, automatycznie tworzy właściciela i ma prywatne settery; `ProjectTask` został przyjęty jako osobny agregat, a niezależność tokenu projektu potwierdza test integracyjny. |
| 2. Model użytkownika i value objects | 55% | `User.Email` i `User.DisplayName` używają kanonicznych value objectów domenowych, a `User` ma fabrykę, prywatne settery i jawne metody zmian profilu oraz stanu bezpieczeństwa; istniejąca płaska tabela `Users` i kontrakty HTTP zostały zachowane. Zastosowanie value objectu dla `Address` oraz pełne rozdzielenie profilu od stanu bezpieczeństwa nadal wymagają oceny. |
| 3. Application services i porty | 65% | Handlery i focused ports dla backendowego pilota `ProjectTasks` są wydzielone; część współdzielonych portów i kontraktów pozostaje przejściowa. |
| 4. Mapping i kontrakty | 60% | DTO i kontrakty HTTP są obecne oraz dokumentowane, a endpointy pilota mają slice-specific adaptery; pełne rozdzielenie modeli nie jest zakończone. |
| 5. Transakcje i partial failure | 70% | Akceptacja zaproszenia, bezpośrednie dodanie członka oraz usunięcie członka mają relacyjne granice transakcji; usunięcie obejmuje także unassign zadań i aktywność, a rollbacki przy błędzie notification są pokryte dla dwóch workflowów z powiadomieniami. |
| 6. Optimistic concurrency | 70% | `Project`, `ProjectInvitation` i `ProjectTask` mają tokeny wersji, konflikty są mapowane na `409`, a konflikty zapisów projektu i zadania oraz wyścig akceptacji są testowane na PostgreSQL. |
| 7. Zapytania dashboardu | 60% | Statystyki dashboardu są agregowane po stronie SQL, listy overdue/upcoming używają zakresów dat przyjaznych indeksom i limitów, a test PostgreSQL potwierdza użycie indeksu `IX_ProjectTasks_ProjectId_Status_DueDate`; benchmark obciążeniowy nadal należy do V6. |
| 8. Pilotaż modularnego VSA | 75% | `ProjectTasks` ma slice'y CRUD, comments, attachments i deadline reminders, własny modułowy entry point, focused ports oraz testy jednostkowe i integracyjne; brakuje migracji frontendowej, lekkich guardrails, przeniesienia ostatnich współdzielonych kontraktów i końcowej oceny kosztu pilota. |

**Postęp V3: 50%**.

Procent obejmuje istniejące fundamenty, nie samą liczbę klas lub endpointów. V3 nie jest jeszcze etapem ukończonym.

## Zakres implementacyjny

### 1. Granica agregatu projektu

- określić, czy zadanie jest częścią agregatu `Project`, czy osobnym agregatem powiązanym przez identyfikator;
- spisać reguły, które musi chronić `Project`;
- ograniczyć publiczne settery i kolekcje;
- dodać metody domenowe dla archiwizacji, zmiany danych i zarządzania członkostwem tam, gdzie reguła jest domenowa;
- nie przenosić do encji reguł zależnych od zewnętrznych zapytań lub wysyłki emaili;
- rozstrzygnąć, czy `ProjectMember` jest częścią agregatu projektu, osobnym agregatem czy relacją zarządzaną przez application service.

### 2. Model użytkownika i value objects

- ocenić, czy email, nazwa i adres powinny być value objects;
- zastosować value object tylko wtedy, gdy usuwa niepoprawne stany, normalizację lub powtórzony kod;
- nie tworzyć value objectów wyłącznie dla większej liczby klas;
- rozdzielić dane profilu od stanu bezpieczeństwa, jeśli poprawi to granice modelu;
- zachować prostotę mappingu EF Core.

W bieżącej iteracji `EmailAddress` jest kanonicznym value objectem domenowym dla `User.Email`,
a `DisplayName` dla `User.DisplayName`. `EmailAddress` przyjmuje tylko poprawny adres,
zapisuje go po `Trim()` i `ToLowerInvariant()` oraz udostępnia bezpieczne `TryCreate`.
`DisplayName` usuwa skrajne białe znaki, scala powtarzające się separatory, odrzuca puste
wartości i zachowuje limit `200` znaków. Oba value objecty są przechowywane w bazie jako
zwykły tekst przez konwertery EF Core, a `DisplayName` implementuje porównywanie
kanonicznej wartości potrzebne do sortowania wyników także przez provider InMemory.
Kontrakty HTTP i application nadal używają `string`, więc granica domeny nie zmienia
publicznego API. Stary helper `Shared.Helpers.EmailAddress` został usunięty, aby nie
pozostawiać dwóch konkurencyjnych implementacji.

W bieżącej iteracji `User` pozostaje jednym aggregate rootem mapowanym do istniejącej tabeli
`Users`, ale jego stan jest zmieniany przez `User.Create(...)` oraz jawne metody domenowe.
Settery są prywatne, a metody profilu, hasła, roli, aktywacji, potwierdzenia emaila,
two-factor i lockoutu nie wymagają zmiany schematu bazy. Jest to etap enkapsulacji zachowania,
nie pełny podział persystencji na osobne `UserProfile` i `UserSecurityState`.

### 3. Application services i porty

- utrzymywać interfejsy przypadków użycia w `Application`;
- rozdzielić kontrakty od implementacji EF w `Infrastructure`;
- preferować porty opisujące konkretną potrzebę funkcji zamiast generycznego repository;
- ograniczyć kontrolery do transportu HTTP, autoryzacji wejścia i mapowania wyniku;
- przenosić reguły biznesowe z kontrolerów i dużych serwisów do odpowiedniego miejsca.

Backendowy inkrement modularizacji został zwalidowany na `ProjectTasks`: CRUD zadań,
komentarze, załączniki i deadline reminders mają osobne command/query, kontrakty
handlerów, implementacje, adaptery HTTP albo workery oraz testy. Rejestracje tasków
są skupione w `ProjectTasksModule.AddProjectTasksModule`, a publiczne endpointy,
kontrakty JSON i `ApplicationDbContext` nie zostały zmienione. Współdzielone porty
`IProjectTaskAccess`, `IProjectTaskCommandStore` oraz `ProjectTaskView` pozostają
świadomie przejściowe, ponieważ korzysta z nich kilka slice'ów i istniejący dashboard.

Po zamknięciu głównej części pilota rozpoczęto następny, nadal przyrostowy krok:
`Projects/GetProjectDetails`. Ten slice zachowuje istniejący endpoint i model
odpowiedzi, ale korzysta już z własnego handlera, portu odczytu, adaptera EF,
kontrolera oraz `ProjectsModule`. Pozostałe use case'y `Projects` pozostają
przejściowo w szerokim serwisie i nie są przenoszone mechanicznie razem z tym
odczytem.

### 4. Mapping i kontrakty

- rozdzielić DTO HTTP, modele Application i encje domenowe tam, gdzie mają różne odpowiedzialności;
- nie wystawiać encji EF bezpośrednio przez API;
- ujednolicić nazwy requestów, response i view modeli;
- sprawdzić, czy `Shared` nie przejmuje odpowiedzialności należącej do konkretnej warstwy;
- dokumentować publiczne kontrakty XML i OpenAPI.

### 5. Transakcje i partial failure

Zidentyfikować operacje obejmujące więcej niż jeden zapis, między innymi:

- akceptacja zaproszenia i dodanie członka;
- usunięcie członka, wyczyszczenie przypisań zadań i zapis aktywności;
- zmiana biznesowa, activity i notification/outbox;
- reset lub zmiana hasła oraz unieważnienie sesji.

Akceptacja zaproszenia, bezpośrednie dodanie członka oraz usunięcie członka używają
wspólnego portu `IProjectTransaction`. W providerze relacyjnym commit następuje
dopiero po zapisaniu wszystkich zmian danego workflowu. Dla usunięcia są to
unassign zadań, usunięcie członkostwa i aktywność. Awaria notification wycofuje cały
workflow w przepływach, które zapisują notification/outbox; provider InMemory
zachowuje dotychczasowe zachowanie bez transakcji relacyjnej.

Dla każdej operacji określić:

- co musi być atomowe;
- co może być wykonane później przez worker;
- jaki jest punkt commit;
- jak system zachowa się po błędzie pośrednim;
- czy potrzebny jest outbox, retry lub kompensacja.

### 6. Optimistic concurrency

- dodać token wersji dla wybranych encji, początkowo projektu, zadania lub zaproszenia;
- skonfigurować concurrency token w EF Core/PostgreSQL;
- mapować `DbUpdateConcurrencyException` na `409 Conflict`;
- zwracać klientowi informację pozwalającą odświeżyć dane;
- przetestować dwa procesy aktualizujące tę samą wersję.

W bieżącej iteracji token `ProjectTask.ConcurrencyStamp` chroni niezależne zmiany
tytułu, opisu, priorytetu, statusu, przypisania, terminu, etykiet i usunięcia zadania.
Mutacje wymagają oczekiwanej wersji, zwracają nową wersję po sukcesie, a nieaktualna
wersja kończy się konfliktem `409`. Migracja backfilluje token dla istniejących zadań,
a test PostgreSQL potwierdza konflikt dwóch kontekstów EF Core.

Nie trzeba dodawać concurrency tokenu do każdej tabeli. Wybór powinien wynikać z ryzyka utraty zmian.

### 7. Zapytania dashboardu

- zidentyfikować miejsca, gdzie pobierane są całe kolekcje tylko po to, aby policzyć statystyki;
- zastąpić je projekcjami i agregacjami SQL;
- używać zakresów zamiast nakładania funkcji na kolumny filtrowane po dacie;
- dodać indeks tylko po sprawdzeniu planu zapytania;
- porównać plan zapytania przed i po zmianie, gdy istnieje wiarygodny baseline;
- zachować czytelność query store.

### 8. Pilotaż modułu biznesowego zawierającego vertical slices

Pilotem pozostaje `ProjectTasks`, ponieważ ma osobny agregat, jawne reguły dostępu,
feature-specific ports oraz testy integracyjne. Celem V3 nie jest jeszcze przeniesienie
całego obszaru Projects ani frontendu do nowej struktury. `GetProjectDetails` jest
pierwszym kontrolowanym slice'em po pilocie i służy do sprawdzenia, czy standard
działa także dla odczytu agregatu `Project`.

W ramach pilota należy:

- zachować `CreateProjectTask` jako reprezentatywny command slice;
- wydzielić co najmniej jeden mały query slice, preferencyjnie `GetProjectTaskDetails`;
- utrzymać osobne kontrakty HTTP, modele application i encje domenowe tam, gdzie pełnią różne role;
- rejestrować endpointy, handlery, porty, adaptery i opcjonalne workery przez jeden entry point modułu;
- utrzymać jedną aplikację, jeden `ApplicationDbContext`, jedną historię migracji i wspólną bazę PostgreSQL;
- nie zmieniać istniejących tras, JSON ani status codes tylko z powodu reorganizacji;
- przetestować sukces, autoryzację, walidację, błędy persistence i publiczny kontrakt API;
- porównać koszt oraz czytelność pilota ze starszym stylem przed migracją kolejnego modułu;
- aktualizować istniejący kod przy okazji realnej zmiany albo jawnie zaplanowanego slice'a, nie przez masowe przenoszenie plików.

Moduł oznacza granicę biznesową, a slice pojedynczy przypadek użycia. `Domain`,
`Application`, `Infrastructure` i `API` pozostają odpowiedzialnościami technicznymi;
sam folder `Modules` bez ograniczonych zależności nie jest jeszcze modularnością.

## Test plan

### Unit tests

- niepoprawne zmiany stanu projektu;
- zakazane przejścia statusów zadania;
- reguły członkostwa i roli;
- fabryka `User` oraz metody zmian profilu i stanu bezpieczeństwa;
- value objects i normalizacja;
- mapowanie wyników application service.

### Integration tests

- transakcja akceptacji zaproszenia nie zostawia częściowego członkostwa;
- równoległa akceptacja tego samego zaproszenia kończy się jednym sukcesem i jednym `409`;
- błąd zapisu notification/outbox ma ustaloną reakcję;
- usunięcie członka atomowo odpina jego zadania, usuwa membership i zapisuje aktywność;
- dwa zapisy tej samej wersji zwracają jeden sukces i jeden `409`;
- constrainty bazy blokują duplikaty;
- dashboard używa poprawnej agregacji i nie zwraca błędnych statystyk;
- usunięcie lub archiwizacja respektuje ustalone delete behaviors.

### Testy PostgreSQL

Concurrency i transakcje należy testować na PostgreSQL, ponieważ zachowanie InMemory nie odzwierciedla blokad, constraintów i wyjątków relacyjnej bazy.

## Definition of Done

- granice agregatów są opisane w dokumencie architektury lub ADR;
- `Project` nie pozwala na krytyczne niepoprawne zmiany przez publiczne settery;
- każdy większy przypadek użycia ma jasno opisany punkt transakcji;
- co najmniej jeden realny konflikt zapisu zwraca `409`;
- test concurrency działa na PostgreSQL;
- dashboard wykonuje agregacje po stronie bazy;
- mapping i porty są spójne w dotkniętym module;
- pilot `ProjectTasks` zawiera co najmniej jeden command slice i jeden query slice;
- nowy większy przypadek użycia ma własny slice, rejestrację modułu i testy, jeśli jego granica została już potwierdzona;
- publiczne trasy i kontrakty pilota pozostają zgodne albo ich zmiana jest osobno uzasadniona;
- wynik pilota opisuje koszt, korzyści i kryteria wyboru następnego modułu;
- istnieją testy reguł domenowych i partial failure.

## Poza zakresem V3

- pełna migracja wszystkich encji do rozbudowanego DDD;
- Event Sourcing;
- MediatR użyty tylko dla ukrycia prostego wywołania;
- mikroserwisy;
- osobna baza dla każdego modułu;
- abstrakcja repository bez konkretnej potrzeby;
- pełna migracja wszystkich funkcji do vertical slices;
- osobny projekt `.csproj`, pakiet NuGet lub generator dla każdego modułu;
- pełna reorganizacja frontendu przed ustabilizowaniem kontraktów pilota.

## Pytania kontrolne

- Dlaczego `ProjectTask` może być osobnym agregatem albo częścią `Project` i jakie są konsekwencje obu decyzji?
- Która reguła wymaga dostępu do bazy i dlatego nie powinna być metodą encji?
- Dlaczego optimistic concurrency jest lepsze od blokowania pesymistycznego w tym przypadku?
- Co dokładnie ma być atomowe przy akceptacji zaproszenia?
- Dlaczego test z EF InMemory nie wystarcza do potwierdzenia zachowania concurrency?
