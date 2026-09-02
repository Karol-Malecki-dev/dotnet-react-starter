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
| V7 | Opcjonalna ewolucja zależna od rzeczywistych potrzeb produktu. |
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

Najważniejsze braki nie polegają obecnie na braku kolejnych endpointów. Dotyczą zachowania systemu przy:

- dezaktywacji konta i zmianie roli;
- równoległym refreshu lub zapisie;
- częściowej awarii operacji wieloetapowej;
- restarcie kontenera i wielu instancjach;
- niepoprawnej konfiguracji proxy, cookie lub kluczy Data Protection;
- dużej liczbie danych;
- niespójnym kontrakcie błędów.

## Status realizacji roadmapy

Stan na: **2026-09-02**.

Procent opisuje realizację głównych obszarów danego etapu, a nie liczbę linii kodu. `100%` oznacza spełniony obszar wraz z testem, dokumentacją albo zaakceptowaną decyzją. `50%` oznacza istniejący fundament bez pełnego Definition of Done, a `0%` oznacza brak rozpoczętej realizacji. Postęp bazowej roadmapy jest średnią arytmetyczną etapów V1-V7 i nie jest miarą gotowości produkcyjnej. V8 jest późniejszym etapem platformizacji i nie jest wliczany do postępu bazowej aplikacji.

| Etap | Postęp | Status | Najważniejszy dowód lub brak |
|---|---:|---|---|
| V1 | 100% | Ukończony | Fundament aplikacji, testy i lokalny workflow są dostępne. |
| V2 | 96% | Ukończony dla bieżącego zakresu | Hardening auth, API, async i konfiguracji jest zwalidowany; pozostały drobne follow-upy porządkowe. |
| V3 | 50% | W toku | `Project` i `ProjectMember` mają wyraźniejszą granicę agregatu, `ProjectTask` został rozstrzygnięty jako osobny agregat, akceptacja zaproszeń ma transakcję i concurrency z testami PostgreSQL, dashboard używa agregacji SQL, zakresów dat i potwierdzonego indeksu PostgreSQL, `User` korzysta z przetestowanych value objectów oraz enkapsulowanych metod domenowych, a backendowy pilot `ProjectTasks` obejmuje CRUD, komentarze, załączniki i worker przypomnień jako osobne vertical slices przy zachowaniu istniejących tabel i kontraktów; rozpoczęto też pierwszy slice modułu `Projects` (`GetProjectDetails`), a pozostały migracja kolejnych przypadków, frontendowe granice, guardrails i benchmarki. |
| V4 | 28% | Fundamenty | Istnieją workflowy, załączniki, activity, quick search i lokalne browser E2E; brak pełnego audytu oraz search workspace, a stagingowa walidacja E2E należy do V5. |
| V5 | 80% | W toku | Implementacja deploymentu VPS, migracji, szyfrowanego backupu, rollbacku, monitoringu i protected staging smoke jest gotowa; formalny gate czeka na realny staging, off-host backup, restore drill i rollback evidence. |
| V6 | 13% | Planowany | Istnieją podstawy EF, PostgreSQL i workerów; brak baseline'ów, load testów i pomiarów. |
| V7 | 0% | Opcjonalny | Brak kierunku wymagającego obecnie implementacji. |
| V8 | 0% | Odroczony | Najpierw kilka modułów i slice'ów musi potwierdzić stabilny standard oraz realny koszt ponownego użycia. |

**Postęp bazowej roadmapy V1-V7: 52%**.

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

Etap w toku. Odpowiada za dojrzalsze granice domeny, agregaty, pilotaż modułu biznesowego zawierającego vertical slices, transakcje, optimistic concurrency, konflikty `409` i bezpieczne operacje wielozapisowe.

Dokument: [03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md](03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md)

Decyzja dotycząca granicy `ProjectTask`: [10_ADR_PROJECT_TASK_AGGREGATE_BOUNDARY.md](10_ADR_PROJECT_TASK_AGGREGATE_BOUNDARY.md)

Decyzja dotycząca inkrementalnej modularizacji VSA: [11_ADR_INCREMENTAL_MODULAR_VSA.md](11_ADR_INCREMENTAL_MODULAR_VSA.md)

Standard modułów i slice'ów: [../MODULAR_VSA_MODULE_CHECKLIST.md](../MODULAR_VSA_MODULE_CHECKLIST.md)

### V4: Kompletność produktu

Domyka brakujące workflowy użytkownika, audyt bezpieczeństwa, wyszukiwanie workspace, załączniki w ujęciu produkcyjnym i browser E2E.

Dokument: [04_V4_PRODUCT_COMPLETENESS.md](04_V4_PRODUCT_COMPLETENESS.md)

### V5: Deployment i operacje

Dodaje wybrane środowisko hostingowe, sekrety, migracje, backup, rollback, monitoring i dokumentację wdrożeniową.

Dokument: [05_V5_DEPLOYMENT_AND_OPERATIONS.md](05_V5_DEPLOYMENT_AND_OPERATIONS.md)

### V6: Wydajność i niezawodność

Wprowadza pomiary, testy obciążeniowe, analizę `EXPLAIN`, idempotencję, rozproszoną koordynację workerów i odporność na retry.

Dokument: [06_V6_PERFORMANCE_AND_RELIABILITY.md](06_V6_PERFORMANCE_AND_RELIABILITY.md)

### V7: Opcjonalna ewolucja

Opisuje technologie i kierunki, które mogą mieć sens dopiero po pojawieniu się konkretnej potrzeby produktu lub infrastruktury.

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
