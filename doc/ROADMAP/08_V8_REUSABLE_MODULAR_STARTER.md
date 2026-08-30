# V8: Reusable Modular Starter / Platformization

## Cel

V8 przekształca sprawdzone wzorce z V3-V7 w powtarzalny starter, który pomaga tworzyć
nowe projekty i kompletne funkcje bez polegania wyłącznie na pamięci autora.

Nie jest to etap przepisywania aplikacji ani dzielenia jej na mikroserwisy. Nadal
preferowany jest modularny monolit, jedna aplikacja i jedna baza PostgreSQL. Celem jest
standaryzacja, automatyzacja i bezpieczne ponowne użycie modułów, których granice zostały
wcześniej potwierdzone w realnych przypadkach użycia.

## Status realizacji

Stan na: **2026-08-31**.

| Obszar | Postęp | Status |
|---|---:|---|
| Standard modułu i slice'a | 35% | Istnieją ADR, checklista oraz backendowy pilot `ProjectTasks` obejmujący kilka rodzajów slice'ów; standard nie został jeszcze potwierdzony na kilku modułach. |
| Guardrails architektoniczne | 0% | Brak automatycznych testów granic, kompletności rejestracji i kontraktów modułu. |
| Scaffolding | 0% | Brak generatora modułu albo slice'a. |
| Wybór i instalacja modułów | 0% | Brak stabilnego mechanizmu tworzenia projektu z wybranym zestawem capability. |
| Wersjonowanie i aktualizacje | 0% | Brak potwierdzonej strategii aktualizowania modułów w wielu projektach. |
| Moduły referencyjne | 15% | Backendowy `ProjectTasks` pilot ma większość przypadków użycia w slice'ach, ale nie jest jeszcze ukończonym produktem dystrybucyjnym ani drugim modułem referencyjnym. |

**Postęp V8: 0%**.

Istniejące fundamenty nie uruchamiają jeszcze etapu. V8 rozpoczyna się dopiero po
spełnieniu kryteriów wejścia.

## Kryteria wejścia

Przed rozpoczęciem V8 wymagane są:

- co najmniej dwa lub trzy moduły biznesowe z kilkoma command i query slices;
- stabilne nazewnictwo kontraktów, handlerów, endpointów, portów i rejestracji;
- potwierdzone testami granice zależności między modułami;
- udokumentowany sposób współdzielenia jednej bazy i migracji;
- zmierzony czas ręcznego tworzenia kolejnych slice'ów;
- przynajmniej jeden przypadek użycia startera albo modułu w drugim projekcie;
- lista elementów rzeczywiście powtarzalnych, a nie tylko przewidywanych.

## Zakres implementacyjny

### 1. Kanoniczny standard modułu

Każdy moduł referencyjny powinien opisywać:

- odpowiedzialność biznesową i należące do niego agregaty;
- publiczne kontrakty oraz dozwolone zależności;
- command i query slices;
- kontrakty HTTP i modele application;
- walidację, autoryzację i mapowanie błędów;
- persistence, konfiguracje EF Core i własność migracji;
- rejestrację DI, endpointów, opcji, workerów i health checks;
- testy backendu i odpowiadające elementy frontendu;
- zasady włączenia, wyłączenia i usunięcia modułu.

Standard ma rozróżniać elementy obowiązkowe, warunkowe i niedotyczące danego
przypadku użycia. Nie każdy slice potrzebuje migracji, workera, eventu domenowego ani
osobnego ekranu.

### 2. Automatyczne guardrails

Wprowadzić możliwie proste kontrole:

- testy architektury blokujące niedozwolone zależności między modułami;
- test composition root potwierdzający możliwość zbudowania kontenera DI;
- test mapowania endpointów włączonych modułów;
- kontrolę konfiguracji opcji podczas startu;
- testy kontraktów OpenAPI i zgodności typów frontendu;
- kontrolowaną migrację pustej bazy oraz upgrade z poprzedniej wersji;
- konwencje CI wykrywające brak testów albo rejestracji tam, gdzie można to ustalić jednoznacznie.

Automatyzacja nie powinna udawać, że potrafi ocenić sens biznesowy walidatora albo
jakość testu wyłącznie na podstawie nazwy pliku. Takie elementy nadal wymagają
checklisty i code review.

### 3. Scaffolding vertical slice

Najpierw przygotować mały generator pojedynczego slice'a. Powinien tworzyć tylko
sprawdzony szkielet, na przykład:

- command albo query;
- handler contract i implementację;
- request/response;
- validator, jeśli występuje input zewnętrzny;
- endpoint;
- miejsca na test jednostkowy i integracyjny;
- wpis lub jednoznaczny punkt rejestracji.

Generator nie powinien tworzyć pustych repository, eventów, workerów ani migracji,
jeśli dany przypadek ich nie potrzebuje.

### 4. Tworzenie nowego projektu

Po ustabilizowaniu scaffolding należy porównać:

- repository template jako najprostszy pełny baseline;
- `dotnet new` dla przewidywalnego wyboru wariantów;
- skrypt PowerShell jako cienką orkiestrację lokalną;
- kopiowanie źródeł dla modułów, które użytkownik ma dalej modyfikować;
- pakiety NuGet tylko dla stabilnych building blocks albo modułów z kontrolowanym API.

Wybór modułów podczas tworzenia projektu jest innym problemem niż runtime feature
flags. Template lub generator określa zawartość nowego rozwiązania, natomiast runtime
configuration steruje zachowaniem już zbudowanej aplikacji.

### 5. Wersjonowanie i aktualizacje

Dla każdego sposobu dystrybucji określić:

- właściciela modułu i jego publiczny kontrakt;
- politykę kompatybilności;
- sposób aktualizacji schematu bazy;
- migrację konfiguracji;
- aktualizację kontraktów TypeScript;
- procedurę rollbacku;
- sposób przenoszenia poprawek do projektów, które zmodyfikowały kod modułu.

Source copy daje największą swobodę, ale utrudnia późniejsze aktualizacje. NuGet
ułatwia aktualizacje tylko wtedy, gdy kontrakt modułu jest stabilny i konsument nie
potrzebuje zmieniać jego wnętrza.

### 6. Moduły referencyjne

Przygotować ograniczony zestaw modułów pokazujących różne rodzaje problemów:

- Identity/Accounts jako capability bezpieczeństwa i sesji;
- Projects jako moduł domenowy z agregatem i członkostwem;
- ProjectTasks jako moduł z command/query slices, concurrency i workerem;
- Notifications jako moduł przekrojowy z outboxem i preferencjami użytkownika.

Nie każdy obszar musi być opcjonalny. Identity lub wspólny HTTP pipeline mogą być
częścią obowiązkowego rdzenia startera.

## Test plan

- utworzenie czystego projektu z minimalnym wspieranym zestawem modułów;
- utworzenie projektu z pełnym zestawem modułów;
- build i testy wygenerowanego rozwiązania bez ręcznych poprawek;
- dodanie command i query slice'a przez generator;
- uruchomienie migracji na pustej bazie i upgrade poprzedniego schematu;
- wyłączenie opcjonalnego endpointu lub workera bez naruszenia pozostałych modułów;
- aktualizacja jednego modułu w co najmniej dwóch projektach testowych;
- wykrycie przykładowej niedozwolonej zależności przez test architektury;
- porównanie czasu ręcznego i generowanego workflowu.

## Definition of Done

- istnieją co najmniej dwa sprawdzone warianty wygenerowanego projektu;
- nowy slice można utworzyć z kanonicznego szablonu bez pomijania podstawowych elementów;
- guardrails wykrywają niedozwolone zależności i błędy rejestracji;
- moduły mają udokumentowane zależności, konfigurację, dane, endpointy, workery i testy;
- strategia runtime enablement jest oddzielona od wyboru kodu podczas generowania projektu;
- migracje wspólnej bazy są deterministyczne i testowane;
- wybrana strategia aktualizacji została sprawdzona na więcej niż jednym projekcie;
- dokumentacja jasno wskazuje, które elementy są obowiązkowym rdzeniem, a które opcjonalnymi modułami;
- zmierzony czas potwierdza, że automatyzacja daje wartość większą niż jej koszt utrzymania.

## Poza zakresem V8

- mikroserwis per moduł;
- osobna baza per moduł bez wymagania izolacji;
- publikowanie każdego modułu jako NuGet;
- marketplace modułów;
- generator próbujący modelować dowolną domenę biznesową;
- runtime instalowanie i usuwanie schematu bazy;
- własny framework zastępujący ASP.NET Core, EF Core albo React;
- utrzymywanie wielu wariantów startera bez automatycznych testów każdego z nich.

## Pytania kontrolne

- Które elementy powtórzyły się w co najmniej kilku rzeczywistych slice'ach?
- Czy moduł jest rozszerzany przez konsumenta, czy powinien być aktualizowany jak pakiet?
- Co stanie się z danymi po wyłączeniu modułu?
- Czy wygenerowany projekt buduje się i przechodzi testy bez ręcznych poprawek?
- Jak aktualizacja modułu zmieni migracje i kontrakty frontendu?
- Czy generator usuwa realną pracę, czy tylko produkuje więcej pustych plików?
- Które zależności mogą być sprawdzone automatycznie, a które wymagają review?
