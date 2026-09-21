# V4: Kompletność produktu

## Cel

V4 domyka funkcje, które są ważne dla użytecznego produktu, ale nie powinny wyprzedzać stabilizacji backendu. Ten etap łączy brakujące workflowy użytkownika, bezpieczeństwo danych i pełniejsze testy przez frontend.

Frontend pozostaje funkcjonalny i podporządkowany backendowi. Nie celem jest budowa osobnego portfolio frontendowego.

Szczegółowa kolejność branchy, zależności, kontrakty startowe i bramki walidacyjne są
opisane w [13_V4_IMPLEMENTATION_PLAN.md](13_V4_IMPLEMENTATION_PLAN.md).

## Status realizacji

Stan na: **2026-09-21**.

| Obszar | Postęp | Status i dowód |
|---|---:|---|
| 1. Account security audit | 80% | `AccountSecurityEvent` i administracyjny read model istnieją. Pozostaje przegląd kompletności zdarzeń, prywatności metadanych i runtime evidence. |
| 2. Auth lockout UX | 75% | Backendowy lockout, neutralne błędy oraz frontendowa obsługa `401`/`429` istnieją; pozostaje domknięcie scenariuszy czasu odblokowania i regresji browser E2E. |
| 3. Global workspace search | 85% | Autoryzowany `SearchWorkspace` endpoint i osobny read path istnieją; pozostaje potwierdzenie kompletnej macierzy uprawnień i zachowania przy większym zbiorze. |
| 4. Załączniki jako funkcja produkcyjna | 85% | Local/S3 storage, quota, content validation, cleanup, reconciliation i opcjonalny ClamAV są zaimplementowane. V5 nadal wymaga restore i operacyjnego evidence na realnym środowisku. |
| 5. Kompletność activity i notifications | 70% | Trwałe notifications, activity, deduplication key i email outbox istnieją. Backend ma szerszy katalog typów niż frontend; to najbliższa luka kontraktu. |
| 6. Browser E2E | 75% | Playwright obejmuje auth/2FA/logout, projekt i zadanie, viewer read-only oraz konflikt concurrency. Pozostają wybrane ścieżki odzyskiwania konta i domknięcie macierzy zdarzeń. |

**Postęp V4: 78%**.

Procent jest średnią głównych obszarów V4 i opisuje gotowość do dalszej realizacji, a nie kompletność obecnych ekranów.

## Stan wyjściowy

Projekt ma już projekty, zadania, członkostwo, zaproszenia, komentarze, załączniki,
aktywność, audyt bezpieczeństwa, autoryzowany search workspace i browser E2E.
Pozostały przede wszystkim luki kontraktowe i dowodowe:

- frontend obsługuje mniej typów Notifications niż backend;
- macierz uprawnień nie jest jeszcze jednym jawnym artefaktem projektowym;
- nie wszystkie krytyczne zdarzenia mają browser E2E;
- production storage, backup, restore i alerty wymagają potwierdzenia runtime w V5.

## Zakres implementacyjny

### 1. Account security audit

Utrzymać i domknąć istniejący audyt zdarzeń bezpieczeństwa, obejmujący między innymi:

- login sukces/porażka;
- logout i revocation;
- zmiana oraz reset hasła;
- włączenie/wyłączenie 2FA i TOTP;
- użycie recovery code;
- zmiana roli lub statusu konta;
- wykrycie replay refresh tokenu;
- zmiany krytycznej konfiguracji administracyjnej.

Audyt powinien przechowywać minimalny zestaw danych: aktora, typ zdarzenia, czas, correlation ID, wynik i bezpieczne metadane. Nie wolno zapisywać haseł, surowych tokenów, kodów ani pełnych sekretów.

### 2. Auth lockout UX

Zweryfikować istniejący backendowy lockout i frontendowe komunikaty:

- neutralne przy błędnych danych;
- jasne przy czasowym zablokowaniu konta;
- bez ujawniania, czy email istnieje w publicznych flow;
- z możliwością ponowienia po czasie.

### 3. Global workspace search

Istniejący autoryzowany `SearchWorkspace` jest bazą. Dalsze rozszerzenie może objąć:

- projekty;
- zadania;
- członkowie dostępnego workspace;
- zaproszenia lub powiadomienia.

Search musi nadal respektować te same uprawnienia co normalne endpointy. Nie można
pobierać wszystkich danych i filtrować ich dopiero w frontendzie.

Przed full-text search albo kolejnymi typami wyników należy potwierdzić realną potrzebę,
plan zapytania i macierz uprawnień.

### 4. Załączniki jako funkcja produkcyjna

Szczegółowa kolejność wdrożenia i decyzje graniczne znajdują się w
`12_ATTACHMENT_HARDENING_PLAN.md`.

Backend i frontendowa obsługa, local/S3 storage, quota, walidacja zawartości, cleanup,
reconciliation oraz opcjonalny ClamAV już istnieją. Do zamknięcia pozostają:

- potwierdzenie konfiguracji S3/MinIO i ClamAV na docelowym środowisku;
- restore drill obejmujący metadane, obiekty i Data Protection keys;
- potwierdzenie retencji oraz alertów;
- regresyjne testy operacyjne po zmianie storage albo limitów.

### 5. Kompletność activity i notifications

- utrzymać jedną mapę ważnych zdarzeń produktu;
- wyrównać backendowe i frontendowe typy powiadomień;
- rozdzielić aktywność produktu od audytu bezpieczeństwa;
- zapewnić spójne linki do zasobów w powiadomieniach;
- obsłużyć błędy i ponowienia bez duplikowania zdarzeń.

### 6. Browser E2E

Utrzymać istniejące browser-level E2E i domknąć brakujące krytyczne workflowy:

- reset hasła i odzyskiwanie konta;
- typ Notifications wcześniej nieobsługiwany przez frontend;
- komentarz i załącznik w krytycznej ścieżce;
- utrata uprawnienia po zmianie roli;
- zachowanie po reconnect dla przyszłego transportu real-time.

## Zasada architektoniczna dla nowych funkcji

V4 wykorzystuje standard sprawdzony w pilocie V3, ale nie jest etapem masowej migracji
starego kodu. Nowy większy przypadek użycia powinien być implementowany jako vertical
slice, gdy należy do modułu o potwierdzonej granicy.

Dobrymi kandydatami są między innymi `SearchWorkspace`,
`RecordAccountSecurityEvent` oraz nowe operacje produkcyjnej obsługi załączników.
Każdy taki slice powinien obejmować potrzebne kontrakty, walidację, autoryzację,
obsługę błędów i testy. Elementy niedotyczące danego przypadku użycia, na przykład
migracja albo worker, nie są dodawane sztucznie wyłącznie dla zachowania identycznej
struktury katalogów.

Frontend może być porządkowany feature-first przy okazji wdrażania tych przepływów.
Nie należy wymuszać identycznej struktury backendu i frontendu ani rozpoczynać
mikrofrontendów.

## Test plan

- API integration tests dla nowych endpointów i uprawnień;
- testy kontraktów response/status code;
- testy frontendowe stanów loading/error/empty;
- browser E2E przeciwko Docker Compose;
- testy bezpieczeństwa załączników;
- testy retencji i audytu bez danych wrażliwych.

## Definition of Done

- wszystkie główne workflowy użytkownika mają obsługę błędu i stanu pustego;
- global search nie omija autoryzacji;
- audyt bezpieczeństwa jest odrębny od activity produktu;
- załączniki mają określoną politykę storage, limitów, retencji i walidacji;
- backendowy i frontendowy katalog Notifications mają jawny kontrakt oraz zachowanie
  dla nieznanego typu;
- główne przepływy przechodzą przez browser E2E;
- frontend pozostaje cienką warstwą prezentacji i nie zawiera reguł bezpieczeństwa;
- nowe większe przypadki użycia w potwierdzonych modułach spełniają modułową checklistę slice'a;
- frontendowe typy, klient API oraz stany loading/error/empty są aktualizowane razem z publicznym kontraktem slice'a;
- dokumentacja opisuje ograniczenia prywatności i retencji danych.

## Poza zakresem V4

- rozbudowany design system;
- mikrofrontend;
- masowa migracja istniejących funkcji wyłącznie dla zmiany struktury katalogów;
- wyszukiwarka oparta o zewnętrzny silnik bez zmierzonej potrzeby;
- dane medyczne lub inne wrażliwe dane domenowe tylko po to, aby projekt przypominał ClinicBook.

## Pytania kontrolne

- Dlaczego audyt bezpieczeństwa nie powinien być tym samym co activity projektu?
- Jak zagwarantować, że search nie pokaże zadań z niedostępnego projektu?
- Co dzieje się z plikiem, gdy zapis metadanych w bazie się nie powiedzie?
- Jak przetestować przepływ email confirmation bez prawdziwego providera?
- Które testy muszą być browser E2E, a które wystarczą jako API integration tests?
