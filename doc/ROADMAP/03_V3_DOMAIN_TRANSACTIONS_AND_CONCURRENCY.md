# V3: Domena, transakcje i współbieżność

## Cel

V3 ma przeprowadzić projekt od poprawnie działającego modularnego monolitu do systemu z wyraźnymi granicami odpowiedzialności. Najważniejsze pytanie tego etapu brzmi:

> Która reguła należy do domeny, która do przypadku użycia, a która do infrastruktury?

Nie chodzi o mechaniczne dodanie wszystkich wzorców DDD. Chodzi o uzasadnienie granic i ochronę najważniejszych niezmienników.

## Stan wyjściowy

`ProjectTask` jest obecnie najlepiej zamodelowaną encją: posiada prywatny konstruktor, ograniczone settery i metody domenowe. `Project` i `User` mają słabszą enkapsulację. Moduł zadań używa już feature-specific ports, co daje dobry punkt wyjścia do dalszego rozdzielania odpowiedzialności.

## Status realizacji

Stan na: **2026-08-29**.

| Obszar | Postęp | Status i dowód |
|---|---:|---|
| 1. Granica agregatów projektu i zadań | 75% | `Project` chroni członkostwo przez metody domenowe, automatycznie tworzy właściciela i ma prywatne settery; `ProjectTask` został przyjęty jako osobny agregat, a niezależność tokenu projektu potwierdza test integracyjny. |
| 2. Model użytkownika i value objects | 25% | `User.Email` używa kanonicznego `Domain.ValueObjects.EmailAddress`, który normalizuje i waliduje wejście; konwerter EF zachowuje tekstową kolumnę i jest pokryty testami jednostkowymi oraz integracyjnymi. Pozostałe dane profilu nadal wymagają oceny. |
| 3. Application services i porty | 50% | Warstwy i feature-specific ports istnieją; część odpowiedzialności nadal wymaga doprecyzowania. |
| 4. Mapping i kontrakty | 50% | DTO i kontrakty HTTP są obecne oraz dokumentowane; pełne rozdzielenie modeli nie jest zakończone. |
| 5. Transakcje i partial failure | 70% | Akceptacja zaproszenia, bezpośrednie dodanie członka oraz usunięcie członka mają relacyjne granice transakcji; usunięcie obejmuje także unassign zadań i aktywność, a rollbacki przy błędzie notification są pokryte dla dwóch workflowów z powiadomieniami. |
| 6. Optimistic concurrency | 70% | `Project`, `ProjectInvitation` i `ProjectTask` mają tokeny wersji, konflikty są mapowane na `409`, a konflikty zapisów projektu i zadania oraz wyścig akceptacji są testowane na PostgreSQL. |
| 7. Zapytania dashboardu | 60% | Statystyki dashboardu są agregowane po stronie SQL, listy overdue/upcoming używają zakresów dat przyjaznych indeksom i limitów, a test PostgreSQL potwierdza użycie indeksu `IX_ProjectTasks_ProjectId_Status_DueDate`; benchmark obciążeniowy nadal należy do V6. |

**Postęp V3: 40%**.

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

W bieżącej iteracji `EmailAddress` jest kanonicznym value objectem domenowym dla `User.Email`.
Przyjmuje tylko poprawny adres, zapisuje go po `Trim()` i `ToLowerInvariant()`,
udostępnia bezpieczne `TryCreate` oraz przechowuje w bazie jako zwykły tekst przez
konwerter EF Core. Kontrakty HTTP i application nadal używają `string`, więc granica
domeny nie zmienia publicznego API. Stary helper `Shared.Helpers.EmailAddress` został
usunięty, aby nie pozostawiać dwóch konkurencyjnych implementacji.

### 3. Application services i porty

- utrzymywać interfejsy przypadków użycia w `Application`;
- rozdzielić kontrakty od implementacji EF w `Infrastructure`;
- preferować porty opisujące konkretną potrzebę funkcji zamiast generycznego repository;
- ograniczyć kontrolery do transportu HTTP, autoryzacji wejścia i mapowania wyniku;
- przenosić reguły biznesowe z kontrolerów i dużych serwisów do odpowiedniego miejsca.

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

## Test plan

### Unit tests

- niepoprawne zmiany stanu projektu;
- zakazane przejścia statusów zadania;
- reguły członkostwa i roli;
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
- istnieją testy reguł domenowych i partial failure.

## Poza zakresem V3

- pełna migracja wszystkich encji do rozbudowanego DDD;
- Event Sourcing;
- MediatR użyty tylko dla ukrycia prostego wywołania;
- mikroserwisy;
- osobna baza dla każdego modułu;
- abstrakcja repository bez konkretnej potrzeby.

## Pytania kontrolne

- Dlaczego `ProjectTask` może być osobnym agregatem albo częścią `Project` i jakie są konsekwencje obu decyzji?
- Która reguła wymaga dostępu do bazy i dlatego nie powinna być metodą encji?
- Dlaczego optimistic concurrency jest lepsze od blokowania pesymistycznego w tym przypadku?
- Co dokładnie ma być atomowe przy akceptacji zaproszenia?
- Dlaczego test z EF InMemory nie wystarcza do potwierdzenia zachowania concurrency?
