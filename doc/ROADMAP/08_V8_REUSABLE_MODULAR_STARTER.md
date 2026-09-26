# V8: Reusable Modular Starter / Platformization

## Cel

V8 przekształca sprawdzone wzorce z V3-V7 w powtarzalny starter, który pomaga tworzyć
nowe projekty i kompletne funkcje bez polegania wyłącznie na pamięci autora.

Nie jest to etap przepisywania aplikacji ani dzielenia jej na mikroserwisy. Nadal
preferowany jest modularny monolit, jedna aplikacja i jedna baza PostgreSQL. Celem jest
standaryzacja, automatyzacja i bezpieczne ponowne użycie modułów, których granice zostały
wcześniej potwierdzone w realnych przypadkach użycia.

## Status realizacji

Stan na: **2026-09-26**.

| Obszar | Postęp | Status |
|---|---:|---|
| Standard modułu i slice'a | 100% | ADR, checklista, trzy moduły produkcyjne oraz consumer template potwierdzają kanoniczny przepływ `ISender -> IRequestHandler`. |
| Guardrails architektoniczne | 85% | Template testuje composition root, unikalność handlerów, `ISender`, brak zależności `Domain` od MediatR i publiczny endpoint; pełne guardrails migracji bazy pozostają poza V8.0. |
| Scaffolding | 100% | `New-VsaSlice.ps1` i `New-V8StarterProject.ps1` generują sprawdzony skeleton query oraz dwa warianty projektu. |
| Wybór i instalacja modułów | 75% | Source-template obsługuje warianty `Minimal` i `Full`; dystrybucja NuGet i runtime installer są świadomie poza zakresem. |
| Wersjonowanie i aktualizacje | 75% | Manifest zawiera wersję template'u i politykę `regenerate-and-review`, sprawdzoną na dwóch niezależnych consumerach. |
| Moduły referencyjne | 70% | Template zawiera mały rdzeń `StarterHealth` oraz opcjonalny `Catalog`; produkcyjne `Projects`, `ProjectTasks` i `Notifications` pozostają wzorcami do ręcznej adaptacji. |

**Gotowość V8.0: 100%** dla zaakceptowanego zakresu source-template i scaffolding.
Szersza platformizacja (automatyczne aktualizacje, stabilne pakiety i pełne moduły
capability) pozostaje świadomym backlogiem V8.1+.

Proof V8 zapisuje wynik w `artifacts\v8\generated-run\proof.json` (katalog jest
ignorowany przez Git), a trwałe podsumowanie znajduje się w
[`V8_RELEASE_EVIDENCE.md`](../V8_RELEASE_EVIDENCE.md).
Decyzję o source-template i aktualizacjach zapisuje
[`15_ADR_SOURCE_TEMPLATE_DISTRIBUTION.md`](15_ADR_SOURCE_TEMPLATE_DISTRIBUTION.md).

## Kryteria wejścia

Przed rozpoczęciem V8 wymagane są:

- co najmniej dwa lub trzy moduły biznesowe z kilkoma command i query slices
  (**spełnione dla backendowego pilota**);
- stabilne nazewnictwo kontraktów, handlerów, endpointów, portów i rejestracji
  (**częściowo spełnione**);
- potwierdzone testami granice zależności między modułami
  (**częściowo spełnione**);
- udokumentowany sposób współdzielenia jednej bazy i migracji;
- zmierzony strukturalny koszt ręcznego workflowu oraz czas proofu generatora;
- przynajmniej jeden przypadek użycia startera albo modułu w drugim projekcie
  (spełnione przez niezależne warianty `Minimal` i `Full`);
- lista elementów rzeczywiście powtarzalnych, a nie tylko przewidywanych.

Historyczny wall-clock ręcznego tworzenia slice'a nie był rekonstruowany po fakcie.
Nie jest udawany jako pomiar; zostaje metryką V8.1, jeśli dalsze użycie startera
wykaże taką potrzebę.

## Zakres wydania V8.0

V8.0 jest pierwszym używalnym wydaniem platformizacji, a nie obietnicą
uniwersalnego frameworka. Wydanie obejmuje:

- `templates\v8-consumer\Common` jako kanoniczny source-template;
- wariant `Minimal` z obowiązkowym rdzeniem oraz wariant `Full` z referencyjnym
  modułem `Catalog`;
- `New-V8StarterProject.ps1` z manifestem projektu i ochroną przed przypadkowym
  nadpisaniem;
- `New-VsaSlice.ps1` generujący query albo command, handler, adapter HTTP oraz
  testy;
- `Invoke-V8ScaffoldingProof.ps1`, który generuje oba warianty, dodaje query i
  command slice, uruchamia backend restore/build/unit/integration tests oraz
  frontend build;
- manifesty z wersją template'u, listą modułów i polityką `regenerate-and-review`.

Generator tworzy wyłącznie techniczny skeleton. Handler ma jawnie oznaczony
`NotImplementedException`, a konsument musi samodzielnie ustalić regułę domenową,
autoryzację, porty, transakcję, błędy i ewentualną migrację.

## Most V3/V4: ergonomia przed platformizacją

Przed formalnym V8 wolno i należy:

- utrzymywać jeden manualny golden path w `doc/ADDING_FEATURES.md`;
- wskazywać działający command i query jako wzorce;
- oznaczać elementy warunkowe jako `N/A`, zamiast tworzyć puste pliki;
- mierzyć czas, liczbę plików i ręczne punkty rejestracji;
- upraszczać dokumentację i istniejące konwencje bez zmiany runtime architecture.

Przed formalnym V8 nie należy:

- budować pełnego generatora modułów;
- wprowadzać auto-discovery tylko po to, aby ukryć rejestrację DI;
- publikować modułów jako NuGet;
- obiecywać aktualizacji wielu projektów bez drugiego rzeczywistego konsumenta.

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
- test dokładnie jednego `IRequestHandler` dla każdego requestu MediatR;
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

Po zakończeniu adopcji MediatR generator tworzy `IRequest<TResult>`,
`IRequestHandler<TRequest, TResult>` i adapter używający `ISender`. Nie powinien
utrzymywać dwóch wariantów dispatchingu tylko dla kompatybilności z niezmigrowanymi
slice'ami; generator zaczyna się dopiero po ustabilizowaniu docelowego standardu.

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

- utworzenie czystego projektu z minimalnym wspieranym zestawem;
- utworzenie projektu z pełnym zestawem referencyjnym;
- build i testy backendu obu wariantów bez ręcznych poprawek;
- build frontendu obu wariantów;
- dodanie query i command slice'a przez generator;
- wykrycie błędu rejestracji albo duplikatu handlera przez test architektury;
- potwierdzenie, że output nie kopiuje katalogu źródłowych wariantów ani artefaktów
  `bin`, `obj`, `node_modules` i `dist`;
- sprawdzenie manifestu wersji, modułów i polityki aktualizacji w dwóch consumerach.

Migracje bazy są w V8.0 `N/A`: template nie zawiera schematu ani `DbContext`, więc
nie może udawać, że testuje migrację. Właściciel danych w consumerze pozostaje
odpowiedzialny za migrację i upgrade zgodnie z jego domeną.

## Definition of Done V8.0

- istnieją dwa sprawdzone warianty niezależnego projektu (`Minimal` i `Full`);
- nowy query albo command można utworzyć z kanonicznego generatora;
- wygenerowany skeleton zawiera request, handler, adapter HTTP, test jednostkowy,
  test integracyjny i manifest;
- guardrails wykrywają brak handlera, duplikat handlera, brak rejestracji
  composition root, brak `ISender` w kontrolerze i zależność `Domain` od MediatR;
- runtime enablement jest odróżnione od wyboru kodu podczas generowania projektu;
- source-template, wersja i polityka `regenerate-and-review` są jawne;
- proof obu consumerów przechodzi restore, Release build, testy backendu i
  frontend build bez ręcznej korekty;
- dokumentacja jasno wskazuje, co jest rdzeniem, co wariantem opcjonalnym, a co
  decyzją biznesową konsumenta;
- ograniczenia (brak wall-clock manualnego workflowu, brak pakietów NuGet i brak
  automatycznych migracji) są zapisane jako świadome decyzje, a nie ukryte braki.

## Następny krok po V8.0

V8.1 powinien rozpocząć się dopiero po realnym użyciu template'u w co najmniej
kilku nowych slice'ach. Wtedy warto zmierzyć wall-clock ręcznego workflowu,
sprawdzić czy source-copy nadal wystarcza i dopiero na tej podstawie ocenić
`dotnet new`, pakiety albo kontrolowane aktualizacje modułów. Nie należy dodawać
ich tylko po to, aby sztucznie zwiększyć zakres wydania V8.0.

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
