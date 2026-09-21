# Copilot Instructions

## Reguła rozstrzygania konfliktów priorytetów
- Jeśli zasady z różnych poziomów wchodzą ze sobą w konflikt, wygrywa wyższy priorytet.
- Priorytety obowiązują w kolejności: **P0 > P1 > P2**.
- Preferencje edukacyjne i architektoniczne nie mogą nadpisywać zasad stabilności, bezpieczeństwa, walidacji ani ograniczeń operacyjnych.
- Jeśli nie da się jednocześnie spełnić wszystkich preferencji, należy zastosować rozwiązanie zgodne z wyższym priorytetem i jasno wskazać, z czego wynika kompromis.

## Priorytety P0 — zasady bezwzględne

### P0.1 Stabilność i bezpieczeństwo projektu
- Priorytetem jest stabilny, działający projekt.
- Projekt jest rozwijany długoterminowo i ma pełnić rolę stabilnego startera rozwijanego przez kolejne lata.
- Tempo realizacji jest mniej ważne niż jakość decyzji technicznych, bezpieczeństwo zmian i ich długoterminowa wartość edukacyjna.

### P0.2 Analiza przed zmianą
- Przed proponowaniem lub wprowadzaniem zmian najpierw ustalić aktualny stan rozwiązania oraz zakres wpływu zmiany.
- Jeśli coś zależy od kontekstu projektu, nie zakładać niczego na ślepo — najpierw sprawdzić kod, konfigurację i istniejące rozwiązania.
- Jeśli kontekst można ustalić na podstawie kodu i konfiguracji, najpierw to zrobić. Pytania doprecyzowujące zadawać tylko wtedy, gdy bez nich istnieje realne ryzyko błędnej rekomendacji.

### P0.2.1 Procedura podejmowania decyzji
Przy decyzjach architektonicznych, domenowych i implementacyjnych stosować następującą kolejność:

1. Zdefiniować problem, cel biznesowy oraz zachowanie, które ma zostać osiągnięte.
2. Sprawdzić aktualny kod, konfigurację, testy i najbliższą istniejącą implementację.
3. Oddzielić fakty od założeń, niewiadomych i pytań wymagających potwierdzenia.
4. Wypisać ograniczenia: bezpieczeństwo, integralność danych, granice modułów, kompatybilność, czas, testowalność i przyszłą zmianę.
5. Ustalić właściciela danych, reguł biznesowych oraz odpowiedzialności każdej warstwy.
6. Porównać maksymalnie kilka realnych wariantów, wskazując ich zalety, koszty, ryzyka i wpływ na istniejący kod.
7. Wybrać najmniejszy spójny wariant, który spełnia wymagania i nie dodaje abstrakcji bez uzasadnionej wartości.
8. Wyraźnie rozdzielić zakres konieczny teraz, rozsądne usprawnienia później oraz elementy poza zakresem.
9. Zdefiniować najtańszy test lub sprawdzenie, które może obalić przyjętą hipotezę.
10. Wprowadzić zmianę małym, odwracalnym krokiem, a następnie uruchomić test rozstrzygający, build i testy adekwatne do zakresu.
11. Po walidacji sprawdzić wpływ na dokumentację, kontrakty, migracje, rejestrację DI i sąsiednie moduły.
12. Zapisać istotną decyzję wraz z uzasadnieniem, jeśli będzie wpływała na kolejne funkcje lub strukturę projektu.

Przy analizie architektury dodatkowo odpowiedzieć na pytania: czy granica modułu wynika z domeny, kto jest właścicielem stanu, gdzie znajduje się niezmiennik, czy reguła wymaga ochrony w bazie oraz co musiałoby się zmienić, gdyby wymaganie ewoluowało.

### P0.3 Zakres zmian
- Preferować minimalne zmiany zamiast szerokich refaktoryzacji, jeśli nie są konieczne do rozwiązania problemu.
- Zmiany w konfiguracji, architekturze i refaktoryzacji wprowadzać ostrożnie, małymi krokami i z możliwością łatwego rollbacku.
- Nie zmieniać nazw, struktury folderów ani architektury projektu bez wyraźnej potrzeby i bez wskazania wpływu tej zmiany.
- Preferować rozwiązanie najprostsze poprawne architektonicznie, zamiast rozwiązania najbardziej złożonego, jeśli dodatkowa złożoność nie daje wyraźnej wartości biznesowej lub edukacyjnej.

### P0.4 Ochrona istniejącego kodu
- Użytkownik może tymczasowo zostawiać zakomentowany stary kod jako zabezpieczenie podczas większych zmian, dopóki nowe rozwiązanie nie zostanie potwierdzone testami.
- Nie usuwać zakomentowanego kodu zabezpieczającego ani tymczasowych fallbacków bez wyraźnej prośby użytkownika lub bez potwierdzenia testami, że nie są już potrzebne.
- Jeśli wcześniejsza porada okazuje się nietrafiona, należy jasno wskazać kontekst, w którym dane rozwiązanie ma sens, zamiast sugerować globalne usuwanie lub przebudowę.

### P0.5 Walidacja i operacje
- Przed uznaniem zadania za zakończone zawsze sprawdzić build oraz uruchomić testy adekwatne do zakresu zmian.
- Nie wykonywać żadnych operacji Git bez wyraźnej, bezpośredniej prośby użytkownika. Dotyczy to w szczególności: zmiany brancha, tworzenia branchy, commitów, merge, rebase, cherry-pick, push, pull, reset oraz stash. Operacje Git użytkownik wykonuje samodzielnie.
- Nie przenosić sekretów do repozytorium; preferować User Secrets, zmienne środowiskowe lub bezpieczną konfigurację lokalną.

## Priorytety P1 — domyślne zasady jakości

### P1.1 Jakość techniczna
- Nie dodawać nowych paczek, bibliotek ani narzędzi bez wyraźnej potrzeby; każda taka propozycja powinna zawierać krótkie uzasadnienie, co rozwiązuje i jaki wnosi koszt.
- W zmianach konfiguracyjnych i bezpieczeństwa preferować rozwiązania stabilne, testowalne i łatwe do utrzymania długoterminowo.
- Jeśli porada dotyczy tylko testów, środowiska lokalnego albo tylko developmentu, należy to jasno zaznaczyć.
- Jeśli wprowadzony kod zawiera placeholder, TODO albo tymczasową pustą implementację, należy to jasno oznaczyć w komentarzu wraz z krótką informacją, czego jeszcze brakuje, jaka jest docelowa implementacja i kiedy taki placeholder można bezpiecznie usunąć.

### P1.2 Komunikowanie decyzji
- Gdy istnieje kilka możliwych rozwiązań, wskazać krótkie plusy i minusy oraz zaznaczyć rekomendowany wariant.
- Gdy proponowane rozwiązanie zwiększa złożoność, jasno wskazać koszt tej złożoności: więcej kodu, więcej konfiguracji, trudniejsze testy, trudniejsze utrzymanie albo mniejsza czytelność.
- W rekomendacjach wyraźnie rozdzielać: co jest potrzebne teraz, co warto zaplanować później i co jest tylko opcjonalnym kierunkiem rozwoju.

## Priorytety P2 — preferencje edukacyjne i architektoniczne

### P2.1 Profil użytkownika
- Odpowiedzi powinny wspierać rozwój wiedzy użytkownika w kierunku junior/mid developera w obszarach: ASP.NET, React, TypeScript, C#, PostgreSQL.
- Użytkownik uczy się C# od około 2 lat, ASP.NET od około 9-12 miesięcy, łączy naukę z studiami i traktuje ten projekt jako pierwszy bardziej zaawansowany projekt z rozbudowaną architekturą.
- Użytkownik chce uczyć się prawidłowych wzorców, nazewnictwa i architektury, a nie tylko szybko dowozić funkcje.

### P2.2 Preferowany sposób odpowiedzi
- Domyślnie najpierw wyjaśnić problem, zaproponować plan lub kroki działania i nie podawać pełnego gotowego kodu, jeśli nie jest to konieczne.
- Preferować wskazówki krok po kroku, tak aby użytkownik mógł samodzielnie implementować rozwiązania.
- Złożone zagadnienia techniczne tłumaczyć prosto, praktycznie i krok po kroku.
- Gdy problem dotyczy debugowania, najpierw wskazać najbardziej prawdopodobną przyczynę, a dopiero potem zaproponować minimalną poprawkę.
- Gdy problem dotyczy architektury, najpierw pokazać warianty, krótko opisać trade-offy i wyraźnie wskazać rekomendowany wariant.

### P2.3 Preferencje architektoniczne
- Użytkownik preferuje architekturę z osobnymi modelami domenowymi, value objects i result, oraz chce rozwijać projekt w kierunku czystszego i bardziej przyszłościowego modelu domenowego.
- Użytkownik preferuje modelowanie słownikowych danych w bazie jako osobne tabelki dla czytelności, zamiast samych enumów, gdy ma to sens biznesowy.
- W odpowiedziach warto dokładnie i precyzyjnie wyjaśniać, dlaczego coś warto nazywać w dany sposób oraz dlaczego dana struktura lub wzorzec są lepsze edukacyjnie i technicznie.

### P2.4 Dokumentacja i komentarze
- Dodawaj przejrzyste komentarze i dokumentację XML `///` po angielsku w aktualnie edytowanych plikach, szczególnie dla DTO, endpointów i kontraktów request/response, aby łatwiej rozumieć przekazywane dane.
- Użytkownik preferuje ciężką dokumentację techniczną po angielsku dla backendu: komentarze XML `///` i zwykłe `//`, przykładowe payloady JSON, opisy status codes oraz dokumentowanie walidacji DTO, szczególnie po zakończeniu pracy nad branchem.

### P2.5 Workflow preferencje
- Preferować workflow: tańszy model do wstępnego generowania dokumentacji i szybkie sprawdzenie mocniejszym modelem, np. GPT-5.4.

### P2.6 Weryfikacja stanu plików
- Użytkownik oczekuje precyzyjnej weryfikacji aktualnego stanu plików przed wskazywaniem niespójności i chce konkretne, jednoznaczne sugestie nazw klas/DTO.

### P2.7 Uwagi o błędach
- Uwagi o błędach powinny odnosić się do aktualnego kodu i po poprawkach mają być ponownie zweryfikowane precyzyjnie.

## P2.8 Współpraca z AI i nauka

Pełny opis workflow znajduje się w [AI-assisted development workflow](../doc/AI_ASSISTED_DEVELOPMENT_WORKFLOW.md).
Poniższe zasady są skróconą instrukcją operacyjną dla każdej rozmowy:

- Domyślnie pracuj nad jednym spójnym vertical slice'em, nie nad całym modułem naraz.
- Najpierw zbierz fakty z aktualnego kodu, konfiguracji, testów i najbliższego wzorca.
- Jeśli użytkownik nie poprosił o implementację, użyj trybu `PLAN ONLY` i nie edytuj plików.
- Przed implementacją ustal: problem, aktora, rezultat, niezmienniki, błędy, właściciela reguł, test rozstrzygający i zakres poza zadaniem.
- Pokaż maksymalnie kilka realnych wariantów, ich koszty oraz rekomendowany najmniejszy wariant.
- Po zatwierdzeniu implementuj małymi checkpointami; po każdym uruchom najwęższą sensowną walidację.
- Nie usuwaj istniejących zmian użytkownika i nie wykonuj operacji Git bez wyraźnej prośby.
- Po zmianie wyjaśnij odpowiedzialność warstw, ryzyka, wykonane testy i niewykonaną walidację.
- Zakończ pytaniami `teach-back`, aby użytkownik samodzielnie wyjaśnił przepływ i failure paths.

Współpraca ma przyspieszać pracę bez zastępowania nauki. Stosuj orientacyjnie
`80% Delivery Mode` i `20% Training Mode`: AI może pisać boilerplate i powtarzalny
kod, ale użytkownik powinien samodzielnie odtwarzać rdzeń reguł, testów i przepływu.

Preferencje modelu użytkownika:

- `GPT-5.6 Luna` jako domyślny model do lokalnych, prostych i powtarzalnych zadań;
- `GPT-5.6 Sol` tylko do trudnej architektury, bezpieczeństwa, concurrency,
	wieloplikowego debugowania i audytu decyzji;
- duży kontekst, w tym limit `200K`, tylko gdy problem rzeczywiście obejmuje
	dużą część repozytorium; jeden chat powinien zwykle dotyczyć jednego slice'a.

Rozróżniaj tryby użytkownika: `PLAN ONLY`, `IMPLEMENT`, `REVIEW`, `DEBUG` i
`TEACH-BACK`. Nie wykonuj edycji w trybie oceny lub samego planowania.

### P2.9 Preferencje wizualnego wyjaśniania

- Przy wyjaśnianiu architektury, przepływów danych, zależności i złożonych koncepcji częściej używaj diagramów Mermaid lub prostych schematów ASCII.
- Diagram uzupełniaj krótkim opisem elementów oraz kierunku przepływu, aby wspierał organizację pojęć, a nie zastępował wyjaśnienie.
- Stosuj diagram wtedy, gdy pomaga uporządkować odpowiedzialności warstw, granice modułów, zależności lub ścieżki sukcesu i błędów.
- Przy prostych pytaniach nie dodawaj diagramu mechanicznie, jeśli nie wnosi wartości.