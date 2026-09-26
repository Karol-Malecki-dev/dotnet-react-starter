# V7: Kontrolowana i opcjonalna ewolucja

## Cel

V7 opisuje kontrolowaną ewolucję produktu i architektury po potwierdzeniu fundamentów
VSA. Większość kierunków pozostaje opcjonalna i zależna od realnej potrzeby.
Inkrementalna adopcja MediatR jest zaakceptowanym kierunkiem edukacyjnym i
architektonicznym; nie oznacza jednak zgody na masowy rewrite. Platformizacja
sprawdzonych modułów, generatory i dystrybucja między projektami należą do osobnego V8.

## Status realizacji

Stan na: **2026-09-25**.

| Obszar | Postęp | Status |
|---|---:|---|
| Dispatch VSA / MediatR | 100% | Pilot, kanoniczny standard nowych slice'ów oraz migracje Notifications, Projects i ProjectTasks są zaimplementowane i objęte guardrails. |
| Tożsamość | 0% | Brak kierunku V7 wymagającego obecnie implementacji. |
| Model produktu | 0% | Brak potwierdzonej potrzeby multi-tenancy, API keys lub wersjonowania publicznego API. |
| Architektura rozproszona | 0% | Brak zmierzonego problemu uzasadniającego wyodrębnianie usług. |
| Operacje | 0% | Multi-region i disaster recovery pozostają opcjonalnymi kierunkami przyszłości. |

**Postęp implementacji V7: 100% w zaakceptowanym torze MediatR**.

Wszystkie sześć checkpointów MediatR ma kod, testy, guardrails DI i dokumentację.
Pozostałe kierunki V7 nadal wymagają konkretnego problemu i zaakceptowanego ADR-u;
nie są ukrytym zakresem ukończonej migracji.

## Możliwe kierunki

### MediatR dla modularnego VSA - kierunek zaakceptowany

MediatR zostanie wdrożony jako in-process dispatcher dla command/query slices.
Pierwszy etap obejmuje:

- weryfikację wersji, licencji i zależności pakietu;
- query `Projects/GetProjectDetails`;
- command `ProjectTasks/CreateProjectTask`;
- użycie `ISender` w adapterach HTTP;
- jeden bezpieczny telemetry pipeline behavior;
- guardrails dokładnie jednego handlera, DI, cancellation i braku zależności
  `Domain -> MediatR`.

Po przejściu checkpointu pilota nowe slice'y używają MediatR domyślnie. Istniejące
moduły `Notifications`, `Projects` i `ProjectTasks` zostały zmigrowane bez zmiany
kontraktów publicznych; `Identity` pozostaje poza zakresem i migruje się tylko przy
realnej zmianie konkretnego use case'a.

MediatR nie przejmuje:

- reguł i niezmienników domenowych;
- resource authorization;
- transakcji i finalnego `SaveChangesAsync`;
- optimistic concurrency;
- trwałych notifications i email outbox;
- integration events między przyszłymi usługami.

Pełna decyzja:
[`14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md`](14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md).

### Tożsamość

- passkeys/WebAuthn;
- OIDC i SSO;
- federacja z zewnętrznym dostawcą tożsamości;
- bardziej rozbudowane policy-based authorization;
- zarządzanie sesjami i urządzeniami.

### Model produktu

- workspace jako jawny bounded context;
- multi-tenancy;
- rozbudowane uprawnienia per workspace/projekt;
- API keys i integracje;
- wersjonowanie publicznego API.

### Architektura

- dalsze wzmacnianie granic modułów, jeśli obecne zależności utrudniają rozwój;
- inkrementalna migracja command/query dispatch do MediatR zgodnie z zaakceptowanym ADR;
- osobne read model tylko dla mierzonego problemu;
- komunikacja asynchroniczna między modułami;
- osobna usługa dopiero po wykazaniu potrzeby niezależnego skalowania, wdrażania lub izolacji awarii.

V7 nie oznacza automatycznego tworzenia osobnych projektów `.csproj`, baz danych albo
pakietów dla każdego modułu. Reużywalność, scaffolding, wersjonowanie i instalowanie
modułów w nowych projektach są oceniane w V8 dopiero po potwierdzeniu kilku modułów
w produkcyjnym kształcie.

### Operacje

- multi-region;
- disaster recovery o określonym RTO/RPO;
- migracje bez downtime;
- zaawansowane alertowanie i capacity planning;
- formalna ocena bezpieczeństwa lub compliance.

## Kryteria rozpoczęcia

Przed dodaniem kierunku należy zapisać:

- jaki konkretny problem rozwiązuje;
- jakie są dowody, że obecny model go nie rozwiązuje;
- jakie są koszty utrzymania;
- jak będzie testowany;
- jak wygląda rollback;
- czy problem nie może być rozwiązany prostszą zmianą;
- czy jest to potrzeba produktu, czy tylko ciekawość technologiczna;
- czy kierunek należy do ewolucji działającej aplikacji V7, czy do platformizacji startera V8.

Dla MediatR te warunki zostały rozstrzygnięte przez jawny cel edukacyjny, istniejące
powtarzalne handlery w trzech modułach oraz
[`ADR adopcji`](14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md). Nadal obowiązuje pomiar
kosztu, test kompatybilności i możliwość rollbacku bez zmiany danych.

## Czego nie robić automatycznie

Nie należy dodawać:

- mikroserwisów;
- Kafki;
- Kubernetes;
- Event Sourcing;
- CQRS z osobną bazą;
- distributed locków;
- multi-region;

tylko dlatego, że są kojarzone z poziomem senior lub enterprise.

Nie należy również używać MediatR do ukrywania granic modułów, zastępowania brokera
wiadomości ani automatycznego przenoszenia autoryzacji i transakcji do globalnych
pipeline behaviors.

## Definition of Done

Kierunek V7 jest ukończony, gdy:

- decyzja i alternatywy są zapisane w ADR;
- istnieje działający przypadek użycia;
- testy obejmują awarie i rollback;
- monitoring pokazuje koszt i efekt rozwiązania;
- dokumentacja mówi, kiedy rozwiązanie należy usunąć lub zastąpić;
- autor potrafi obronić, dlaczego prostszy wariant nie wystarczał.

Dla adopcji MediatR dodatkowo:

- query i command pilota zachowują publiczne kontrakty;
- bezpieczny telemetry behavior ma testy;
- każdy request ma dokładnie jeden handler;
- nowe slice'y używają zaakceptowanego standardu;
- migracja modułów odbyła się bez dwóch aktywnych dispatch paths dla jednego slice'a;
- `Notifications`, `Projects` i `ProjectTasks` mają zakończoną migrację, a release gate
  opisuje wymagane potwierdzenie CI i środowiska Docker.
