# V3: Domena, transakcje i współbieżność

## Cel

V3 ma przeprowadzić projekt od poprawnie działającego modularnego monolitu do systemu z wyraźnymi granicami odpowiedzialności. Najważniejsze pytanie tego etapu brzmi:

> Która reguła należy do domeny, która do przypadku użycia, a która do infrastruktury?

Nie chodzi o mechaniczne dodanie wszystkich wzorców DDD. Chodzi o uzasadnienie granic i ochronę najważniejszych niezmienników.

## Stan wyjściowy

`ProjectTask` jest obecnie najlepiej zamodelowaną encją: posiada prywatny konstruktor, ograniczone settery i metody domenowe. `Project` i `User` mają słabszą enkapsulację. Moduł zadań używa już feature-specific ports, co daje dobry punkt wyjścia do dalszego rozdzielania odpowiedzialności.

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
- zmiana członka oraz wyczyszczenie przypisań zadań;
- zmiana biznesowa, activity i notification/outbox;
- reset lub zmiana hasła oraz unieważnienie sesji.

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

Nie trzeba dodawać concurrency tokenu do każdej tabeli. Wybór powinien wynikać z ryzyka utraty zmian.

### 7. Zapytania dashboardu

- zidentyfikować miejsca, gdzie pobierane są całe kolekcje tylko po to, aby policzyć statystyki;
- zastąpić je projekcjami i agregacjami SQL;
- porównać plan zapytania przed i po zmianie;
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
- błąd zapisu notification/outbox ma ustaloną reakcję;
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
