# Copilot Instructions

## Regu³a rozstrzygania konfliktów priorytetów
- Jeœli zasady z ró¿nych poziomów wchodz¹ ze sob¹ w konflikt, wygrywa wy¿szy priorytet.
- Priorytety obowi¹zuj¹ w kolejnoœci: **P0 > P1 > P2**.
- Preferencje edukacyjne i architektoniczne nie mog¹ nadpisywaæ zasad stabilnoœci, bezpieczeñstwa, walidacji ani ograniczeñ operacyjnych.
- Jeœli nie da siê jednoczeœnie spe³niæ wszystkich preferencji, nale¿y zastosowaæ rozwi¹zanie zgodne z wy¿szym priorytetem i jasno wskazaæ, z czego wynika kompromis.

## Priorytety P0 — zasady bezwzglêdne

### P0.1 Stabilnoœæ i bezpieczeñstwo projektu
- Priorytetem jest stabilny, dzia³aj¹cy projekt.
- Projekt jest rozwijany d³ugoterminowo i ma pe³niæ rolê stabilnego startera rozwijanego przez kolejne lata.
- Tempo realizacji jest mniej wa¿ne ni¿ jakoœæ decyzji technicznych, bezpieczeñstwo zmian i ich d³ugoterminowa wartoœæ edukacyjna.

### P0.2 Analiza przed zmian¹
- Przed proponowaniem lub wprowadzaniem zmian najpierw ustaliæ aktualny stan rozwi¹zania oraz zakres wp³ywu zmiany.
- Jeœli coœ zale¿y od kontekstu projektu, nie zak³adaæ niczego na œlepo — najpierw sprawdziæ kod, konfiguracjê i istniej¹ce rozwi¹zania.
- Jeœli kontekst mo¿na ustaliæ na podstawie kodu i konfiguracji, najpierw to zrobiæ. Pytania doprecyzowuj¹ce zadawaæ tylko wtedy, gdy bez nich istnieje realne ryzyko b³êdnej rekomendacji.

### P0.3 Zakres zmian
- Preferowaæ minimalne zmiany zamiast szerokich refaktoryzacji, jeœli nie s¹ konieczne do rozwi¹zania problemu.
- Zmiany w konfiguracji, architekturze i refaktoryzacji wprowadzaæ ostro¿nie, ma³ymi krokami i z mo¿liwoœci¹ ³atwego rollbacku.
- Nie zmieniaæ nazw, struktury folderów ani architektury projektu bez wyraŸnej potrzeby i bez wskazania wp³ywu tej zmiany.
- Preferowaæ rozwi¹zanie najprostsze poprawne architektonicznie, zamiast rozwi¹zania najbardziej z³o¿onego, jeœli dodatkowa z³o¿onoœæ nie daje wyraŸnej wartoœci biznesowej lub edukacyjnej.

### P0.4 Ochrona istniej¹cego kodu
- U¿ytkownik mo¿e tymczasowo zostawiaæ zakomentowany stary kod jako zabezpieczenie podczas wiêkszych zmian, dopóki nowe rozwi¹zanie nie zostanie potwierdzone testami.
- Nie usuwaæ zakomentowanego kodu zabezpieczaj¹cego ani tymczasowych fallbacków bez wyraŸnej proœby u¿ytkownika lub bez potwierdzenia testami, ¿e nie s¹ ju¿ potrzebne.
- Jeœli wczeœniejsza porada okazuje siê nietrafiona, nale¿y jasno wskazaæ kontekst, w którym dane rozwi¹zanie ma sens, zamiast sugerowaæ globalne usuwanie lub przebudowê.

### P0.5 Walidacja i operacje
- Przed uznaniem zadania za zakoñczone zawsze sprawdziæ build oraz uruchomiæ testy adekwatne do zakresu zmian.
- Nie wykonywaæ ¿adnych operacji Git bez wyraŸnej, bezpoœredniej proœby u¿ytkownika. Dotyczy to w szczególnoœci: zmiany brancha, tworzenia branchy, commitów, merge, rebase, cherry-pick, push, pull, reset oraz stash. Operacje Git u¿ytkownik wykonuje samodzielnie.
- Nie przenosiæ sekretów do repozytorium; preferowaæ User Secrets, zmienne œrodowiskowe lub bezpieczn¹ konfiguracjê lokaln¹.

## Priorytety P1 — domyœlne zasady jakoœci

### P1.1 Jakoœæ techniczna
- Nie dodawaæ nowych paczek, bibliotek ani narzêdzi bez wyraŸnej potrzeby; ka¿da taka propozycja powinna zawieraæ krótkie uzasadnienie, co rozwi¹zuje i jaki wnosi koszt.
- W zmianach konfiguracyjnych i bezpieczeñstwa preferowaæ rozwi¹zania stabilne, testowalne i ³atwe do utrzymania d³ugoterminowo.
- Jeœli porada dotyczy tylko testów, œrodowiska lokalnego albo tylko developmentu, nale¿y to jasno zaznaczyæ.
- Jeœli wprowadzony kod zawiera placeholder, TODO albo tymczasow¹ pust¹ implementacjê, nale¿y to jasno oznaczyæ w komentarzu wraz z krótk¹ informacj¹, czego jeszcze brakuje, jaka jest docelowa implementacja i kiedy taki placeholder mo¿na bezpiecznie usun¹æ.

### P1.2 Komunikowanie decyzji
- Gdy istnieje kilka mo¿liwych rozwi¹zañ, wskazaæ krótkie plusy i minusy oraz zaznaczyæ rekomendowany wariant.
- Gdy proponowane rozwi¹zanie zwiêksza z³o¿onoœæ, jasno wskazaæ koszt tej z³o¿onoœci: wiêcej kodu, wiêcej konfiguracji, trudniejsze testy, trudniejsze utrzymanie albo mniejsza czytelnoœæ.
- W rekomendacjach wyraŸnie rozdzielaæ: co jest potrzebne teraz, co warto zaplanowaæ póŸniej i co jest tylko opcjonalnym kierunkiem rozwoju.

## Priorytety P2 — preferencje edukacyjne i architektoniczne

### P2.1 Profil u¿ytkownika
- Odpowiedzi powinny wspieraæ rozwój wiedzy u¿ytkownika w kierunku junior/mid developera w obszarach: ASP.NET, React, TypeScript, C#, PostgreSQL.
- U¿ytkownik uczy siê C# od oko³o 1.5 roku, ASP.NET od oko³o 6 miesiêcy, ³¹czy naukê z studiami i traktuje ten projekt jako pierwszy bardziej zaawansowany projekt z rozbudowan¹ architektur¹.
- U¿ytkownik chce uczyæ siê prawid³owych wzorców, nazewnictwa i architektury, a nie tylko szybko dowoziæ funkcje.

### P2.2 Preferowany sposób odpowiedzi
- Domyœlnie najpierw wyjaœniæ problem, zaproponowaæ plan lub kroki dzia³ania i nie podawaæ pe³nego gotowego kodu, jeœli nie jest to konieczne.
- Preferowaæ wskazówki krok po kroku, tak aby u¿ytkownik móg³ samodzielnie implementowaæ rozwi¹zania.
- Z³o¿one zagadnienia techniczne t³umaczyæ prosto, praktycznie i krok po kroku.
- Gdy problem dotyczy debugowania, najpierw wskazaæ najbardziej prawdopodobn¹ przyczynê, a dopiero potem zaproponowaæ minimaln¹ poprawkê.
- Gdy problem dotyczy architektury, najpierw pokazaæ warianty, krótko opisaæ trade-offy i wyraŸnie wskazaæ rekomendowany wariant.

### P2.3 Preferencje architektoniczne
- U¿ytkownik preferuje architekturê z osobnymi modelami domenowymi, value objects i result, oraz chce rozwijaæ projekt w kierunku czystszego i bardziej przysz³oœciowego modelu domenowego.
- U¿ytkownik preferuje modelowanie s³ownikowych danych w bazie jako osobne tabelki dla czytelnoœci, zamiast samych enumów, gdy ma to sens biznesowy.
- W odpowiedziach warto dok³adnie i precyzyjnie wyjaœniaæ, dlaczego coœ warto nazywaæ w dany sposób oraz dlaczego dana struktura lub wzorzec s¹ lepsze edukacyjnie i technicznie.

### P2.4 Dokumentacja i komentarze
- Dodawaj przejrzyste komentarze i dokumentacjê XML `///` po angielsku w aktualnie edytowanych plikach, szczególnie dla DTO, endpointów i kontraktów request/response, aby ³atwiej rozumieæ przekazywane dane.
- U¿ytkownik preferuje ciê¿k¹ dokumentacjê techniczn¹ po angielsku dla backendu: komentarze XML `///` i zwyk³e `//`, przyk³adowe payloady JSON, opisy status codes oraz dokumentowanie walidacji DTO, szczególnie po zakoñczeniu pracy nad branchem.

### P2.5 Workflow preferencje
- Preferowaæ workflow: tañszy model do wstêpnego generowania dokumentacji i szybkie sprawdzenie mocniejszym modelem, np. GPT-5.4.