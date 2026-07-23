# Frontend Setup

Ten dokument opisuje aktualną strukturę frontendu i sposób rozwijania UI w tym starterze.

## When To Read This Document

Czytaj ten plik, gdy zmiana dotyczy stron, routingu, contextów, hooków, klienta API, formularzy albo runtime feature flags po stronie frontendu.

## Current Frontend Responsibilities

Frontend odpowiada za:

- routing aplikacji
- renderowanie publicznych i chronionych ekranów
- bootstrap sesji użytkownika
- bootstrap runtime feature flags
- wyświetlanie stanu auth, 2FA i profilu
- komunikację z backendem przez centralny HTTP client

## Frontend Structure

Najważniejsze katalogi w `frontend/src/`:

- `components/` - komponenty UI i shell aplikacji
- `context/` - globalny stan auth i runtime config
- `hooks/` - hooki dostępowe i feature gating
- `pages/` - strony aplikacji
- `services/` - komunikacja z API
- `types/` - typy TypeScript
- `utils/` - walidacja i funkcje pomocnicze
- `tests/` - testy frontendowe

## Bootstrap Order

Frontend nie renderuje od razu całego UI.

Kolejność startu jest taka:

1. `RuntimeConfigProvider` ładuje `GET /api/runtime-config`.
2. `AuthProvider` próbuje odtworzyć sesję z localStorage i refresh token cookie.
3. `AppBootstrapGate` czeka aż auth i runtime config przestaną być w stanie loading.
4. Dopiero potem renderuje się `AppShell`.

To ogranicza migotanie UI i przypadkowe pokazywanie elementów, które po chwili miałyby zniknąć.

## State Management Pattern

Frontend nie używa tu osobnego store typu Redux czy Zustand.

Zamiast tego główne stany globalne są trzymane w contextach:

- `AuthContext`
- `RuntimeConfigContext`

Pattern jest prosty:

- context przechowuje stan i metody
- hook udostępnia wygodny dostęp
- komponenty korzystają tylko z hooka, nie z surowego contextu

## API Layer Pattern

Warstwa API jest scentralizowana.

Najważniejszy plik:

- `frontend/src/services/api/HttpClient.ts`

Ten klient odpowiada za:

- budowanie `baseUrl`
- dołączanie access tokenu do requestów
- wysyłanie `credentials: 'include'` dla refresh token cookie
- retry po `401`, jeśli da się odświeżyć sesję
- mapowanie błędów i generowanie globalnych notice

Na nim opierają się konkretne klienty, np. auth, users i runtime config.

## Authentication on the Frontend

Najważniejsze miejsca auth po stronie UI:

- `context/AuthContext.tsx`
- `hooks/useAuth.ts`
- `components/UI/ProtectedRoute.tsx`
- strony auth w `pages/`

Frontend przechowuje:

- access token w localStorage
- user snapshot w localStorage
- pending 2FA challenge w sessionStorage

Frontend nie ma dostępu do refresh tokenu, bo ten siedzi w HttpOnly cookie.

## Runtime Config Pattern

Runtime feature flags są ładowane z backendu podczas startu aplikacji.

Najważniejsze pliki:

- `context/RuntimeConfigContext.tsx`
- `hooks/useRuntimeConfig.ts`
- `hooks/useFeatureAvailability.ts`
- `types/runtimeConfig.ts`

Zasada jest ważna:

- `RuntimeConfigContext` zna pełny obiekt runtime config
- `useFeatureAvailability()` wystawia już uproszczone booleany dla UI
- komponenty nie powinny samodzielnie parsować odpowiedzi API

## Where Feature Flags Affect the UI

Aktualnie flagi sterują między innymi:

- quick search w navbarze
- widocznością dashboardu
- widocznością sekcji admin
- widocznością listy użytkowników
- sekcjami związanymi z email features
- dostępnością flow 2FA

Najważniejsze miejsca użycia:

- `components/UI/Navbar.tsx`
- `components/AppRoutes.tsx`
- `pages/Home.tsx`
- `pages/Dashboard.tsx`
- `components/UI/AppBootstrapGate.tsx`

## Routing Pattern

Routing jest zebrany centralnie w `components/AppRoutes.tsx`.

Podział odpowiedzialności:

- strony publiczne są definiowane bez wrappera
- strony chronione są pod `ProtectedRoute`
- trasy admina dodatkowo sprawdzają role
- runtime flags dodatkowo sterują widocznością tras i redirectami

To znaczy, że decyzje o dostępności ekranów powinny trafiać najpierw do routingu i shella, a dopiero później do konkretnej strony.

## Forms and Validation

Formularze opierają się o:

- `react-hook-form`
- `zod`
- `@hookform/resolvers`

Schemat jest prosty:

1. definicja schematu w `utils/`
2. inferowany typ formularza
3. `useForm()` z `zodResolver`
4. request do API klienta

## Environment Configuration

Frontend używa tylko publicznych wartości build-time z prefiksem `REACT_APP_`.

Najważniejsza zmienna:

- `REACT_APP_API_URL`

Przykłady:

- local frontend do local backendu: `http://localhost:5000`
- Docker/nginx reverse proxy: `/api`

Nigdy nie wkładaj sekretów do `REACT_APP_*`.

## Naming Guidelines

Kilka praktycznych zasad nazewnictwa:

- context nazywaj według globalnego obszaru stanu, np. `AuthContext`, `RuntimeConfigContext`
- hooki dostępowe zaczynaj od `use`, np. `useAuth`, `useRuntimeConfig`, `useFeatureAvailability`
- strony nazywaj po ekranie lub flow, np. `Login`, `ConfirmEmail`, `ResetPassword`
- klienty API nazywaj według obszaru odpowiedzialności, np. `AuthApi`, `UserApi`, `RuntimeConfigApi`
- typy współdzielone grupuj według domeny, a nie losowo

## What Not To Do

- nie odczytuj feature flags bezpośrednio z kilku różnych źródeł
- nie duplikuj logiki auth w wielu komponentach
- nie blokuj bezpieczeństwa tylko po stronie frontendu
- nie dodawaj nowych tras bez decyzji, czy są publiczne, protected czy admin-only

## See Also

- `doc/ARCHITECTURE.md` - pełna mapa projektu i główne zależności między warstwami
- `doc/JWT_ARCHITECTURE.md` - bootstrap sesji, access token i refresh flow
- `doc/EMAIL_2FA_FLOWS.md` - ekrany confirm email, verify 2FA i reset hasła w kontekście całego flow
- `doc/BACKEND_SETUP.md` - endpointy, persistence i konfiguracja, z którymi frontend współpracuje
