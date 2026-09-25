# V6: Wydajność i niezawodność

## Cel

V6 uczy optymalizacji na podstawie pomiarów oraz projektowania odporności na retry, chwilowe błędy i większą liczbę danych. Nie należy zaczynać od dodawania cache lub kolejki. Najpierw trzeba mieć hipotezę, pomiar i kryterium sukcesu.

## Status realizacji

Stan na: **2026-09-25**.

| Obszar | Postęp | Status i dowód |
|---|---:|---|
| 1. Baseline i pomiary | 85% | Protokół, runner oraz opt-in test authenticated API baseline'u są przygotowane w `doc/V6_BASELINE.md`, `scripts/Measure-ApiBaseline.ps1` i `doc/V6_LARGE_FIXTURE_API_BASELINE.md`; istnieje powtarzalny pomiar 10 000 zadań z p50/p95/p99, error rate, payloadem, throughputem, czasem EF oraz raportem before/after dla pierwszego indeksu. |
| 2. EF Core i PostgreSQL | 70% | Istnieją plany PostgreSQL, rzeczywisty wygenerowany SQL EF, liczba round-tripów i rozdzielenie czasu EF od czasu HTTP; pierwszy indeks paginacji został dodany dopiero po pozytywnym pomiarze before/after. |
| 3. Cache | 0% | Brak uzasadnionego przypadku cache wymagającego implementacji. |
| 4. Idempotencja i retry | 55% | Powiadomienia mają jawny kontrakt `userId + deduplicationKey`, ochronę przed wyścigiem zapisu oraz outbox z warunkowym claim/lease, retry i dead-letter po trzeciej porażce; HTTP idempotency keys oraz provider-level email idempotency pozostają poza zakresem. |
| 5. Background processing | 80% | Outbox ma atomowy claim PostgreSQL, pięciominutowy lease, odzyskiwanie wygasłych rekordów, warunkowe finalizowanie, jawny dead-letter status oraz trzy gauge metrics dla kolejki; provider delivery receipts pozostają do wykonania. |
| 6. Frontend request coordination | 65% | `HttpClient` ma single-flight refresh, a lista zadań anuluje poprzednie requesty i odrzuca spóźnione odpowiedzi; scenariusze offline, retry UI i pozostałe read flows pozostają do walidacji. |

**Postęp V6: 30%**.

V6 powinien ruszyć dopiero po wybraniu scenariuszy, danych testowych i mierzalnego kryterium sukcesu.

Pierwszy slice V6 definiuje trzy read-only scenariusze API, fixture danych i powtarzalny
runner baseline'u. Test dużego fixture łączy authenticated HTTP p95, rzeczywisty
SQL EF i plany PostgreSQL dla 10 000 zadań. Nie wprowadza cache ani Redis;
zmiana schematu została dodana dopiero po zebraniu pomiarów.

Pierwsza zmiana schematu została wdrożona dopiero po eksperymencie na tym
fixture: `ProjectTasks(ProjectId, CreatedAt DESC)` skrócił task-page p95 z
`22.272 ms` do `19.060 ms` i zmienił plan z pełnego odczytu tasków na odczyt
pierwszych 20 rekordów przez indeks. Wynik i ograniczenia opisuje
`doc/V6_QUERY_PLAN_FINDINGS.md`.

Drugi slice V6 formalizuje idempotencję powiadomień. Dla stabilnego klucza
biznesowego powtórzenie zapisu jest sukcesem bez tworzenia drugiego
powiadomienia ani drugiego wpisu email outbox. Kontrakt i granice opisuje
`doc/V6_IDEMPOTENCY.md`.

Trzeci slice V6 koordynuje wiele instancji workera email outbox. Rekord jest
atomowo przejmowany przez warunkowy `UPDATE`, a wygasły lease może zostać
odzyskany przez kolejnego workera. Sukces i retry są zapisywane tylko wtedy,
gdy lease nadal należy do wykonującej instancji. Dowód konkurencji i ograniczenia
opisuje `doc/V6_OUTBOX_LEASING.md`.

Czwarty slice V6 nadaje trwały status rekordom, które wyczerpały trzy próby.
`DeadLetteredAt` jest ustawiane atomowo przy trzeciej porażce, a zwykły worker
nie wybiera takiego rekordu ponownie. Przywrócenie do kolejki wymaga jawnej
operacji operatorskiej resetującej status i licznik prób.

Piąty slice V6 dodaje bezpieczny endpoint Prometheus `/metrics` dla outboxa.
Eksportowane są liczba nieprzetworzonych rekordów, wiek najstarszego rekordu
oraz liczba rekordów dead-letter. Wszystkie wartości są liczone jednym
agregującym zapytaniem PostgreSQL i mają test integracyjny.

Szósty slice V6 koordynuje odświeżanie sesji po równoległych odpowiedziach `401`.
Wspólny `HttpClient` wykonuje najwyżej jeden refresh dla jednego okna
współbieżności, a pozostałe requesty czekają na ten sam rezultat. Jeśli token
został już podmieniony przez inny request, kolejny request jest ponawiany bez
uruchamiania drugiego refresh. Publiczne requesty z `skipAuth` nie uruchamiają
mechanizmu odświeżania. Kontrakt i dowody opisuje
`doc/V6_FRONTEND_REQUEST_COORDINATION.md`.

Siódmy slice V6 anuluje nieaktualne odczyty listy zadań. Zmiana projektu,
strony, wyszukiwania lub filtrów przerywa poprzedni request przez
`AbortController`, a provider sprawdza sygnał przed zapisaniem odpowiedzi do
stanu. Spóźniona odpowiedź nie może już nadpisać nowszych wyników ani zgłosić
błędu zamierzonego anulowania. Kontrakt i dowód opisuje
`doc/V6_FRONTEND_REQUEST_CANCELLATION.md`.

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

### 7. Koszt granic modułowych i vertical slices

Po wdrożeniu kilku slice'ów należy zmierzyć, czy nowe granice nie powodują:

- dodatkowych round-tripów do PostgreSQL;
- problemów N+1 ukrytych za portami;
- wielokrotnego wykonywania tych samych kontroli dostępu;
- nadmiernego mapowania i alokacji modeli pośrednich;
- rozszerzania jednej operacji na niepotrzebnie wiele zapisów lub transakcji;
- synchronicznego łańcucha eventów trudnego do obserwowania i testowania.

Vertical Slice Architecture ma przede wszystkim poprawić lokalność zmian i kompletność
funkcji. Nie jest automatyczną optymalizacją wydajności. Port lub event, który zwiększa
koszt bez ochrony realnej granicy, powinien zostać uproszczony.

## Test plan

- benchmark endpointu przed i po projekcji SQL;
- test dużego zbioru danych;
- test planów zapytań i indeksów;
- before/after test dla zmierzonego indeksu paginacji;
- load test najważniejszych scenariuszy;
- test retry bez duplikowania efektu;
- test dwóch workerów przetwarzających ten sam rekord;
- test niedostępnego cache;
- test timeoutu i circuit breakera;
- test request cancellation;
- porównanie liczby zapytań i czasu reprezentatywnego slice'a ze stanem bazowym;
- pomiar regresji po zmianie.

## Definition of Done

- istnieje raport baseline i raport po zmianie;
- najważniejsze zapytania mają sprawdzony plan;
- pagination, projection i tracking są dobrane do konkretnego przypadku;
- retry i timeout mają ustalone granice;
- co najmniej jedna operacja jest idempotentna i ma test powtórzenia;
- worker ma strategię retry i obsługi trwałego błędu;
- cache, jeśli dodany, ma określony TTL, invalidation i fallback;
- reprezentatywne slice'y nie wprowadzają nieuzasadnionych round-tripów, N+1 ani wielokrotnych kontroli dostępu;
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
