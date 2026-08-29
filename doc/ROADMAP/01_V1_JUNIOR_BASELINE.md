# V1: Aktualny baseline juniorowy

## Cel

V1 jest wersją bazową startera. Ma pokazać, że aplikacja może być zbudowana jako pełny, testowalny modularny monolit, a nie tylko zbiór endpointów CRUD.

V1 traktujemy jako w dużej mierze ukończone. Dalsza praca powinna przede wszystkim poprawiać poprawność i odporność istniejących mechanizmów.

## Co V1 już demonstruje

### Backend

- .NET 9 i ASP.NET Core Web API;
- routing, kontrolery, middleware i dependency injection;
- konfigurację przez Options pattern oraz zmienne środowiskowe;
- warstwy API, Application, Domain, Infrastructure i Shared;
- EF Core, PostgreSQL, migracje i konfiguracje encji;
- walidację FluentValidation;
- JWT access tokeny i refresh tokeny przechowywane jako hashe;
- refresh-token rotation w HttpOnly cookie;
- email confirmation, email 2FA, TOTP i recovery codes;
- role administracyjne oraz autoryzację zasobów projektu;
- projekty, zadania, członków, zaproszenia, komentarze i załączniki;
- paginację, filtrowanie i sortowanie zadań;
- powiadomienia, email outbox i hosted workers;
- Serilog, correlation ID i health checks;
- Docker Compose, Mailpit i CI.

### Frontend

- React + TypeScript;
- routing publiczny i chroniony;
- AuthContext oraz bootstrap sesji;
- centralny HttpClient;
- formularze z React Hook Form i Zod;
- widoki projektów, zadań, komentarzy, załączników, członków i zaproszeń;
- funkcjonalne ustawienia profilu i bezpieczeństwa;
- testy komponentów i kontekstu.

### Testy

- testy domenowe i usług;
- testy kontrolerów;
- testy integracyjne API;
- testy z rzeczywistym PostgreSQL przez Testcontainers;
- smoke tests uruchamiane przeciwko działającej aplikacji;
- frontendowe testy React Testing Library/Vitest.

## Czego V1 nie udaje

V1 nie jest jeszcze pełnym rozwiązaniem produkcyjnym. Znane ograniczenia obejmują między innymi:

- niedomkniętą politykę unieważniania sesji po zmianie hasła, resecie, dezaktywacji i zmianie roli;
- brak pełnego wykrywania replay refresh-token family;
- brak ochrony przed równoległą rotacją tego samego tokenu;
- częściowy rate limiting i brak pełnego lockoutu;
- brak spójnego kontraktu wszystkich błędów API;
- niespójny poziom enkapsulacji `Project`, `User` i `ProjectTask`;
- brak optimistic concurrency;
- procesowo lokalny stan zdrowia workerów;
- lokalne storage załączników bez pełnego production hardening;
- brak pełnego audytu bezpieczeństwa i globalnego wyszukiwania workspace;
- brak konkretnego środowiska staging/production;
- brak pomiarów wydajnościowych i testów obciążeniowych.

## Kryteria ukończenia V1

V1 można uznać za ukończone, gdy:

- aplikacja buduje się lokalnie i w CI;
- podstawowy przepływ rejestracja -> email confirmation -> login -> 2FA -> aplikacja działa;
- można utworzyć projekt, zadanie, członkostwo i zaproszenie;
- uprawnienia są egzekwowane po stronie backendu;
- testy normalnych ścieżek przechodzą;
- lokalne środowisko można uruchomić przez Docker Compose;
- ograniczenia są zapisane i nie są mylone z gotowością produkcyjną.

## Następny krok

Następnym etapem jest V3, zaczynające się od granic domeny, transakcji i optimistic concurrency. Nie należy rozpoczynać od nowych technologii infrastrukturalnych bez konkretnego problemu.
