# STYLE GUIDE: wygląd gry

Opis trzech obrazów referencyjnych (lokalnie w `References/`, poza repozytorium, bo to zrzuty z cudzej gry). Opis ma wystarczyć, żeby odtworzyć wygląd bez patrzenia na obrazy.

**Zasada:** odtwarzamy klimat własnymi prostymi modelami (prymitywy, low-poly). Nie kopiujemy nazwy, logo, fontów, muzyki ani modeli oryginału.

## 0. Mixed reality: jak to stosujemy

Stopklatka zostaje grą MR (decyzja właściciela, `PLAN.md`). Otoczeniem jest prawdziwy pokój widziany przez kamery, więc:
- **Biały świat** daje filtr obrazu z kamer „SUPERHOT” (`PassthroughStyle`). Gradient jasności z kamer odwzorowuje się na paletę z punktu 3: ciemne `#B9C6CC`, średnie `#E6EBED`, jasne `#F2F4F5`→`#FFFFFF`.
- **Punkty 4–5 o otoczeniu** (lightmapy, mgła, materiały ścian) dotyczą **wirtualnych rekwizytów**: postumentów, posągów, latarni, osłon i ramy windy.
- **Przeciwnicy, broń, efekty i napisy** wyglądają dokładnie tak jak w punktach 6–10.

## 1. Wrażenie ogólne

Trzy kolory i nic więcej:
- **Biel:** świat (ściany, podłoga, meble, posągi), lekko chłodna, w głębi przechodzi w turkusową mgłę.
- **Czerwień:** wszystko, co żyje i chce cię zabić (przeciwnicy i ich odłamki).
- **Czerń:** wszystko, czego możesz użyć (broń, przedmioty do rzucania, twoje dłonie).

Akcent: **żółto-pomarańczowe świecenie** tylko tam, gdzie przeciwnik pęka. Do tego jaskrawoczerwone smugi pocisków.

Kontrast jest ważniejszy niż detal. Przeciwnik musi „krzyczeć” czerwienią na białym tle z każdej odległości.

## 2. Obrazy

### Obraz 1: ulica, lecący pocisk, umierający przeciwnik
- **Kadr:** z oczu gracza. W prawej ręce czarny rewolwer, wycelowany w przód i lekko w prawo.
- **Otoczenie:** biała uliczka.
  - Po obu stronach fasady budynków z kolumnami, prostymi gzymsami i prostokątnymi otworami.
  - Latarnie: smukły słup z prostą głowicą.
  - Po lewej niska barierka z pionowych prętów.
  - Na środku dwa białe samochody: proste sedany z kilku brył (nadwozie, kabina, koła jako walce), bez detali.
- **Podłoga:** duże kwadratowe płyty około 1×1 m, oddzielone cienką szarą fugą.
- **Głąb:** biała mgła rozmywa dalekie budynki, horyzont jest prawie biały.
- **Pocisk:**
  - Mały czarny podłużny pocisk, kapsuła około 2–3 cm długości i 1 cm średnicy.
  - Za nim bardzo długi (3–6 m), cienki (około 1 cm), prosty, jaskrawoczerwony ślad o niemal stałej grubości, przezroczysty na samym końcu.
  - Leci od przeciwnika po prawej w poprzek kadru.
- **Przeciwnicy:**
  - Po prawej: idzie w stronę gracza i celuje czarnym pistoletem z wyprostowanej ręki.
  - Na środku, w trakcie śmierci:
    - głowy już nie ma, z szyi i barków tryska chmura drobnych czerwonych odłamków,
    - jeden czarny pistolet wylatuje mu z ręki,
    - tułów i nogi jeszcze stoją.
  - Na ziemi: leży martwy, rozbity na kilka dużych kawałków. Na przełomach świeci żółto-pomarańczowy środek.
- **Na podłodze:** porzucone czarne pistolety.

### Obraz 2: galeria, walka wręcz
- **Kadr:** z oczu gracza. Prawa ręka (czarna, fasetowana, do łokcia) wchodzi w kadr z prawego dolnego rogu i uderza pięścią w pierś przeciwnika.
- **Otoczenie:** długi korytarz galerii o szerokości około 4 m, wysoki.
  - Po lewej rząd prostopadłościennych postumentów i kolumn, na niektórych białe fasetowane posągi postaci w pozach.
  - Po prawej ciągła ściana z wysokimi pionowymi oknami. Wpada przez nie prześwietlone białe światło, które rysuje jasne prostokąty na podłodze.
  - Głąb korytarza tonie w jasnoturkusowej mgle.
- **Przeciwnik trafiony:**
  - Duży, szeroki w barkach.
  - Głowa wydłużona i wąska, jak wysoki, ścięty wielościenny kryształ bez twarzy.
  - Od pięści rozchodzą się żółto-pomarańczowe świecące pęknięcia, odpadają czerwone trójkąty.
  - Ręce uniesione w szoku.
- **W tle:** trzech kolejnych biegnie w dynamicznych pozach (szeroki krok, ręce w ruchu), jeden niesie czarny przedmiot.
- **Powierzchnia przeciwników:** delikatne ukośne kreskowanie, czyli cienkie ciemniejsze paski pod kątem około 45°, widoczne z bliska.

### Obraz 3: galeria, przeciwnik przecięty kataną
- **Kadr:** ten sam typ korytarza.
- **Katana:** u dołu kadru leży poziomo długie, prawie proste czarne ostrze, którym właśnie cięto. Po prawej rękojeść z owijką i małą okrągłą gardą.
- **Przecięty przeciwnik:**
  - Rozcięty w pasie ukośnie. Nogi i biodra jeszcze stoją, górna część obraca się i odlatuje w prawo.
  - Obie powierzchnie cięcia są płaskie i świecą jednolicie jasnożółto-pomarańczowo, jak gładki przekrój kryształu.
  - Z linii cięcia sypie się wachlarz kilkudziesięciu drobnych czerwonych odłamków.
- **Pozostali:** po lewej przeciwnik w pozie obronnej (ręce skrzyżowane przed twarzą), w głębi kolejny biegnie.
- **W tle:** biały posąg na postumencie.

## 3. Paleta (HEX)

| Rola | Kolor |
| --- | --- |
| Podłoga w świetle | `#F2F4F5` |
| Ściany | `#E6EBED` |
| Ściany i bryły w cieniu | `#D3DCE0` |
| Fugi płyt | `#C7D1D6` |
| Miękki cień rzucony | `#B9C6CC` (chłodny szaroniebieski, nigdy czarny) |
| Mgła i głąb | `#DDEFF4` → `#CBE5EE` (jasny cyan) |
| Okna, światło, niebo | `#FFFFFF` |
| Przeciwnik, ścianka w pełnym świetle | `#FF2B1C` |
| Przeciwnik, ścianka średnia | `#E0150F` |
| Przeciwnik, ścianka w cieniu | `#9E0A08` |
| Przeciwnik, najciemniejsze zagłębienia | `#6A0505` |
| Kreskowanie na czerwieni | `#B80E0A` przy około 25% krycia |
| Wnętrze, pęknięcia, przekroje: rdzeń | `#FFD84D` (emisja) |
| Wnętrze, przekroje: brzeg | `#FF9A1F` (emisja) |
| Broń i dłonie gracza: baza | `#0B141A` |
| Broń i dłonie: ścianka w świetle | `#1E3242` |
| Broń i dłonie: krawędź, połysk | `#3A5A6E` (granatowo-turkusowy) |
| Pocisk: korpus | `#121212` |
| Smuga pocisku | `#FF1E1E` → przezroczysty |
| Posągi | `#ECEFF0`, fasety `#D9E0E3` |

## 4. Światło i cienie
- **Jasno (high-key):**
  - jedno główne światło kierunkowe padające wysoko z boku,
  - mocne światło otoczenia w kolorze bieli z lekką domieszką cyanu.
- **Cienie miękkie i jasne:** `#B9C6CC`, krawędzie rozmyte, nigdy czarne. Delikatne przyciemnienie w narożnikach (ambient occlusion).
- **Głębia:** mgła liniowa w kolorze `#DDEFF4`, od około 8 m do 40 m. W wąskich pomieszczeniach bliżej.
- **Okna i otwory:** prześwietlone, czysta biel. Efekt „bloom” udajemy nieoświetlonymi (unlit) płaszczyznami z białym gradientem przy krawędziach okien, a na podłodze prostokątami jaśniejszego światła wypalonymi w lightmapie. Prawdziwego post-processingu nie używamy (wydajność na Quest).
- **Przeciwnicy i broń** nie biorą udziału w wypalaniu światła. Mają własne cieniowanie w shaderze (dwa tony plus połysk), a pod stopami miękki cień w formie krążka.

## 5. Materiały i ustawienia w URP

| Materiał | Shader | Ustawienia |
| --- | --- | --- |
| Ściany, podłoga, meble, samochody | URP `Simple Lit` albo `Baked Lit` | Albedo `#F2F4F5`/`#E6EBED`, smoothness 0,05–0,1 (mat), bez specular. Lightmapy wypalone, `Contribute GI`. Podłoga: tekstura 256 px z fugą `#C7D1D6` (tiling 1 płyta = 1 m) |
| Posągi | `Stopklatka/Crystal` | Lit `#ECEFF0`, shadow `#D9E0E3`, rim słaby (0,3), gloss niski |
| Przeciwnik | `Stopklatka/Crystal` | Lit `#FF2B1C`, shadow `#9E0A08`, rim `#FF6A5A` 0,6, highlight 0,5, gloss 40. „Lekko szklisty” = rim plus połysk, bez prawdziwej przezroczystości. Kreskowanie: paski 45° w przestrzeni obiektu, gęstość około 60 na metr, krycie 25% |
| Odłamki przeciwnika | jak przeciwnik, dwa odcienie | 1/3 odłamków ciemniejsza (`#9E0A08` jako lit) |
| Wnętrze, przekroje | `Stopklatka/Transparent` albo `Unlit` | Kolor `#FFD84D`, brzeg `#FF9A1F`. Pełna jasność, bez cieni |
| Broń i przedmioty | `Stopklatka/Crystal` | Lit `#1E3242`, shadow `#0B141A`, rim `#3A5A6E` 0,5, highlight 0,9, gloss 64. Półmat z wyraźnym refleksem na krawędziach |
| Dłonie gracza | jak broń | Fasetowana bryła, czarna |
| Smuga pocisku | `Stopklatka/Transparent` | `#FF1E1E` α 0,8, gradient do 0 na końcu |
| Okna | `Unlit` | `#FFFFFF`, gradient alfa na krawędziach |

## 6. Przeciwnicy
- **Wysokość:** około 1,8 m. Proporcje atletyczne: szerokie barki (około 0,5 m), wąska talia, długie nogi.
- **Budowa:** low-poly z brył, każda ścianka płasko cieniowana.
  - Głowa wydłużona i wąska (około 0,18 × 0,3 m), wielościenna, bez oczu i ust.
  - Szyja, klatka piersiowa jako odwrócony klin, brzuch, miednica.
  - Ramiona i przedramiona, uda i łydki, dłonie jako bryłki.
- **Ruch:** proceduralny, dynamiczny. Bieg z szerokim krokiem, celowanie z wyprostowanej ręki, uderzenie z zamachu, poza obronna przy trafieniu w pobliżu.
- **Broń przeciwnika:** taka sama czarna jak broń gracza. Po śmierci wypada.

## 7. Broń i przedmioty

| Broń | Wygląd |
| --- | --- |
| Rewolwer i pistolet | Proste bryły (rękojeść pochylona, zamek, lufa jako graniastosłup, bębenek u rewolweru), około 25 cm |
| Strzelba | Długa, około 80 cm: kolba, komora, lufa, czółenko |
| Pistolet maszynowy | Krótki, około 35 cm, z długim magazynkiem w dół |
| Katana | Około 100 cm. Ostrze wąskie, prawie proste, czarne, ze stalowoszarą krawędzią. Okrągła garda, rękojeść z owijką (fasety) |

Przedmioty do rzucania: butelka, kubek, popielniczka i proste bryły (sześcian, klin). Wszystko czarne albo ciemnoszare, matowe.

## 8. Efekt rozpadu (najważniejszy efekt)
1. **Miejsce trafienia:** krótki jasny błysk w kolorze `#FFD84D`.
2. **Ciało pęka na kawałki:** duże bryły kończyn i tułowia odpadają, a z miejsca trafienia tryska 30–60 drobnych czerwonych odłamków (trójkąty i czworościany, 2–8 cm, dwa odcienie czerwieni).
3. **Przełomy i przekroje świecą** żółto-pomarańczowo (`#FFD84D` w środku, `#FF9A1F` przy brzegu).
4. **Cięcie kataną:** płaski świecący przekrój wzdłuż linii cięcia. Obie połowy odlatują osobno.
5. **Ciosy pięścią:** od punktu uderzenia rozchodzą się świecące pęknięcia.
6. **W zwolnionym czasie** odłamki wiszą w powietrzu. Po 3–5 s zmniejszają się i znikają.

Technika: gotowe kawałki w poolu, bez cięcia siatki w grze. Przekrój to gotowy płaski „kapelusz” emisyjny dołączony do kawałka.

## 9. Pomieszczenia
- **Wymiary:** każde mieści strefę gry gracza około 2×2 m (room-scale) plus przestrzeń, z której nadchodzą przeciwnicy (8–20 m).
- **Wspólne cechy:**
  - płyty 1×1 m na podłodze,
  - białe ściany,
  - jedno źródło światła i światło otoczenia,
  - mgła cyan w głębi,
  - kilka białych przeszkód na wysokości pasa do chowania się (kucanie, krok w bok).

| Pomieszczenie | Inspiracja | Zawartość |
| --- | --- | --- |
| **Ulica** | obraz 1 | Fasady z kolumnami po obu stronach, 2–3 białe samochody, latarnie, barierka. Przeciwnicy wychodzą zza samochodów i z bram |
| **Galeria** | obrazy 2 i 3 | Długi korytarz, postumenty z białymi posągami po jednej stronie, okna ze światłem po drugiej. Przeciwnicy nadbiegają z głębi i zza kolumn |
| **Hol z windą** | własny, w tym samym stylu | Biały hol biurowy z ladą recepcji, kolumnami i dwiema windami. Przeciwnicy wychodzą z wind i z bocznych drzwi |

## 10. Napisy i UI
- Na obrazach nie ma napisów.
- Zgodnie ze specyfikacją: tylko duże, proste napisy w przestrzeni (TextMeshPro w world space) na ścianach albo w powietrzu kilka metrów przed graczem.
  - Font bezszeryfowy, pogrubiony, biały z ciemnym cieniem albo czerwony.
  - Bez HUD-a przyklejonego do twarzy.
- **Hasła własne** (polskie), na przykład:
  - „STOP.” / „KLATKA.” na koniec poziomu,
  - „TRAFIONY” przy śmierci.
- **Menu:** czarne przedmioty do chwycenia, nie przyciski.

## 11. Lista porównawcza (po każdym etapie)
- [ ] Na każdym ujęciu są tylko biel, czerwień, czerń, żółto-pomarańczowe świecenie i czerwone smugi.
- [ ] Cienie są jasne i chłodne, nigdzie nie ma czarnych plam.
- [ ] Mgła cyan daje głębię w korytarzach.
- [ ] Przeciwnik: fasetowany, czerwony, bez twarzy, z wydłużoną głową, widoczne kreskowanie z bliska.
- [ ] Pocisk: mały czarny, długa cienka czerwona smuga, widoczna w zwolnionym czasie.
- [ ] Rozpad: drobne odłamki w dwóch odcieniach, świecące przełomy, przekrój po cięciu.
- [ ] Broń i dłonie: czarne z granatowym połyskiem na krawędziach.
- [ ] Pomieszczenia: płyty z fugą, białe bryły, okna lub otwory dające światło.
