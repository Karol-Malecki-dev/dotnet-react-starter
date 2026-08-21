# V6: Wydajność i niezawodność

## Cel

V6 uczy optymalizacji na podstawie pomiarów oraz projektowania odporności na retry, chwilowe błędy i większą liczbę danych. Nie należy zaczynać od dodawania cache lub kolejki. Najpierw trzeba mieć hipotezę, pomiar i kryterium sukcesu.

## Zakres implementacyjny

### 1. Baseline i pomiary

- określić najważniejsze scenariusze API;
- zmierzyć p50, p95 i p99 latency;
- zmierzyć error rate i throughput;
- określić rozmiary payloadów;
- zebrać czas zapytań PostgreSQL;
- zdefiniować dane testowe o realistycznym rozmiarze;
- zapisać środowisko i parametry pomiaru.

Bez baseline nie można uczciwie stwierdzić, że optymalizacja pomogła.

### 2. EF Core i PostgreSQL

Przećwiczyć i udokumentować:

- `IQueryable` kontra `IEnumerable`;
- projekcje do DTO;
- `AsNoTracking`;
- problemy N+1;
- indeksy jedno- i wielokolumnowe;
- indeksy częściowe;
- constraints;
- `EXPLAIN ANALYZE`;
- paginację offsetową;
- keyset pagination tam, gdzie jest uzasadniona;
- wpływ sortowania i filtrów na plan zapytania;
- bezpieczne migracje dużych tabel.

### 3. Cache

Cache-aside lub Redis można dodać tylko po wskazaniu konkretnego przypadku, na przykład:

- rzadko zmieniana konfiguracja;
- kosztowny dashboard;
- często odczytywany runtime config.

Dla każdego cache trzeba określić:

- TTL;
- klucz i zakres danych;
- invalidation;
- zachowanie przy niedostępnym cache;
- ryzyko nieaktualnych danych;
- koszt infrastruktury.

### 4. Idempotencja i retry

- zidentyfikować retryowalne komendy;
- dodać idempotency key tam, gdzie klient może bezpiecznie ponowić żądanie;
- zapisać wynik operacji dla powtórzonego klucza;
- stosować retry tylko dla błędów przejściowych;
- nie ponawiać bezmyślnie operacji nieidempotentnych;
- dodać timeout i backoff;
- rozważyć circuit breaker dla zewnętrznych usług.

Przykładowe miejsca: wysyłka email, tworzenie zaproszenia i żądania do storage.

### 5. Background processing

Obecny outbox i workery powinny zostać przeanalizowane pod kątem wielu instancji:

- row claiming lub lease;
- retry count;
- next attempt time;
- dead-letter lub trwały status błędu;
- idempotentne wysyłanie;
- monitoring opóźnienia kolejki;
- graceful shutdown;
- brak podwójnego przetwarzania przy dwóch workerach.

Hangfire, Quartz, RabbitMQ lub Azure Service Bus są opcjami do porównania, a nie obowiązkową listą instalacji.

### 6. Frontend request coordination

Pomocniczo można poprawić:

- single-flight refresh dla wielu równoległych `401`;
- anulowanie nieaktualnych requestów;
- debounce wyszukiwania;
- unikanie wielokrotnego ładowania tego samego dashboardu;
- czytelne stany retry i offline.

## Test plan

- benchmark endpointu przed i po projekcji SQL;
- test dużego zbioru danych;
- test planów zapytań i indeksów;
- load test najważniejszych scenariuszy;
- test retry bez duplikowania efektu;
- test dwóch workerów przetwarzających ten sam rekord;
- test niedostępnego cache;
- test timeoutu i circuit breakera;
- test request cancellation;
- pomiar regresji po zmianie.

## Definition of Done

- istnieje raport baseline i raport po zmianie;
- najważniejsze zapytania mają sprawdzony plan;
- pagination, projection i tracking są dobrane do konkretnego przypadku;
- retry i timeout mają ustalone granice;
- co najmniej jedna operacja jest idempotentna i ma test powtórzenia;
- worker ma strategię retry i obsługi trwałego błędu;
- cache, jeśli dodany, ma określony TTL, invalidation i fallback;
- wyniki pomiarów są zapisane w dokumentacji.

## Poza zakresem V6

- skalowanie do milionów użytkowników bez danych uzasadniających taki cel;
- cache każdej odpowiedzi;
- kolejka tylko dlatego, że jest popularna;
- optymalizacje bez benchmarku;
- przedwczesne rozdzielanie systemu na usługi.

## Pytania kontrolne

- Jak udowodnisz, że zapytanie jest problemem?
- Kiedy `IQueryable` wykonuje się w bazie, a kiedy dane są już w pamięci?
- Dlaczego retry może utworzyć duplikat?
- Co stanie się, gdy worker padnie po wysłaniu emaila, ale przed oznaczeniem outbox jako przetworzonego?
- Jak rozpoznać, że cache pogorszył poprawność systemu?
- Czym różni się timeout od cancellation requestu?
