# V7: Opcjonalna ewolucja

## Cel

V7 opisuje kierunki, które mogą być wartościowe, ale nie powinny być obowiązkową częścią startera. Ich kolejność ma wynikać z realnej potrzeby produktu, użytkowników, infrastruktury lub ograniczeń zespołu.

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

- dalsze wyodrębnienie modułów;
- osobne read model tylko dla mierzonego problemu;
- komunikacja asynchroniczna między modułami;
- osobna usługa dopiero po wykazaniu potrzeby niezależnego skalowania, wdrażania lub izolacji awarii.

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
- czy jest to potrzeba produktu, czy tylko ciekawość technologiczna.

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
