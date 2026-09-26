# Backlog kandydatów funkcjonalnych

## Status dokumentu

To jest backlog do analizy, nie zobowiązanie do implementacji. Kandydat otrzymuje status
`proposed` dopiero po opisaniu problemu i dowodów w pliku utworzonym na podstawie szablonu.

Statusy rozróżniają implementację od dowodu środowiskowego:

- `next` - rekomendowany najbliższy inkrement;
- `accepted / planned` - decyzja została podjęta i ma plan implementacji;
- `proposed` - problem opisany, ale zakres nie został jeszcze zaakceptowany;
- `implemented / evidence pending` - kod istnieje, lecz brakuje wskazanego dowodu runtime;
- `parked` - brak potwierdzonej potrzeby.

Identyfikatory `CAP-*` oznaczają capability produktowe, a `DX-*` wewnętrzne
usprawnienie developer experience potrzebne do bezpiecznego dostarczania capability.

Oceny są hipotezą roboczą:

- **wartość** - przewidywana wartość dla użytkownika lub operatora;
- **nauka** - wartość dla rozwoju projektu i zrozumienia dojrzałych wzorców;
- **ryzyko** - koszt i ryzyko implementacji, od 1 (niskie) do 5 (wysokie);
- **dowód** - obserwacja lub pomiar potrzebny przed rozpoczęciem pracy.

## Macierz kandydatów

| ID | Kandydat | Priorytet | Wartość | Nauka | Ryzyko | Stan |
| --- | --- | --- | ---: | ---: | ---: | --- |
| DX-001 | Prosty golden path VSA i pomiar kosztu slice'a | P0 | 5 | 5 | 2 | next |
| CAP-001 | Domknięcie modułu Notifications i kontraktu zdarzeń | P0 | 5 | 5 | 3 | next |
| DX-002 | Inkrementalna adopcja MediatR w VSA | P0 | 4 | 5 | 3 | implemented / evidence pending |
| CAP-002 | Account security audit | P0 | 5 | 5 | 3 | implemented / evidence review |
| CAP-003 | Authorized workspace search | P0 | 4 | 4 | 3 | implemented / evidence review |
| CAP-004 | Production-grade attachment lifecycle | P0 | 4 | 5 | 4 | implemented / V5 evidence pending |
| CAP-005 | Critical browser E2E matrix | P0 | 5 | 5 | 3 | implemented baseline / matrix closure |
| CAP-006 | Durable real-time notification delivery | P1 | 4 | 5 | 4 | proposed after CAP-001/CAP-007 |
| CAP-007 | Permission matrix and workspace boundary | P1 | 5 | 5 | 4 | proposed |
| CAP-008 | Task workflow and approval states | P1 | 4 | 4 | 4 | proposed |
| CAP-009 | Idempotent commands and retry contracts | P1 | 4 | 5 | 4 | V6 candidate |
| CAP-010 | Saved views, filters and personal workspace preferences | P2 | 3 | 3 | 2 | proposed |
| CAP-011 | Outbound integrations and signed webhooks | P2 | 3 | 5 | 5 | proposed |
| CAP-012 | Passkeys, OIDC/SSO lub multi-tenancy | P3 | 5 | 5 | 5 | parked as separate decisions |

## Recommended order

### 1. Ustabilizować ergonomię VSA i kontrakt Notifications

Najbliższe dwa inkrementy to `DX-001` i `CAP-001`:

- utrzymać jeden krótki golden path dla command i query slice'a;
- nie tworzyć pustych elementów tylko dla symetrii katalogów;
- zmierzyć ręczne kroki i czas dodania kolejnych slice'ów;
- wyrównać backendowe i frontendowe typy Notifications;
- domknąć linkowanie, autoryzację i test kontraktu.

Nie należy ponownie implementować account security audit, workspace search,
attachment lifecycle ani browser E2E tylko dlatego, że starsze plany nadal opisują je
jako brakujące. Najpierw trzeba zweryfikować istniejący kod i zamknąć wyłącznie
pozostałe dowody.

Dowody V5 dla stagingu, backupu, restore, rollbacku i alertów są osobnym torem. Nie
blokują pracy lokalnej, ale blokują deklarację production-ready.

### 2. Wdrożyć MediatR bez przepisywania architektury

`DX-002` jest zaakceptowanym krokiem po `CAP-001`. Najpierw migruje jedno query i
jeden command, dodaje bezpieczny telemetry behavior oraz guardrails DI/handlerów.
Po przejściu bramki nowe slice'y używają MediatR, a istniejące moduły są migrowane
pojedynczo.

MediatR odpowiada wyłącznie za dispatch in-process. Nie zastępuje focused ports,
autoryzacji zasobowej, transakcji, outboxa ani przyszłego brokera.

### 3. Uporządkować role przed nowym transportem

`CAP-007` powinien zacząć się od jawnej macierzy uprawnień dla obecnych ról projektu.
Dopiero wynik analizy powinien rozstrzygnąć, czy potrzebny jest osobny `Workspace`,
role niestandardowe, capability-based permissions albo polityki zależne od zasobu.

Pierwsza wersja nie powinna automatycznie oznaczać multi-tenancy. Należy najpierw
sprawdzić, czy obecny model `Project` i członkostwa rzeczywiście blokuje planowany
workflow.

### 4. Dodać real-time jako warstwę dostawy

`CAP-006` jest najlepszym kandydatem po ustabilizowaniu Notifications. Powiadomienie
powinno najpierw zostać zapisane trwale i mieć stabilny identyfikator oraz typ zdarzenia.
Transport real-time jest tylko szybką informacją dla otwartego klienta.

Minimalny zakres pierwszej wersji:

- dostawa zdarzeń do zalogowanego użytkownika;
- reconnect i ponowne pobranie stanu z API;
- brak utraty danych, gdy klient był offline;
- fallback do istniejącego odświeżania;
- autoryzacja po stronie serwera dla każdego połączenia i subskrypcji;
- test dwóch użytkowników oraz test restartu procesu.

Na tym etapie nie ma uzasadnienia dla Kafki, Redis Pub/Sub ani mikroserwisów. Najpierw
należy wybrać prosty transport, zmierzyć ograniczenia jednej instancji i opisać moment,
w którym potrzebna byłaby koordynacja rozproszona.

### 5. Rozszerzyć domenę zadaniową dopiero po decyzji produktowej

`CAP-008` może dodać review, approval, checklisty, zależności lub SLA, ale nie należy
łączyć wszystkich tych elementów w jeden duży zakres. Każdy nowy stan powinien mieć:

- określonego aktora i dozwolone przejście;
- historię zmiany lub wpis activity;
- zachowanie przy równoległej edycji;
- kontrakt powiadomienia;
- test API i browser E2E, jeśli jest to krytyczny workflow.

### 6. Wprowadzać niezawodność punktowo

`CAP-009` ma sens dla operacji, które mogą być ponawiane po timeoutach lub awarii:
email outbox, webhooki, uploady i komendy uruchamiane z UI. Idempotency key nie powinien
być obowiązkowym polem każdego endpointu bez dowodu, że retry jest realnym problemem.

## Karty kandydatów

### DX-001: prosty golden path VSA

**Problem:** poprawny slice jest rozłożony między kilka projektów i wymaga ręcznej
rejestracji, przez co łatwo pominąć kontrakt, walidację albo test.

**Wartość:** krótsze wdrożenie kolejnego feature'a oraz jawny baseline do późniejszego
porównania z dispatchingiem MediatR.

**Granica pierwszej wersji:** dokumentowana ścieżka command/query, istniejące slice'y
referencyjne, modułowa checklista oraz pomiar czasu i ręcznych kroków. Bez generatora.

**Dowody akceptacji:** następny command i query powstają według instrukcji, przechodzą
guardrails DI/route/dependency, a zebrany pomiar wskazuje realne źródła tarcia.

### DX-002: inkrementalna adopcja MediatR

**Problem:** obecne jawne handlery potwierdziły granice VSA, ale każdy slice ma własny
interfejs i bezpośrednią rejestrację. Projekt nie ćwiczy wspólnego dispatchingu i
pipeline behaviors spotykanych w wielu aplikacjach ASP.NET Core.

**Wartość:** praktyczna znajomość `IRequest`, `IRequestHandler`, `ISender`,
rejestracji DI i bezpiecznych pipeline behaviors bez utraty wypracowanych granic
modułów.

**Granica pierwszej wersji:** query `GetProjectDetails`, command
`CreateProjectTask`, jeden telemetry behavior oraz testy handler uniqueness, DI,
publicznego kontraktu i cancellation. Bez globalnej transakcji, retry, cache,
validation behavior i `MediatR.INotification`.

**Kolejność migracji:** nowe slice'y, następnie `Notifications`, `Projects` i
`ProjectTasks` (zakres ukończony); `Identity` pozostaje poza zakresem i migruje się
tylko przy realnej zmianie use case'a.

**Dowody akceptacji:** publiczne kontrakty pilota są niezmienione, każdy request ma
dokładnie jeden handler, `Domain` nie zależy od MediatR, logowanie nie zapisuje
payloadów ani sekretów, a dokumentacja wskazuje MediatR jako domyślny standard dla
nowych slice'ów.

**Decyzja:** zaakceptowana w
[`ADR incremental MediatR adoption`](../ROADMAP/14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md).

### CAP-001: domknięcie kontraktu Notifications

**Problem:** backendowy katalog typów powiadomień jest szerszy od katalogu
frontendowego, co zwiększa ryzyko nieobsłużonego zdarzenia i błędnego linkowania.

**Wartość:** jeden stabilny kontrakt dla kolejnych workflowów i bezpieczna baza pod
real-time.

**Granica pierwszej wersji:** wyrównanie typów, wymaganych pól, zachowania nieznanego
typu i linków do zasobów wraz z testem kontraktu oraz jednym browser E2E.

**Dowody akceptacji:** każdy publiczny typ backendowy ma jawne zachowanie frontendowe,
a użytkownik nie może odczytać ani zmienić cudzego powiadomienia.

### CAP-006: Durable real-time notification delivery

**Problem do potwierdzenia:** użytkownik musi ręcznie odświeżać widok, aby zobaczyć ważne
zdarzenie, mimo że powiadomienie jest już zapisane w bazie.

**Wartość:** szybsza informacja o zaproszeniu, przypisaniu zadania, komentarzu i konflikcie.

**Granica pierwszej wersji:** dostawa istniejących powiadomień do użytkownika, bez zmiany
źródła prawdy i bez obietnicy dostarczenia dokładnie raz. Klient musi umieć odtworzyć
stan przez API po reconnect.

**Decyzje do podjęcia:** SignalR kontra SSE, model grup/subskrypcji, autoryzacja połączenia,
wersja zdarzenia, kolejność, deduplikacja po `NotificationId` i zachowanie wielu kart.

**Dowody akceptacji:** test reconnect, offline/replay, drugi użytkownik nie otrzymuje
cudzego zdarzenia, restart aplikacji nie usuwa powiadomienia, metryki połączeń i błędów.

### CAP-007: Permission matrix and workspace boundary

**Problem do potwierdzenia:** rosnąca liczba workflowów może prowadzić do rozproszonych,
trudnych do audytu warunków `Owner`/`Member`/`Viewer`.

**Wartość:** przewidywalne role, mniej przypadkowych luk autoryzacji i prostsze projektowanie
nowych workflowów.

**Granica pierwszej wersji:** dokumentacja macierzy uprawnień i jeden wybrany przypadek,
który obecny model obsługuje nieczytelnie. Nie tworzyć od razu systemu custom roles.

**Dowody akceptacji:** tabela decyzji dla każdego endpointu, testy anonymous/forbidden,
testy dostępu do zasobu po zmianie roli oraz test frontendowego feature gatingu bez
traktowania go jako zabezpieczenia.

### CAP-008: Task workflow and approval states

**Problem do potwierdzenia:** `Todo`/`InProgress`/`Done` nie opisuje procesu review lub
akceptacji, którego wymaga rzeczywisty sposób pracy.

**Wartość:** lepsza widoczność odpowiedzialności i mniej zmian wykonywanych poza systemem.

**Ryzyko:** rozszerzenie stanów wpływa na agregat, activity, notifications, dashboard,
filtry, E2E i optimistic concurrency.

**Dowody akceptacji:** potwierdzony workflow użytkownika, diagram przejść, reguły roli,
konflikt dwóch zmian oraz migracja danych dla istniejących zadań.

### CAP-009: Idempotent commands and retry contracts

**Problem do potwierdzenia:** retry klienta lub workera może utworzyć duplikat zaproszenia,
powiadomienia, uploadu albo webhooka.

**Wartość:** bezpieczne ponawianie po timeoutach i restartach.

**Granica pierwszej wersji:** jeden przypadek o zmierzonym ryzyku, z kluczem idempotencji,
retencją rekordu i jasno opisanym wynikiem ponownego żądania.

**Dowody akceptacji:** powtórzone żądanie zwraca ten sam wynik bez drugiego efektu,
wygaśnięcie klucza jest udokumentowane, a retry i awaria są pokryte testem PostgreSQL
albo testem workera.

### CAP-010: Saved views, filters and preferences

**Problem do potwierdzenia:** użytkownik regularnie odtwarza te same filtry i sortowanie,
a obecny dashboard nie wspiera tego workflowu.

**Wartość:** produktywność bez wprowadzania ciężkiej infrastruktury.

**Warunek:** najpierw zebrać dane z użycia filtrów lub potwierdzenie w wymaganiach.
To dobry kandydat produktowy, ale nie powinien wyprzedzać bezpieczeństwa i E2E.

### CAP-011: Outbound integrations and signed webhooks

**Problem do potwierdzenia:** użytkownik potrzebuje synchronizacji z zewnętrznym systemem
albo automatyzacji poza aplikacją.

**Wartość:** rozszerzalność produktu i realny przypadek dla retry, podpisów oraz outboxa.

**Warunek:** konkretny pierwszy odbiorca i zdarzenie. Przed implementacją trzeba ustalić
wersjonowanie payloadu, podpis HMAC, replay protection, retry, dead letter i usuwanie
sekretów.

### CAP-012: Passkeys, OIDC/SSO or multi-tenancy

To są osobne kierunki, nie jeden pakiet „enterprise”. Każdy wymaga osobnego problemu,
modelu zagrożeń, kosztu operacyjnego i ADR-u. Pozostają odroczone do czasu, gdy obecna
aplikacja lub docelowy sposób jej użycia dostarczy dowodów.

## Warunki odrzucenia kandydata

Kandydata należy odłożyć albo odrzucić, jeśli:

- rozwiązuje wyłącznie problem wyobrażony, bez scenariusza użytkownika;
- wymaga nowej infrastruktury, ale nie ma baseline'u ani metryki;
- dubluje istniejący etap V3-V6;
- zwiększa powierzchnię autoryzacji bez testowalnej macierzy uprawnień;
- nie ma sensownego rollbacku lub migracji danych;
- nie da się wskazać najtańszego testu wykrywającego jego główną regresję.

## Definition of Ready

Kandydat jest gotowy do planowania implementacji, gdy ma:

- zaakceptowany opis problemu i użytkownika;
- zakres oraz listę rzeczy poza zakresem;
- aktorów, role i reguły autoryzacji;
- diagram stanów albo przepływu;
- rozstrzygniętą granicę danych i strategię concurrency;
- kontrakt API i zachowanie błędów;
- plan testów unit/integration/PostgreSQL/browser E2E;
- plan obserwowalności, rollout i rollback;
- wskazanie, czy potrzebuje ADR-u;
- jeden branch obejmujący jeden spójny temat.

Do opisania tych punktów służy
[`FEATURE_PROPOSAL_TEMPLATE.md`](FEATURE_PROPOSAL_TEMPLATE.md). Rekomendowana
kolejność znajduje się w [`DEVELOPMENT_PLAN.md`](DEVELOPMENT_PLAN.md).
