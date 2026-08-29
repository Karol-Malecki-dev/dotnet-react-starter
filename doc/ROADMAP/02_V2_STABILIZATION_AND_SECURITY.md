# V2: Stabilizacja i bezpieczeństwo

## Cel

V2 ma domknąć istniejące fundamenty. Nie jest etapem dokładania wielu nowych funkcji. Jest etapem udowodnienia, że obecne auth, API i konfiguracja zachowują się poprawnie przy błędach, restartach, równoległych żądaniach i zmianach stanu konta.

To jest najbliższy priorytet rozwoju backendu.

## Dlaczego ten etap jest pierwszy

Starter ma już szeroki zakres funkcji, ale szeroki zakres nie oznacza jeszcze spójnego systemu. Największe ryzyko znajduje się w mechanizmach, które decydują o dostępie do konta i sesji:

- refresh token przechowuje snapshot użytkownika;
- konto może zmienić stan po wydaniu tokenu;
- kilka żądań może próbować obrócić ten sam token;
- reset hasła i zmiana hasła nie powinny pozostawiać starych sesji bez jasnej polityki;
- limity publicznych endpointów muszą obejmować całe auth, nie tylko login;
- klucze Data Protection muszą przeżyć restart procesu w docelowym środowisku.

## Zakres implementacyjny

### 1. Polityka sesji

Zdefiniować i udokumentować zachowanie dla:

- logoutu jednej sesji;
- logoutu wszystkich sesji;
- zmiany hasła;
- resetu hasła;
- dezaktywacji konta;
- aktywacji konta;
- zmiany roli;
- wyłączenia 2FA;
- wykrycia replay refresh tokenu.

Warto zapisać te reguły w ADR, ponieważ są decyzją bezpieczeństwa, a nie tylko szczegółem implementacji.

### 2. Refresh-token rotation

- sprawdzać aktualnego użytkownika w bazie przed odświeżeniem sesji;
- odrzucać refresh dla nieaktywnego lub usuniętego użytkownika;
- nie odbudowywać stanu konta wyłącznie ze snapshotu tokenu;
- zachować stabilne `FamilyId` w całym łańcuchu rotacji;
- oznaczać relację poprzedni -> następny;
- wykrywać użycie tokenu już cofniętego;
- ustalić, czy replay unieważnia całą rodzinę;
- zabezpieczyć zapis przed dwoma równoległymi refresh requestami;
- zweryfikować indeksy i constraints w bazie.

### 3. Unieważnianie po zmianie poświadczeń

Po zmianie lub resecie hasła należy zastosować ustaloną politykę, na przykład unieważnić wszystkie aktywne rodziny refresh tokenów użytkownika. To samo należy rozważyć dla zmiany ustawień bezpieczeństwa.

Testy muszą potwierdzić zarówno odrzucenie starych sesji, jak i możliwość normalnego ponownego logowania.

### 4. Brute-force protection

Decyzję dla tego fragmentu opisuje [ADR-09: Authentication brute-force protection](09_ADR_AUTH_BRUTE_FORCE_PROTECTION.md).

- rozszerzyć rate limiting na forgot password, reset password, confirm/resend confirmation i operacje 2FA;
- rozważyć partycjonowanie limitu po IP, koncie lub połączeniu obu strategii;
- dodać lockout po określonej liczbie nieudanych loginów;
- zapisywać moment odblokowania i liczbę prób;
- nie ujawniać, czy konto istnieje w publicznych flow;
- nie logować haseł, tokenów ani kodów.

Parametry powinny być konfigurowalne i testowalne.

### 5. Spójny kontrakt błędów

Ustalić jedną strategię dla:

- błędów walidacji;
- błędów domenowych;
- `401 Unauthorized`;
- `403 Forbidden`;
- `404 Not Found`;
- `409 Conflict`;
- `429 Too Many Requests`;
- nieoczekiwanych błędów serwera.

Należy zdecydować, czy projekt pozostaje przy `ApiResponse`, przechodzi na `ProblemDetails`, czy używa kontrolowanego połączenia obu formatów. Decyzja ma objąć backend i frontend.

### 6. Cancellation i async

- dodać `CancellationToken` do kontrolerów, interfejsów Application i zapytań EF;
- przekazywać token do `ToListAsync`, `SingleAsync`, `SaveChangesAsync` i operacji zewnętrznych;
- usunąć synchroniczne wywołania EF z metod `async`;
- rozróżniać anulowanie requestu od błędu biznesowego;
- sprawdzić graceful shutdown workerów.

### 7. Konfiguracja i deployment correctness

- utrwalać klucze Data Protection w środowisku, w którym chroniony jest sekret TOTP;
- skonfigurować `ForwardedHeaders` za nginx;
- zweryfikować `Secure`, `SameSite`, `Domain` i `Path` refresh cookie;
- przetestować lokalny HTTP oraz docelowe HTTPS osobno;
- zweryfikować CORS po uwzględnieniu proxy;
- nie uruchamiać produkcyjnego secretu z przykładowej konfiguracji;
- walidować krytyczne Options przy starcie.

### 8. Usunięcie nieaktywnego lub mylącego kodu

Po potwierdzeniu referencji i testów:

- zaimplementować albo usunąć `NotImplementedException`;
- rozstrzygnąć los pustych seederów;
- usunąć nieużywane alternatywne implementacje auth;
- usunąć lub wyraźnie oznaczyć nieużywane generyczne repository;
- nie utrzymywać dwóch wzorców jako aktywnie rekomendowanych.

## Test plan

### Unit tests

- polityka unieważniania sesji;
- replay refresh tokenu;
- lockout i reset licznika prób;
- mapowanie błędów;
- walidacja ustawień cookie i rate limitu.

### Integration tests

- refresh po dezaktywacji konta zwraca `401`;
- refresh po zmianie roli korzysta z aktualnej roli;
- reset hasła unieważnia stare sesje;
- zmiana hasła unieważnia stare sesje;
- replay refresh tokenu powoduje ustaloną reakcję całej rodziny;
- dwa równoległe refresh requesty nie tworzą dwóch poprawnych następców;
- publiczne endpointy auth zwracają neutralne odpowiedzi;
- rate limit zwraca `429` i nie ujawnia sekretów;
- restart aplikacji nie uniemożliwia poprawnej obsługi TOTP, jeśli storage kluczy jest skonfigurowany.

### Frontend tests

Frontend ma obsłużyć jednolity kontrakt błędów, wylogowanie po utracie sesji oraz pojedynczą próbę odświeżenia. Szczegółowa implementacja frontendu jest pomocnicza wobec backendu.

## Definition of Done

- polityka sesji jest opisana w ADR i dokumentacji auth;
- wszystkie zmiany auth mają test normalnej ścieżki i test nadużycia/awarii;
- nie ma synchronicznych zapytań EF w metodach asynchronicznych w dotkniętym zakresie;
- request cancellation jest przekazywany przez dotknięty przepływ;
- błędy walidacji i wyjątków mają jeden udokumentowany kontrakt;
- konfiguracja cookie, proxy i Data Protection jest sprawdzona testem lub procedurą uruchomieniową;
- build, testy jednostkowe i właściwe testy integracyjne przechodzą;
- dokumentacja opisuje ograniczenia oraz kolejne kroki.

## Poza zakresem V2

- Redis;
- mikroserwisy;
- pełny globalny search danych;
- zaawansowane metryki i distributed tracing;
- migracja całego projektu do nowej architektury;
- kosmetyczne refaktoryzacje frontendu.

## Pytania kontrolne

- Dlaczego refresh token musi sprawdzać aktualnego użytkownika?
- Czym różni się revocation pojedynczego tokenu od revocation całej rodziny?
- Co może pójść źle przy dwóch równoległych refresh requestach?
- Dlaczego reset hasła powinien kończyć stare sesje?
- Jak rate limiting wpływa na user enumeration?
- Co stanie się z TOTP po restarcie kontenera bez trwałego key ring?
