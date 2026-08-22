# Workflow nauki i realizacji etapów

## Cel

Starter jest rozwijany wspólnie z asystentem. Asystent może przygotowywać implementację, testy i dokumentację, a właściciel projektu czyta kod, rozumie decyzje i później samodzielnie odtwarza wzorce w nowych projektach.

Głównym celem nie jest samo dopisanie funkcji. Celem jest zbudowanie rozumienia od teorii do praktyki.

## Rola asystenta

Asystent powinien:

- analizować aktualny kod przed zmianą;
- wybierać najmniejszy poprawny zakres;
- implementować backend jako priorytet;
- utrzymywać frontend funkcjonalny, ale nie rozwijać go kosztem celu backendowego;
- pisać testy razem z implementacją;
- aktualizować dokumentację i ADR-y;
- uruchamiać adekwatny build i testy;
- wskazywać ryzyka, ograniczenia i miejsca wymagające decyzji;
- nie dodawać bibliotek bez konkretnego uzasadnienia;
- nie wykonywać operacji Git bez wyraźnej prośby.

## Rola właściciela projektu

Właściciel projektu powinien:

- czytać zmieniony kod;
- zadawać pytania o decyzje i alternatywy;
- samodzielnie tłumaczyć przepływ po zakończeniu etapu;
- uruchamiać lub powtarzać wybrane testy;
- prowadzić notatki o tym, co było trudne;
- w kolejnych projektach wykonywać około 70-80% implementacji;
- korzystać z asystenta głównie do planowania, code review, debugowania i weryfikacji.

## Schemat pracy nad zadaniem

1. Zdefiniować problem i kryterium sukcesu.
2. Odczytać lokalny kod, kontrakt i sąsiednie testy.
3. Sformułować jedną hipotezę o właściwym miejscu zmiany.
4. Wybrać najmniejszą implementację, która może tę hipotezę sprawdzić.
5. Dodać lub zmienić test.
6. Uruchomić najwęższą sensowną walidację.
7. Naprawić lokalne problemy i ponowić ten sam test.
8. Dodać dokumentację lub ADR, jeśli zmieniła się decyzja.
9. Wykonać szerszy build/test odpowiedni do ryzyka.
10. Zakończyć krótkim podsumowaniem i pytaniami kontrolnymi.

## Siedem pytań dla każdej funkcji

1. Jaki problem rozwiązuje?
2. Dlaczego wybrano to rozwiązanie?
3. Jakie ma ograniczenia?
4. Jak jest testowane?
5. Co dzieje się przy awarii?
6. Jak można je zmienić?
7. Czy można zastosować je w nowym projekcie?

## Dziennik tarcia

Dla każdego większego zadania warto zapisać:

- czego nie rozumiałem;
- gdzie powstał boilerplate;
- co było trudne w narzędziach lub deployment;
- jaki błąd wynikał z niejasnej granicy;
- który test był najtrudniejszy;
- która decyzja zmieniła się po zebraniu dowodów;
- czy rozwiązanie było potrzebne, czy przeprojektowane.

## Artefakty końca etapu

Każdy etap powinien kończyć się:

- kodem;
- testami;
- dokumentacją;
- ADR-ami, jeśli są potrzebne;
- wynikami builda i testów;
- krótkim raportem ryzyk;
- odpowiedziami na siedem pytań;
- listą rzeczy nadal niejasnych.

## Strategia branchy

Preferowany jest jeden większy temat na branch. Przykłady:

```text
feature/auth-session-hardening
feature/optimistic-concurrency
feature/security-audit
feature/workspace-search
chore/deployment-readiness
perf/project-dashboard-query
```

Dokumentacja całej roadmapy może być na osobnym branchu:

```text
docs/project-development-roadmap
```

Nie trzeba tworzyć nowego brancha dla każdej małej poprawki w ramach aktywnego tematu.

## Jak oceniać postęp

Nie oceniać etapu wyłącznie liczbą endpointów. Sprawdzać, czy potrafisz:

- wskazać właściciela reguły;
- opisać przepływ danych;
- wyjaśnić statusy HTTP;
- przewidzieć częściową awarię;
- odróżnić test jednostkowy od integracyjnego;
- powiedzieć, co zmieni się przy większej liczbie danych;
- uzasadnić użycie lub odrzucenie biblioteki;
- odtworzyć podobne rozwiązanie w nowym projekcie.

## Relacja do poziomu zawodowego

Ukończenie roadmapy może dać techniczne kompetencje junior+ i częściowo mid-like. Nie zastępuje jednak doświadczenia w utrzymaniu produkcji, pracy zespołowej, code review, zmianach wymagań, awariach i odpowiedzialności za biznes.
