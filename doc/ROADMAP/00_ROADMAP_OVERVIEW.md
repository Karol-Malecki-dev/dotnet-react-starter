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
| V3 | Domena, granice modułów, transakcje i współbieżność. |
| V4 | Kompletność produktu i pełniejsze przepływy użytkownika. |
| V5 | Deployment, operacje i utrzymanie środowiska. |
| V6 | Pomiar wydajności, niezawodność i zachowanie pod obciążeniem. |
| V7 | Opcjonalna ewolucja zależna od rzeczywistych potrzeb produktu. |

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

## Priorytety

1. Backend i poprawność zachowania systemu.
2. Testy, które dokumentują reguły oraz przypadki awarii.
3. Dokumentacja techniczna i decyzje architektoniczne.
4. Funkcjonalny frontend potrzebny do sprawdzania przepływów.
5. Technologie dodatkowe tylko wtedy, gdy rozwiązują konkretny problem.

Nie dodajemy Redis, kolejki, mikroserwisów, Kafki ani Kubernetes wyłącznie dla CV. Najpierw trzeba rozumieć ograniczenie, zmierzyć problem i uzasadnić koszt rozwiązania.

## Kolejność etapów

### V1: Aktualny baseline

Opisuje to, co już działa, oraz ograniczenia, które są świadomie przeniesione do kolejnych etapów.

Dokument: [01_V1_JUNIOR_BASELINE.md](01_V1_JUNIOR_BASELINE.md)

### V2: Stabilizacja i bezpieczeństwo

Najbliższy etap. Obejmuje politykę sesji, refresh tokeny, rate limiting, lockout, konfigurację wdrożeniową, kontrakty błędów, cancellation i testy przypadków awarii.

Dokument: [02_V2_STABILIZATION_AND_SECURITY.md](02_V2_STABILIZATION_AND_SECURITY.md)

### V3: Domena, transakcje i concurrency

Odpowiada za dojrzalsze granice domeny, agregaty, transakcje, optimistic concurrency, konflikty `409` i bezpieczne operacje wielozapisowe.

Dokument: [03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md](03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md)

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
