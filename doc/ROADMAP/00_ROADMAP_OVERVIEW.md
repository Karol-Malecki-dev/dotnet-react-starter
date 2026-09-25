# Roadmap rozwoju startera

## Cel dokumentu

Ten dokument jest mapą rozwoju repozytorium `dotnet-react-starter`. Starter ma być długoterminową bazą edukacyjną dla aplikacji opartych przede wszystkim o:

- C# i .NET;
- ASP.NET Core Web API;
- EF Core i PostgreSQL;
- bezpieczeństwo aplikacji webowych;
- projektowanie domeny;
- testowanie i niezawodność systemów;
- wdrażanie aplikacji backendowych.

Frontend React + TypeScript pozostaje funkcjonalną warstwą demonstracyjną. Ma działać poprawnie i pokazywać pełne przepływy użytkownika, ale głównym celem nauki jest backend.

## Jak czytać wersje

Wersje `V1`, `V2` itd. nie są terminami kalendarzowymi ani obowiązkowymi release'ami. Oznaczają kolejne poziomy dojrzałości technicznej:

| Etap | Znaczenie |
|---|---|
| V1 | Aktualny szybki projekt juniorowy, który pokazuje szerokie fundamenty. |
| V2 | Stabilizacja, bezpieczeństwo i spójność istniejących mechanizmów. |
| V3 | Domena, granice modułów, pilotaż Vertical Slice Architecture, transakcje i współbieżność. |
| V4 | Kompletność produktu i pełniejsze przepływy użytkownika. |
| V5 | Deployment, operacje i utrzymanie środowiska. |
| V6 | Pomiar wydajności, niezawodność i zachowanie pod obciążeniem. |
| V7 | Kontrolowana ewolucja architektury; MediatR jest zaplanowany, pozostałe kierunki zależą od potrzeb produktu. |
| V8 | Platformizacja sprawdzonych modułów i przygotowanie startera do wielokrotnego użycia. |

Etap można uznać za ukończony dopiero wtedy, gdy istnieją kod, testy, dokumentacja i możliwość wyjaśnienia najważniejszych kompromisów.

## Aktualna ocena

Starter jest obecnie **mocną bazą juniorową z wieloma elementami junior+**. Zawiera więcej niż klasyczny CRUD:

- modularny monolit z warstwami API, Application, Domain, Infrastructure i Shared;
- JWT, refresh-token rotation, HttpOnly cookie, email confirmation, 2FA, TOTP i recovery codes;
- PostgreSQL, EF Core, migracje, indeksy i konfiguracje relacji;
- projekty, zadania, członkostwo, zaproszenia, komentarze, załączniki, aktywność i powiadomienia;
- paginację i filtrowanie zadań;
- logowanie strukturalne, correlation ID, health checks i workery;
- testy jednostkowe, integracyjne, PostgreSQL Testcontainers i smoke tests;
- Docker Compose oraz CI.

Najważniejsze braki nie polegają obecnie na braku kolejnych endpointów. Dotyczą:

- jednego prostego golden path dla kompletnego command/query slice'a;
- spójności kontraktów Notifications między backendem i frontendem;
- migracji kolejnych modułów do zaakceptowanego standardu MediatR bez utraty granic modułów;
- jawnej, testowalnej macierzy uprawnień przed dodaniem kolejnych workflowów;
- zachowania klienta po reconnect, retry i konflikcie;
- pomiarów wydajności oraz kosztu ręcznego tworzenia slice'ów;
- runtime evidence dla stagingu, backupu, restore, rollbacku i alertów.

## Status realizacji roadmapy

Stan na: **2026-09-25**.

Procent opisuje realizację głównych obszarów danego etapu, a nie liczbę linii kodu. `100%` oznacza spełniony obszar wraz z testem, dokumentacją albo zaakceptowaną decyzją. `50%` oznacza istniejący fundament bez pełnego Definition of Done, a `0%` oznacza brak rozpoczętej realizacji. Postęp bazowej roadmapy jest średnią arytmetyczną etapów V1-V7 i nie jest miarą gotowości produkcyjnej. V8 jest późniejszym etapem platformizacji i nie jest wliczany do postępu bazowej aplikacji.

| Etap | Postęp | Status | Najważniejszy dowód lub brak |
|---|---:|---|---|
| V1 | 100% | Ukończony | Fundament aplikacji, testy i lokalny workflow są dostępne. |
| V2 | 96% | Ukończony dla bieżącego zakresu | Hardening auth, API, async i konfiguracji jest zwalidowany; pozostały drobne follow-upy porządkowe. |
| V3 | 65% | W toku; pilot VSA ukończony | `Projects`, `ProjectTasks` i `Notifications` potwierdzają backendowy standard slice'a, jawne porty, modułowe DI i guardrails. Pozostałe prace V3 dotyczą granic domenowych, starszych modeli i domknięcia kontraktów, nie masowej migracji folderów. |
| V4 | 78% | Domykanie kontraktów i dowodów | Account security audit, autoryzowany workspace search, produkcyjny lifecycle załączników oraz bazowa macierz browser E2E są zaimplementowane. Najbliższa luka to pełny kontrakt Notifications po obu stronach API oraz domknięcie pozostałych scenariuszy. |
| V5 | 80% | W toku | Implementacja deploymentu VPS, migracji, szyfrowanego backupu, rollbacku, monitoringu i protected staging smoke jest gotowa; formalny gate czeka na realny staging, off-host backup, restore drill i rollback evidence. |
| V6 | 13% | Planowany | Istnieją podstawy EF, PostgreSQL i workerów; brak baseline'ów, load testów i pomiarów. |
| V7 | 67% | Pilot MediatR, standard nowych slice'ów i migracja Notifications ukończone; migracja modułów w toku | `GetProjectDetails`, `CreateProjectTask`, bezpieczny telemetry behavior oraz wszystkie sześć Notifications slices używają kanonicznego dispatchu; kolejne migracje dotyczą `Projects` i `ProjectTasks`. |
| V8 | 0% | Odroczony; fundamenty częściowo gotowe | Trzy moduły i pierwsze guardrails istnieją, ale generator, wybór modułów i strategia aktualizacji wymagają najpierw pomiaru kolejnych ręcznych slice'ów. |

**Postęp bazowej roadmapy V1-V7: 71%**.

## Aktualna strategia wykonania

Etapy pozostają mapą dojrzałości, ale praca przebiega w trzech torach:

1. **Produkt i VSA:** najpierw prosty golden path i kontrakt Notifications, następnie
   kolejne migracje MediatR, macierz uprawnień i pojedyncze workflowy.
2. **Dowody V5:** staging, off-host backup, restore drill, rollback i alert test są
   zbierane równolegle. Nie blokują lokalnego feature development, lecz blokują
   deklarację production-ready.
3. **V8:** automatyzacja zaczyna się od pomiaru kosztu ręcznego slice'a. Generator i
   template projektu nie są warunkiem obecnych funkcji.

Szczegółowa kolejność znajduje się w
[`../PRODUCT_EVOLUTION/DEVELOPMENT_PLAN.md`](../PRODUCT_EVOLUTION/DEVELOPMENT_PLAN.md).

## Priorytety

1. Backend i poprawność zachowania systemu.
2. Testy, które dokumentują reguły oraz przypadki awarii.
3. Dokumentacja techniczna i decyzje architektoniczne.
4. Funkcjonalny frontend potrzebny do sprawdzania przepływów.
5. Technologie dodatkowe tylko wtedy, gdy rozwiązują konkretny problem.

Nie dodajemy Redis, kolejki, mikroserwisów, Kafki ani Kubernetes wyłącznie dla CV. Najpierw trzeba rozumieć ograniczenie, zmierzyć problem i uzasadnić koszt rozwiązania.

## Kierunek organizacji funkcji

Od V3 preferowanym kierunkiem jest hybrydowy modularny monolit:

- moduł odpowiada za spójny obszar biznesowy;
- vertical slice odpowiada za pojedynczy przypadek użycia, na przykład utworzenie albo odczyt zadania;
- warstwy Domain, Application, Infrastructure i API opisują odpowiedzialności techniczne wewnątrz rozwiązania, ale nie zastępują granic biznesowych;
- nowe większe przypadki użycia korzystają z zaakceptowanego standardu slice'a, gdy granica ich modułu jest już potwierdzona;
- istniejący kod jest migrowany przy okazji realnej zmiany funkcjonalnej albo zaplanowanego pilota, a nie przez jednorazowe przepisywanie repozytorium;
- jedna aplikacja, jeden `ApplicationDbContext` i jedna baza PostgreSQL pozostają domyślnym modelem do czasu pojawienia się mierzalnej potrzeby zmiany.

Vertical Slice Architecture jest sposobem organizacji implementacji. Nie zastępuje modelowania domeny, transakcji, bezpieczeństwa, operacji ani pomiarów wydajności opisanych w kolejnych etapach.

## Kolejność etapów

### V1: Aktualny baseline

Opisuje to, co już działa, oraz ograniczenia, które są świadomie przeniesione do kolejnych etapów.

Dokument: [01_V1_JUNIOR_BASELINE.md](01_V1_JUNIOR_BASELINE.md)

### V2: Stabilizacja i bezpieczeństwo

Etap ukończony dla bieżącego zakresu. Obejmuje politykę sesji, refresh tokeny, rate limiting, lockout, konfigurację wdrożeniową, kontrakty błędów, cancellation i testy przypadków awarii.

Dokument: [02_V2_STABILIZATION_AND_SECURITY.md](02_V2_STABILIZATION_AND_SECURITY.md)

### V3: Domena, granice modułów, vertical slice pilot, transakcje i concurrency

Etap w toku, ale backendowy pilot VSA został ukończony na trzech modułach. Pozostały
zakres dotyczy dojrzalszych granic domeny, starszych modeli, transakcji, optimistic
concurrency i spójności kontraktów, a nie mechanicznego przenoszenia kolejnych plików.

Dokument: [03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md](03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md)

Decyzja dotycząca granicy `ProjectTask`: [10_ADR_PROJECT_TASK_AGGREGATE_BOUNDARY.md](10_ADR_PROJECT_TASK_AGGREGATE_BOUNDARY.md)

Decyzja dotycząca inkrementalnej modularizacji VSA: [11_ADR_INCREMENTAL_MODULAR_VSA.md](11_ADR_INCREMENTAL_MODULAR_VSA.md)

Decyzja dotycząca inkrementalnej adopcji MediatR:
[14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md](14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md)

Standard modułów i slice'ów: [../MODULAR_VSA_MODULE_CHECKLIST.md](../MODULAR_VSA_MODULE_CHECKLIST.md)

### V4: Kompletność produktu

Domyka kontrakty i dowody dla istniejących workflowów użytkownika. Najbliższym
inkrementem jest pełna spójność Notifications; nowe funkcje są dodawane pojedynczymi
slice'ami, nie jako jeden szeroki pakiet V4.

Dokument: [04_V4_PRODUCT_COMPLETENESS.md](04_V4_PRODUCT_COMPLETENESS.md)

### V5: Deployment i operacje

Dodaje wybrane środowisko hostingowe, sekrety, migracje, backup, rollback, monitoring i dokumentację wdrożeniową.

Dokument: [05_V5_DEPLOYMENT_AND_OPERATIONS.md](05_V5_DEPLOYMENT_AND_OPERATIONS.md)

### V6: Wydajność i niezawodność

Wprowadza pomiary, testy obciążeniowe, analizę `EXPLAIN`, idempotencję, rozproszoną koordynację workerów i odporność na retry.

Dokument: [06_V6_PERFORMANCE_AND_RELIABILITY.md](06_V6_PERFORMANCE_AND_RELIABILITY.md)

### V7: Kontrolowana i opcjonalna ewolucja

Obejmuje zaakceptowaną inkrementalną adopcję MediatR jako dispatchera command/query
oraz pozostałe kierunki, które nadal wymagają konkretnej potrzeby produktu lub
infrastruktury. MediatR nie zmienia granic modułów, nie zastępuje outboxa i nie
uzasadnia masowego rewrite'u.

Dokument: [07_V7_OPTIONAL_EVOLUTION.md](07_V7_OPTIONAL_EVOLUTION.md)

### V8: Reusable Modular Starter / Platformization

Rozpoczyna się dopiero po potwierdzeniu standardu na kilku rzeczywistych modułach. Obejmuje automatyczne guardrails, scaffolding, wybór modułów podczas tworzenia projektu oraz strategię ich wersjonowania i aktualizacji. Nie jest wymagany do ukończenia bazowej aplikacji V1-V7.

Dokument: [08_V8_REUSABLE_MODULAR_STARTER.md](08_V8_REUSABLE_MODULAR_STARTER.md)

## Ogólna Definition of Done

Dla każdego większego zadania wymagane są:

- opis problemu i zakresu;
- decyzja architektoniczna, jeśli zmienia się granica lub kontrakt;
- implementacja backendu;
- testy adekwatne do ryzyka;
- aktualizacja dokumentacji;
- walidacja builda i testów;
- opis zachowania przy błędzie i częściowej awarii;
- krótka odpowiedź na siedem pytań z [workflowu nauki](08_LEARNING_WORKFLOW.md).

## Czego nie obiecuje ta roadmapa

Roadmapa nie ma udawać doświadczenia zawodowego. Projekt domowy nie zastępuje:

- utrzymania systemu przez dłuższy czas;
- pracy zespołowej i code review;
- realnych awarii produkcyjnych;
- zmieniających się wymagań biznesowych;
- odpowiedzialności za koszty i SLA.

Jej celem jest stworzenie środowiska, w którym można przećwiczyć techniczne decyzje spotykane w takich systemach i nauczyć się je uzasadniać.
