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
| 3. Application services i porty | 90% | Backendowe use case'y `ProjectTasks` i `Projects` mają focused handlery i porty; usunięto szerokie project services, a współpraca modułów przechodzi przez jawne porty. |
| 4. Mapping i kontrakty | 75% | DTO i kontrakty HTTP są obecne oraz dokumentowane, a endpointy obu modułów mają slice-specific adaptery; frontend i część starszych application models pozostają przejściowe. |
| 5. Transakcje i partial failure | 90% | Task/member/invitation/activity/notification/outbox są stagingowane w jednym scoped context i zapisywane jednym commitem; cleanup plików korzysta z trwałego outboxu, a rollbacki są pokryte testami PostgreSQL. |
| 6. Optimistic concurrency | 75% | `Project`, `ProjectInvitation` i `ProjectTask` mają tokeny wersji, konflikty są mapowane na `409`, a konflikty zapisów projektu i zadania, wyścig akceptacji oraz równoległe tworzenie zaproszeń są testowane na PostgreSQL. |
| 7. Zapytania dashboardu | 80% | `ProjectTasks` posiada jawny dashboard read port; `Projects` nie czyta jego `DbSet`, a agregacje SQL, zakresy dat, limity i indeks są zachowane. Benchmark obciążeniowy nadal należy do V6. |
| 8. Pilotaż modularnego VSA | 100% | Backendowe moduły `ProjectTasks` i `Projects` mają komplet slice'ów, modułowe entry pointy i guardrails DI/route/dependency. Pełne testy PostgreSQL, backend unit/integration, Release build oraz regresja frontendu przechodzą; feature-first frontend pozostaje osobnym późniejszym inkrementem. |

**Postęp V3: 65%**.

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

Backendowy inkrement modularizacji został zwalidowany na dwóch granicach.
`ProjectTasks` ma osobne slice'y CRUD, comments, attachments i deadline reminders,
a `Projects` ma slice'y lifecycle, membership, invitations, activity i dashboard.
Rejestracje są skupione odpowiednio w `ProjectTasksModule` i `ProjectsModule`.
Szerokie kontrolery oraz project application services zostały usunięte.

Współpraca modułów jest jawna: usunięcie członka używa write portu należącego do
`ProjectTasks`, a dashboard używa należącego do niego read portu statystyk. Moduły
przekazują identyfikatory i read modele, nie encje. `IProjectTaskAccess`,
`IProjectTaskCommandStore` oraz `ProjectTaskView` pozostają świadomie przejściowymi
kontraktami współdzielonymi wewnątrz obszaru tasków.

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

Akceptacja zaproszenia, bezpośrednie dodanie członka oraz usunięcie członka stage'ują
wszystkie zmiany w jednym scoped `ApplicationDbContext`. Jeden końcowy
`SaveChangesAsync` obejmuje relacyjną transakcją membership, invitation, activity,
notification i email outbox. Usunięcie członka dodatkowo korzysta z write portu
`ProjectTasks`, który stage'uje unassign bez własnego commitu. Nie jest potrzebna
ręczna transakcja ani generyczny unit of work dla pojedynczego zapisu.

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

Tworzenie zaproszenia dodatkowo korzysta z częściowego unikalnego indeksu
`(ProjectId, InvitedUserId, Status) WHERE Status = 'Pending'`. Dzięki filtrowi
historyczne zaproszenia nie blokują kolejnych. Handler zmienia wygasłe rekordy
`Pending` na `Expired`, a naruszenie indeksu przez równoległy insert zwraca `409`.
Migracja przed utworzeniem indeksu porządkuje ewentualne istniejące duplikaty,
pozostawiając jako `Pending` wyłącznie najnowszy rekord dla danej pary.

Nie trzeba dodawać concurrency tokenu do każdej tabeli. Wybór powinien wynikać z ryzyka utraty zmian.

### 7. Zapytania dashboardu

- zidentyfikować miejsca, gdzie pobierane są całe kolekcje tylko po to, aby policzyć statystyki;
- zastąpić je projekcjami i agregacjami SQL;
- używać zakresów zamiast nakładania funkcji na kolumny filtrowane po dacie;
- dodać indeks tylko po sprawdzeniu planu zapytania;
- porównać plan zapytania przed i po zmianie, gdy istnieje wiarygodny baseline;
- zachować czytelność query store.

### 8. Pilotaż modułu biznesowego zawierającego vertical slices

Pierwszym pilotem pozostaje `ProjectTasks`, ponieważ ma osobny agregat, jawne reguły
dostępu, feature-specific ports oraz testy integracyjne. Standard został następnie
potwierdzony przez kompletny backendowy moduł `Projects`. Frontend pozostaje poza
tym inkrementem i ma zostać przeniesiony feature-first dopiero po ustabilizowaniu
kontraktów backendu.

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
- równoległe utworzenie zaproszenia dla tej samej pary projekt-użytkownik pozostawia
  tylko jeden rekord `Pending`, a wygasły rekord może zostać bezpiecznie zastąpiony;
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
- testy architektoniczne wykrywają brak DI handlera, duplikat route oraz bezpośredni
  `ApplicationDbContext` w module controller/handler.

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
