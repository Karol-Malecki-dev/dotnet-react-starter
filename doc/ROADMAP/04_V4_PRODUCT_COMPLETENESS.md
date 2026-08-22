# V4: Kompletność produktu

## Cel

V4 domyka funkcje, które są ważne dla użytecznego produktu, ale nie powinny wyprzedzać stabilizacji backendu. Ten etap łączy brakujące workflowy użytkownika, bezpieczeństwo danych i pełniejsze testy przez frontend.

Frontend pozostaje funkcjonalny i podporządkowany backendowi. Nie celem jest budowa osobnego portfolio frontendowego.

## Stan wyjściowy

Projekt ma już projekty, zadania, członkostwo, zaproszenia, komentarze, załączniki, aktywność, dashboard i podstawowy quick search. Część funkcji istnieje tylko jako fundament:

- quick search wyszukuje strony i akcje, nie dane workspace;
- załączniki mają lokalny storage i podstawową walidację, ale nie pełne zabezpieczenia produkcyjne;
- aktywność nie jest jeszcze pełnym audytem bezpieczeństwa;
- testy frontendowe nie zastępują browser E2E przez realny stack.

## Zakres implementacyjny

### 1. Account security audit

Dodać audyt zdarzeń bezpieczeństwa, między innymi:

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

Po wdrożeniu backendowego lockoutu dodać frontendowe komunikaty:

- neutralne przy błędnych danych;
- jasne przy czasowym zablokowaniu konta;
- bez ujawniania, czy email istnieje w publicznych flow;
- z możliwością ponowienia po czasie.

### 3. Global workspace search

Rozszerzyć quick search o dane, jeśli produkt tego potrzebuje:

- projekty;
- zadania;
- członkowie dostępnego workspace;
- zaproszenia lub powiadomienia.

Search musi respektować te same uprawnienia co normalne endpointy. Nie można pobierać wszystkich danych i filtrować ich dopiero w frontendzie.

Zakres powinien rozpocząć się od jednego endpointu z paginacją i określonym typem wyników. Dopiero później można dodać full-text search lub wyszukiwanie wielomodułowe.

### 4. Załączniki jako funkcja produkcyjna

Backend i frontendowa obsługa już istnieją. Do produkcyjnego poziomu brakuje między innymi:

- trwałego i odpowiedniego storage;
- limitów per użytkownik, projekt i zadanie;
- bezpiecznej nazwy oraz content type;
- content sniffing zamiast zaufania do rozszerzenia;
- kontroli malware lub quarantine, jeśli środowisko tego wymaga;
- cleanupu pliku po nieudanym zapisie metadanych;
- cleanupu metadanych po nieudanym usunięciu pliku;
- polityki retencji i usuwania;
- testów path traversal, dużych plików i niedozwolonych typów.

### 5. Kompletność activity i notifications

- zidentyfikować wszystkie ważne zdarzenia domenowe;
- dodać brakujące wpisy dla usunięcia, zmian roli i statusu;
- rozdzielić aktywność produktu od audytu bezpieczeństwa;
- zapewnić spójne linki do zasobów w powiadomieniach;
- obsłużyć błędy i ponowienia bez duplikowania zdarzeń.

### 6. Browser E2E

Dodać browser-level E2E dla najważniejszych workflowów:

- rejestracja i potwierdzenie emaila;
- login bez i z 2FA;
- reset hasła;
- utworzenie projektu;
- zaproszenie i akceptacja członka;
- utworzenie, edycja i zmiana statusu zadania;
- komentarz i załącznik;
- brak dostępu do zasobu innego użytkownika;
- konflikt concurrency.

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
- główne przepływy przechodzą przez browser E2E;
- frontend pozostaje cienką warstwą prezentacji i nie zawiera reguł bezpieczeństwa;
- dokumentacja opisuje ograniczenia prywatności i retencji danych.

## Poza zakresem V4

- rozbudowany design system;
- mikrofrontend;
- wyszukiwarka oparta o zewnętrzny silnik bez zmierzonej potrzeby;
- dane medyczne lub inne wrażliwe dane domenowe tylko po to, aby projekt przypominał ClinicBook.

## Pytania kontrolne

- Dlaczego audyt bezpieczeństwa nie powinien być tym samym co activity projektu?
- Jak zagwarantować, że search nie pokaże zadań z niedostępnego projektu?
- Co dzieje się z plikiem, gdy zapis metadanych w bazie się nie powiedzie?
- Jak przetestować przepływ email confirmation bez prawdziwego providera?
- Które testy muszą być browser E2E, a które wystarczą jako API integration tests?
