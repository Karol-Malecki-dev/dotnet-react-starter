# V5: Deployment i operacje

## Cel

V5 ma przeprowadzić aplikację z lokalnego Docker Compose i CI do jednego realnego środowiska staging lub production. Nie chodzi o wybór najbardziej złożonej platformy. Chodzi o umiejętność wdrożenia, obserwowania, odtworzenia i wycofania aplikacji.

## Status realizacji

Stan na: **2026-08-29**.

| Obszar | Postęp | Status i dowód |
|---|---:|---|
| 1. Środowiska i konfiguracja | 40% | Local, Integration i konfiguracja przez środowisko są opisane; nie ma jeszcze realnego staging/production. |
| 2. Obrazy i registry | 50% | Obrazy są multi-stage, runtime backendu działa jako non-root, a CI publikuje obrazy; brakuje skanowania i pełnej polityki artefaktów. |
| 3. Migracje bazy | 30% | Migracje i startup migration działają lokalnie; brakuje osobnego kontrolowanego joba, testu upgrade/rollback i planu naprawy. |
| 4. Reverse proxy i TLS | 50% | Nginx, forwarded headers i lokalne cookie są sprawdzone; docelowe HTTPS, nagłówki bezpieczeństwa i limity proxy zależą od hostingu. |
| 5. Baza, storage i backup | 20% | Compose ma trwałe wolumeny lokalne; brak próbnego backupu, restore i produkcyjnego storage załączników. |
| 6. CI/CD | 60% | CI buduje i testuje backend/frontend/Compose, a CD publikuje obrazy GHCR; brak wdrożenia na konkretną platformę. |
| 7. Monitoring i procedury | 35% | Logi, correlation ID i health checks działają; brak alertów, dashboardów, runbooka i przetestowanego rollbacku. |

**Postęp V5: 41%**.

Wynik nie oznacza gotowości produkcyjnej. Największy brak V5 to wybrane, realne środowisko wdrożeniowe z backupem, monitoringiem i rollbackiem.

## Decyzja o środowisku

Najpierw należy wybrać jeden cel wdrożenia i opisać powód wyboru. Przykładowe opcje to:

- Azure App Service lub Container Apps;
- VPS z Docker Compose;
- inny dostawca kontenerów.

Kubernetes nie jest wymagany do zaliczenia tego etapu. Dla jednej aplikacji i jednej osoby może zwiększyć koszt operacyjny bez wartości edukacyjnej proporcjonalnej do złożoności.

## Zakres implementacyjny

### 1. Środowiska i konfiguracja

- rozdzielić local, test, staging i production;
- trzymać konfigurację poza obrazem;
- przechowywać sekrety w secret management właściwym dla platformy;
- walidować krytyczne ustawienia przy starcie;
- nie używać przykładowych sekretów w środowisku publicznym;
- opisać różnice cookie, CORS, SMTP, storage i bazy między środowiskami.

### 2. Obrazy i registry

- używać multi-stage Dockerfile;
- uruchamiać runtime z minimalnymi uprawnieniami, jeśli jest to możliwe;
- tagować obrazy wersją oraz identyfikatorem commit;
- nie polegać wyłącznie na tagu `latest`;
- skanować obrazy pod kątem znanych podatności;
- przechowywać artefakty w kontrolowanym registry.

### 3. Migracje bazy

- zdecydować, czy migracje wykonuje aplikacja, osobny job czy pipeline;
- unikać niekontrolowanych migracji przy starcie wielu instancji;
- testować migrację z poprzedniej wersji schematu;
- dokumentować operacje potencjalnie blokujące;
- przygotować procedurę rollbacku aplikacji i plan naprawy migracji.

### 4. Reverse proxy i TLS

- skonfigurować forwarded headers;
- zweryfikować generowanie absolutnych linków i redirectów;
- wymusić HTTPS w środowisku publicznym;
- sprawdzić Secure/SameSite cookies;
- dodać bezpieczne nagłówki HTTP;
- ustawić limity request body i timeouty proxy.

### 5. Baza, storage i backup

- używać trwałego storage PostgreSQL;
- zaplanować backup oraz retencję;
- wykonać próbne odtworzenie backupu;
- używać storage odpornego na restart dla załączników;
- ustalić retencję plików i danych;
- zweryfikować, że Data Protection key ring nie ginie po deployu.

### 6. CI/CD

Pipeline powinien wykonywać przynajmniej:

- restore i build;
- testy jednostkowe;
- testy integracyjne w kontrolowanym środowisku;
- test PostgreSQL;
- build obrazów;
- skanowanie obrazów;
- publikację artefaktu;
- wdrożenie na staging po wymaganej aprobacie;
- smoke test po wdrożeniu.

### 7. Monitoring i procedury

- logi aplikacji i proxy;
- health checks liveness/readiness;
- alerty dla błędów, niedostępności bazy i workerów;
- korelacja requestów;
- dashboard podstawowych metryk;
- runbook dla typowych awarii;
- procedura rollbacku;
- procedura rotacji sekretów;
- procedura odtworzenia bazy.

## Test plan

- deploy na czyste środowisko;
- restart aplikacji bez utraty sesji/kluczy zgodnie z polityką;
- niedostępność bazy podczas readiness check;
- niedostępność email/storage;
- migracja od poprzedniej wersji;
- rollback obrazu;
- odtworzenie backupu do oddzielnej bazy;
- smoke test przez publiczny adres HTTPS;
- weryfikacja cookie za proxy.

## Definition of Done

- istnieje działające środowisko staging lub production;
- deployment jest odtwarzalny z dokumentacji i pipeline'u;
- sekrety nie są zapisane w repozytorium ani obrazie;
- migracje są kontrolowane;
- backup został odtworzony przynajmniej raz;
- aplikacja ma logi, health checks i podstawowe alerty;
- istnieje rollback oraz runbook;
- po deployu wykonywany jest smoke test.

## Poza zakresem V5

- multi-region;
- Kubernetes bez konkretnej potrzeby;
- pełny SRE stack;
- automatyczna zmiana produkcji bez kontroli ryzyka;
- udawanie SLA bez realnych pomiarów i zobowiązań.

## Pytania kontrolne

- Kto i kiedy wykonuje migracje?
- Co się stanie, gdy nowa aplikacja wystartuje przed zakończeniem migracji?
- Jak odtworzysz bazę po uszkodzeniu danych?
- Gdzie są sekrety i jak rotujesz je bez publikowania w logach?
- Jak odróżnisz proces żywy od procesu gotowego do przyjmowania ruchu?
- Jak wycofasz wersję, która przeszła build, ale powoduje błąd po wdrożeniu?
