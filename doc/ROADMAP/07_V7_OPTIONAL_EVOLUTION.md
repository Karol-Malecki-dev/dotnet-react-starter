# V7: Opcjonalna ewolucja

## Cel

V7 opisuje kierunki rozwoju produktu i infrastruktury, które mogą być wartościowe, ale nie powinny być obowiązkową częścią startera. Ich kolejność ma wynikać z realnej potrzeby produktu, użytkowników, infrastruktury lub ograniczeń zespołu. Platformizacja sprawdzonych modułów, generatory i dystrybucja między projektami należą do osobnego V8.

## Status realizacji

Stan na: **2026-08-29**.

| Obszar | Postęp | Status |
|---|---:|---|
| Tożsamość | 0% | Brak kierunku V7 wymagającego obecnie implementacji. |
| Model produktu | 0% | Brak potwierdzonej potrzeby multi-tenancy, API keys lub wersjonowania publicznego API. |
| Architektura | 0% | Brak zmierzonego problemu uzasadniającego dalsze wyodrębnianie modułów lub usług. |
| Operacje | 0% | Multi-region i disaster recovery pozostają opcjonalnymi kierunkami przyszłości. |

**Postęp V7: 0%**.

To celowy status: V7 rozpoczyna się dopiero po pojawieniu się konkretnego problemu i zaakceptowaniu ADR-u.

## Możliwe kierunki

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

## Definition of Done

Opcjonalny kierunek jest ukończony, gdy:

- decyzja i alternatywy są zapisane w ADR;
- istnieje działający przypadek użycia;
- testy obejmują awarie i rollback;
- monitoring pokazuje koszt i efekt rozwiązania;
- dokumentacja mówi, kiedy rozwiązanie należy usunąć lub zastąpić;
- autor potrafi obronić, dlaczego prostszy wariant nie wystarczał.
