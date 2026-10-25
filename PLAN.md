# PLAN: Stopklatka według GAME_SPEC.md

Ten dokument czytają też subagenci (`unity-dev`, `unity-tweaks`). Wszystko ważne jest tutaj albo w `GAME_SPEC.md` i `STYLE_GUIDE.md`.

## Decyzje właściciela (2026-10-25)

1. **Zostajemy przy mixed reality.** Gra toczy się w prawdziwych pomieszczeniach ze skanu Space Setup (tryby SZKOŁA i DOM), widzianych przez kamery.
   - Z `GAME_SPEC.md` bierzemy mechaniki, przeciwników, broń, efekty, menu z przedmiotów, konfigurację w ScriptableObject, AudioMixer i TextMeshPro.
   - Biały świat z obrazów daje filtr obrazu z kamer (styl SUPERHOT) i białe wirtualne rekwizyty.
2. **Płynność: domyślnie 90 Hz, 120 Hz jako opcja w configu.**
3. **Praca w chmurze bez edytora Unity.** Każdy etap kończy się kompilacją testową (prawdziwe biblioteki Unity 6000.0.85f1 i zaślepki SDK) oraz raportem. Testy w edytorze i na goglach zrobi później Claude Code na komputerze właściciela, po kroku 3 instalacji (`CLAUDE.md`).

## Rozpoznanie projektu (etap 1)

| Pytanie | Odpowiedź |
| --- | --- |
| Wersja Unity | Unity 6 LTS: MRUK 207 wymaga 6000.0.66f2 lub nowszej. Kompilacja testowa na 6000.0.85f1. Projekt nie był jeszcze otwarty, więc nie ma `ProjectSettings/` ani `Packages/`; tworzy je krok 1 z `CLAUDE.md` |
| Render pipeline | URP (builder ustawia MSAA 4×, bez HDR, bez cieni, SRP Batcher) |
| SDK do VR | Meta XR Core SDK 207.0.1 (`OVRCameraRig`, `OVRInput`, `OVRManager`, `OVRPassthroughLayer`, `EnvironmentDepthManager`) i MR Utility Kit 207.0.1 (`MRUK`, `EffectMesh`, `SceneNavigation`). **Bez** Interaction SDK i bez XR Interaction Toolkit: interakcje są własne (`ControllerHand`). Tak zostaje, żeby nie mieszać systemów |
| Wtyczka XR | OpenXR z grupą funkcji Meta Quest, tryb Multi-view (konfiguruje Simplified VR Setup i Project Setup Tool) |
| Wejście | Tylko kontrolery Touch (`HandTrackingSupport.ControllersOnly`). Hand tracking nie jest obsługiwany, więc zgodnie ze specyfikacją go nie dodajemy |
| Lokomocja | Brak sztucznej. Chodzisz naprawdę po zeskanowanych pomieszczeniach, co jest zgodne z „bez sztucznej lokomocji” |
| Scena | Jedna: `Assets/Stopklatka/Scenes/Stopklatka.unity`, budowana menu **Stopklatka → Build Game** (bez ręcznej edycji YAML, tak jak chce specyfikacja) |
| Struktura | `Assets/Stopklatka/{Scripts/{Core,World,Player,Weapons,Enemies,UI,Visuals,Audio},Shaders,Editor,Resources/Materials}` |

**Co już jest w kodzie** (napisane i skompilowane testowo, nieuruchomione):

| System | Co robi |
| --- | --- |
| Czas | `TimeController`: skala czasu z ruchu głowy i kontrolerów, `Kick()`, skalowany `fixedDeltaTime`, tryby Normal, MotionDriven, Fixed, Paused |
| Rig | `PlayerRig`: hitboxy głowy i tułowia od headsetu, wysokość podłogi z MRUK |
| Chwytanie i rzut | `ControllerHand`: grip, trzymanie w czasie rzeczywistym (`OVRCameraRig.UpdatedAnchors`), rzut z historii prędkości (100 ms, czas niezależny od spowolnienia) |
| Broń | `Gun`: pistolet (4 naboje) i strzelba (2 strzały, 6 śrucin), bez przeładowania. `Katana`: cięcie przeciwników i odbijanie pocisków. `Throwable`: butelka (tłucze się i ogłusza) |
| Pociski | `Bullet` i `BulletSystem`: widoczne pociski w poolu, raycast między klatkami, czerwona albo ciemna smuga |
| Przeciwnicy | `Enemy` i `EnemyBody`: NavMeshAgent, celowanie z opóźnieniem, strzał z rozrzutem, bieg i cios wręcz, ogłuszenie, proceduralny fasetowany model, pojawianie się od podłogi w górę |
| Rozpad | `ShardSystem`: pool odłamków (do 320), dwa odcienie czerwieni, błysk przy trafieniu |
| Przebieg gry | `GameManager`: menu z kryształów, fale, śmierć z restartem fali, pauza poza skanem |
| Tryby | `GameMode`: SZKOŁA i DOM. `PassthroughStyle`: NORMALNY, BIAŁY, SUPERHOT |
| Dźwięk | `SoundBank`: dźwięki generowane w kodzie, pitch zależny od czasu, szum tła |
| HUD | `Hud`: legacy TextMesh w przestrzeni, cień pod tekstem, winieta przy trafieniu |
| Wydajność | `PerformanceManager`: 120 Hz, foveation, poziomy CPU i GPU, odczyt FPS |

## Specyfikacja przełożona na mixed reality

| Punkt `GAME_SPEC.md` | W Stopklatce (MR) |
| --- | --- |
| Białe pomieszczenia, wypalone lightmapy | Prawdziwy pokój przez kamery z filtrem SUPERHOT (domyślnym od teraz), z paletą ze `STYLE_GUIDE.md`: cienie `#B9C6CC`, średnie `#E6EBED`, jasne `#F2F4F5`→biel. Lightmap nie ma, bo światło jest prawdziwe |
| 3 poziomy w różnych pomieszczeniach | 3 **scenariusze** w zeskanowanej przestrzeni. Każdy ma własny zestaw fal (typy przeciwników i broni, 30–90 s) i własne białe wirtualne rekwizyty. **Od etapu 6 (bezpieczeństwo):** rekwizyty wolnostojące tylko w obrysie zeskanowanych mebli, płaskie rekwizyty tylko na wolnych fragmentach ścian, nic na wolnej podłodze. 1. Galeria (postumenty z białymi posągami, półkolumny, światło z okien), 2. Ulica (auta, latarnie, barierki, gzymsy), 3. Hol (lada, kolumny, ramy „wind” wokół zeskanowanych drzwi, z których wychodzą przeciwnicy) |
| Room-scale 2×2 m, bez lokomocji | Chodzenie po skanie jak dotąd, bez sztucznej lokomocji. Przeciwnicy pojawiają się w odległości ze `ModeProfile` (SZKOŁA od 2,5 m, DOM od 1,8 m) |
| Wejścia drzwiami, zza rogu, z windy | Drzwi z MRUK (otwory `DOOR_FRAME`) jak dotąd, plus wirtualna „winda” w poziomie 3 |
| Mgła cyan | Nie da się zamglić prawdziwego świata. Zastępujemy chłodnym odcieniem w gradiencie filtra SUPERHOT i lekkim zanikaniem bardzo dalekich wirtualnych obiektów |
| Śmierć: błysk lub wygaszenie, napis, restart po 1,5 s | Jak dotąd (winieta, napis), restart **poziomu** po 1,5 s |
| Docelowo 90 FPS | 90 Hz domyślnie, 120 w configu (`GameConfig.targetRefreshRate`) |

## Co zostaje, co zmieniam, co dodaję

**Zostaje bez zmian:**
- rig i passthrough,
- skan MRUK, NavMesh i drzwi,
- tryby SZKOŁA i DOM,
- `ControllerHand` jako jedyny system interakcji,
- shadery (rozszerzane, nie wymieniane),
- builder jako jedyna droga do scen i materiałów.

**Zmieniam:**
- `Tuning` i `ModeProfile` → wartości w **ScriptableObject** `GameConfig` (z sekcjami czasu, walki, broni, przeciwników, trybów i wydajności). Builder tworzy asset z wartościami domyślnymi. Statyczne `Tuning.X` czytają z assetu, więc reszta kodu prawie się nie zmienia.
- `TimeController` (pełni rolę `TimeManager` ze specyfikacji):
  - wagi ruchu (przesunięcie 1,0, obrót 0,3),
  - `SmoothDamp` (szybkie przyspieszanie, wolniejsze zwalnianie),
  - impuls 0,2 s przy strzale, rzucie i chwycie,
  - `fixedDeltaTime = 1/odświeżanie × timeScale`.
- `PerformanceManager`: 90 Hz domyślnie, 120 z configu.
- Menu: zamiast kryształów do strzelania **czarne sześciany do chwycenia** (START, POZIOM 1/2/3, STYL, TRYB, WYJŚCIE).
- HUD i napisy: **TextMeshPro** w przestrzeni. Builder importuje TMP Essentials. Polskie znaki dzięki dynamicznemu atlasowi.
- `SoundBank`: pitch przez **AudioMixer** z wystawionym parametrem. Builder spróbuje utworzyć mixer. Jeśli Unity na to nie pozwoli z kodu, zostaje obecny pitch na źródłach i instrukcja ręcznego utworzenia mixera w 6 krokach.
- `GameManager`: fale grupowane w **poziomy** (`LevelManager`). Śmierć restartuje poziom.

**Dodaję:**
- pistolet maszynowy (serie, bez przeładowania),
- klik i wibracja przy pustym magazynku,
- `killOnThrow`,
- kubki, popielniczki i proste bryły do rzucania,
- **pięść**: czarna fasetowana dłoń na pustym kontrolerze, cios powyżej progu prędkości zabija,
- **zestrzeliwanie pocisków pociskami**,
- łapanie broni w locie (sprawdzić i dopracować),
- **cięcie kataną na pół** ze świecącym przekrojem,
- świecące pęknięcia i przełomy,
- kreskowanie na przeciwnikach,
- białe rekwizyty poziomów,
- wskaźnik `timeScale` na nadgarstku (tryb debug),
- scena testowa czasu.

## Etapy

Po każdym etapie:
1. kompilacja testowa (0 błędów, 0 ostrzeżeń),
2. porównanie ze `STYLE_GUIDE.md` (lista z punktu 11),
3. aktualizacja tego pliku i `CLAUDE.md` (lista testów na goglach),
4. commit i push,
5. raport dla właściciela, potem czekamy na „dalej”.

| Etap | Kto | Zakres | Gotowe, gdy |
| --- | --- | --- | --- |
| 1 | główna sesja | Ten plan, `STYLE_GUIDE.md`, `GAME_SPEC.md`, subagenci | Pliki w repo |
| 2 | główna sesja | `GameConfig` (SO) + przeniesienie `Tuning`. `TimeController` według specyfikacji (wagi, SmoothDamp, impulsy, `fixedDeltaTime` od odświeżania). `PerformanceManager` 90/120. Wskaźnik na nadgarstku. Scena testowa **Stopklatka → Build Time Test Scene** (wieżyczka strzelająca wolnymi pociskami, wahadło, cele) | Kompilacja OK, lista testów czasu w `CLAUDE.md` |
| 3 | `unity-dev` | Pistolet maszynowy, pusty strzał (klik i wibracja), `killOnThrow`, nowe przedmioty do rzucania, pięść, zestrzeliwanie pocisków, łapanie w locie (impulsy czasu przy chwycie i rzucie są już z etapu 2) | Kryteria 2, 3 i 6 możliwe do sprawdzenia |
| 4 | `unity-dev` | Cięcie na pół, świecące przełomy i przekroje, `LevelManager` z restartem poziomu po 1,5 s, fale w poziomach | Kryteria 4 i 5 |
| 5 | główna sesja | Styl: gradient filtra SUPERHOT według palety, kreskowanie, kolory i materiały, dłonie i broń. Dźwięk: AudioMixer i ambient narastający z ruchem | Lista ze `STYLE_GUIDE.md` |
| 6 | `unity-dev` | 3 poziomy (rekwizyty i fale), menu z sześcianów, wybór poziomu, wyjście, przejścia i napisy w TMP | Pełna gra od menu do końca poziomu 3 |
| 7 | `unity-dev` | Optymalizacja: budżety niżej, pooling, ustawienia XR i buildu (raport z wartościami) | Budżety spełnione na papierze, lista pomiarów na gogle |

### Stan

- **Etap 1: gotowy.** Plan, `STYLE_GUIDE.md`, `GAME_SPEC.md`, subagenci.
- **Etap 2: gotowy (kompilacja testowa: 0 błędów, 0 ostrzeżeń; niesprawdzony w Unity i na goglach).**
  - `GameConfig` (`Scripts/Core/GameConfig.cs`, asset `Resources/GameConfig.asset` tworzy builder): sekcje `time`, `player` (klasa `HandSettings`, bo `PlayerSettings` zasłaniałoby `UnityEditor.PlayerSettings`), `combat`, `enemies`, `school`, `home`, `performance`, `debug`. `Tuning` i `GameMode.Profile` czytają z niego.
  - `TimeController`: ruch = max(głowa, dłonie, 0,3 × obrót głowy), krzywa odpowiedzi, `SmoothDamp` na czasie rzeczywistym (0,05 s w górę, 0,18 s w dół), impuls 0,2 s przy strzale, chwycie i szybkim rzucie, pomijanie skoków trackingu, `fixedDeltaTime = 1/odświeżanie × timeScale`.
  - `PerformanceManager`: 90 Hz domyślnie, 120 z configu, foveation z configu.
  - `PlayerHealth`: trafienia gracza jako zdarzenie (gra restartuje falę, scena testowa tylko liczy).
  - `TimeIndicator`: wskaźnik czasu nad lewym nadgarstkiem (`debug.wristTimeIndicator`, w scenie testowej zawsze).
  - `TimeTestScene` + **Stopklatka → Build Time Test Scene**: wieżyczka (pocisk co 2,5 s czasu gry), wahadło, przeciwnik, broń w zasięgu ręki. Przełączanie scen: **Stopklatka → Build Settings: Game Scene / Time Test Scene**.
- **Etap 3: gotowy (kompilacja testowa: 0 błędów, 0 ostrzeżeń; niesprawdzony w Unity i na goglach).**
  - `GameConfig`: nowe sekcje `weapons` (amunicja, odstępy i rozrzut pistoletu, strzelby i pistoletu maszynowego) i `haptics` (siła, długość i częstotliwość każdej wibracji). W `player`: łapanie w locie i pięść. W `combat`: zestrzeliwanie pocisków, cięcie pocisków kataną, `killOnThrow` i prędkości rzutów.
  - **Pistolet maszynowy** (`GunKind.Smg`, `Factory`): seria 3 strzałów na jedno naciśnięcie, 15 nabojów (5 serii), mały rozrzut, jeden impuls czasu na serię. Odstępy w serii liczone w czasie gry. Na razie tylko dla gracza i w znajdźkach (`SpawnPickups`); przeciwnicy z serią to etap 4.
  - **Pusta broń**: dźwięk kliku i dwa krótkie ostre impulsy wibracji przy każdym naciśnięciu spustu, także tuż po ostatnim strzale. Strzał to jedna długa, mocna wibracja, więc łatwo je odróżnić.
  - **`killOnThrow`** (domyślnie wyłączone): szybki rzut ogłusza przeciwnika i wytrąca mu broń. Po włączeniu: powyżej `throwKillSpeed` zabija, a butelki, kubki i popielniczki zabijają zawsze (tak było przed etapem 3). **Decyzja:** rzucona katana zabija niezależnie od flagi (`thrownBladeKills`, domyślnie włączone), bo to ostrze i tak działało wcześniej.
  - **Nowe przedmioty do rzucania** (`Throwable` z `ThrowableKind`): kubek i popielniczka (czarne, tłuką się na ciemne odłamki), sześcian i kula (czarne, odbijają się). Butelka zostaje biała, szklana. Wszystkie z materiałem `WeaponBlack` (bez nowego materiału, lepsze batchowanie).
  - **Pięść** (`Player/Fist.cs`): czarna fasetowana dłoń z mankietem na pustym kontrolerze, schowana, gdy coś trzymasz. Cios szybszy niż 2 m/s (prędkość w czasie rzeczywistym) rozbija przeciwnika. **Decyzja:** cios 1–2 m/s tylko ogłusza i wytrąca broń, wolniejszy dotyk nic nie robi (`punchStunSpeed` = 0 wyłącza ogłuszanie). Jedno `OverlapSphereNonAlloc`, tylko gdy ręka szybko się rusza. Pięść ustawiona kciukiem do góry, jak dłoń na uchwycie kontrolera (`Factory.FistPosition/FistEuler`, do dostrojenia na goglach).
  - **Zestrzeliwanie pocisków**: twój pocisk niszczy pocisk wroga, który mija go bliżej niż 5,5 cm. Liczone jako najbliższe zbliżenie dwóch punktów poruszających się w tej samej klatce, więc nie przeskakują przez siebie. Mały błysk i wysoki „brzdęk”. Limit 48 pocisków jest teraz pilnowany w `BulletSystem.Fire` (najpierw znika najstarszy pocisk gracza).
  - **Cięcie pocisków kataną**: w zwolnionym czasie pocisk prawie stoi, a ostrze przelatuje obok niego między klatkami, więc samo sprawdzanie promienia pocisku nie wystarczało. Teraz katana sprawdza cały obszar, który ostrze przemiotło od poprzedniej klatki.
  - **Łapanie w locie**: trzymaj grip pustą ręką, a lecący przedmiot zostanie złapany, gdy doleci do dłoni. Sprawdzana jest też droga przedmiotu od poprzedniej klatki, a szybkie przedmioty mają trochę większy promień chwytu. Broń wypadająca z ręki ogłuszonego przeciwnika przez chwilę (0,6 s czasu gry) nie zderza się z jego ciałem, więc wylatuje do góry i da się ją złapać, a potem można nią w niego rzucić.
  - **Wibracje**: strzał, chwyt, złapanie (mocniej), cios, cięcie, zablokowany pocisk. Trafienie gracza wibruje oba kontrolery w `PlayerHealth.TakeHit`, więc działa też w scenie testowej (wcześniej było tylko w `GameManager`).
  - **Podłoga bez skanu**: `RoomService` tworzy niewidzialną podłogę (warstwa Room) na wysokości `PlayerRig.FloorY`, gdy nie ma skanu, zgody albo MRUK. Scena testowa ma swoją `TestFloor` i nie ma `RoomService`, więc podłoga nigdy się nie dubluje.
  - **Scena testowa**: przed tobą łuk unoszących się przedmiotów (katana, strzelba, pistolet maszynowy, pistolet, butelka, kubek, popielniczka, sześcian). **Przycisk X** rzuca pistolet łukiem do prawej ręki (w czasie gry, więc w zwolnionym tempie łatwo go złapać). Pociski wieżyczki służą do zestrzeliwania i cięcia.
  - **Poprawka przy sprawdzaniu etapu:** builder używał `GetComponent<T>() ?? AddComponent<T>()` dla `OVRManager` i `MRUK`. W edytorze brakujący komponent to „fałszywy null”, którego `??` nie widzi, więc komponent nie zostałby dodany. Teraz `TryGetComponent`.
  - **Porównanie ze `STYLE_GUIDE.md`:** zgadza się czerń broni, przedmiotów i dłoni (`WeaponBlack`), biała szklana butelka, pistolet maszynowy około 35 cm z długim magazynkiem, dłoń jako fasetowana bryła. Odbiega: przedmioty używają półmatowego materiału broni (przewodnik mówi „matowe”), popielniczka nie ma wgłębienia (`MeshKit` buduje tylko wypukłe bryły), zamiast klina jest kula. Pęknięcia od pięści (punkt 8.5) to etap 4.
- **Etap 4: gotowy (kompilacja testowa: 0 błędów, 0 ostrzeżeń; niesprawdzony w Unity i na goglach).**
  - `GameConfig`: nowe sekcje `shatter` (odłamki, duże kawałki, błysk, cięcie kataną, pęknięcia od pięści) i `levels` (3 poziomy z falami, restart, przerwy). W `enemies`: udział pistoletów maszynowych, sygnał przed strzałem, serie wroga, zamach przed ciosem, poza obronna, punkty spawnu. Czytane przez `Tuning`.
  - **Wygląd przeciwnika** (`EnemyBody`, punkt 6): 1,8 m, szerokie spiczaste barki (0,49 m), wąska talia, długie nogi (biodro na 0,94 m), wydłużona wąska głowa bez twarzy (0,18 × 0,27 m plus szyja), klatka jako odwrócony klin, brzuch z miednicą, ramiona, przedramiona z dłońmi jak bryłki, uda, łydki ze stopami. 11 części + cień = 12 draw calli (jak budżet), wspólny materiał, SRP Batcher. Nowa metoda `MeshKit.Loft` (bryła przez pierścienie) do klatki, bioder i głowy. Każda część zna miejsce, w którym się odłamuje (pod świecący „kapelusz”).
  - **Rozpad** (`Shatter`, `Debris`, punkt 8): żółty błysk `#FFD84D` w miejscu trafienia; część trafiona i części bliżej niż 20 cm rozsypują się, z miejsca trafienia tryska 30–60 odłamków (czworościany, trójkątne płatki i bryłki, 2–8 cm, dwa odcienie czerwieni plus 15% świecących żółtych); pozostałe części odpadają jako duże kawałki z płaskim świecącym przełomem (`#FFD84D` w środku, `#FF9A1F` przy brzegu), odbijają się od podłogi, kładą się i po około 4 s czasu gry maleją i znikają. W zwolnionym czasie wszystko wisi w powietrzu.
  - **Decyzja (technika):** duże kawałki to kopie siatek części martwego przeciwnika w poolu (`DebrisSystem`, bez cięcia siatek), poruszane własnym prostym ruchem w czasie gry (grawitacja, odbicie od podłogi), bez `Rigidbody`. Tanie, ale przenikają przez prawdziwe ściany (lecą najwyżej około metra).
  - **Cięcie kataną na pół** (punkt 8.4): płaszczyzna cięcia = kierunek ostrza × kierunek zamachu. Każda część idzie w całości na jedną stronę (wg środka), więc cięcie zawsze wypada między częściami (szyja, pas, biodra, barki, kolana). Każda połowa dostaje płaski świecący przekrój tam, gdzie kończą się jej części, wielkości przekroju ciała na tej wysokości (rozciągnięty przy cięciu ukośnym, wysoki przy pionowym). Górna połowa odlatuje z zamachem i obraca się, nogi zostają; z linii cięcia sypie się wachlarz odłamków; po około 1,3 s czasu gry obie połowy rozpadają się na odłamki. Pchnięcie bez zamachu rozbija zwykłym rozpadem.
  - **Pęknięcia od pięści** (punkt 8.5): cios zabijający pokazuje jasną plamę i 6 świecących pęknięć (co drugie z odgałęzieniem), które rosną od kłykci po trafionej części przez 0,25 s czasu gry; przeciwnik drży, potem się rozpada. Paski w poolu, podążają za częścią ciała.
  - **Nowe materiały** (tworzy builder): `FractureGlow` (shader `Stopklatka/Transparent`, biały; kolory żółty→pomarańczowy w kolorach wierzchołków, wartości liniowe) do przełomów, przekrojów, pęknięć i błysku; `ShardGlow` (`Stopklatka/Crystal`, `#FFD84D`/`#FF9A1F`, emisja 0,45) do świecących odłamków. **Bez zmian w shaderach.**
  - **Przeciwnicy z pistoletem maszynowym**: serie po 3 strzały co 0,13 s czasu gry, wszystkie w miejsce, gdzie była twoja głowa na początku serii, z rozrzutem; da się uniknąć krokiem. W poziomach od poziomu 2, w trybie bez końca od fali 4.
  - **Zachowanie** (GAME_SPEC 4): ręka z bronią unosi się widocznie przez czas reakcji, potem przed każdym strzałem **sygnał** (0,35 s: staje, ręka nieruchomieje, błysk na lufie i klik), dopiero potem strzał. Bez broni: podchodzi (prędkości z trybu, bez biegania), widoczny zamach ręką do tyłu, cios. **Poza obronna**: twój pocisk przelatujący bliżej niż 0,6 m albo śmierć sąsiada w promieniu 3 m: ręce skrzyżowane przed twarzą na 0,7 s i utrata celu.
  - **Spawn** (`RoomService`): często tuż za ramą drzwi (`DOOR_FRAME` z MRUK) na twoim piętrze, „wchodzą drzwiami”; inaczej czasem poza twoim polem widzenia (≥ 65° od kierunku patrzenia). Zawsze w zeskanowanym pokoju, z tymi samymi minimalnymi odległościami (2,5 m szkoła, 1,8 m dom, najmniej 1,2 m) i połączeniem NavMesh z graczem. Sprawdzane obie strony ramy, więc kierunek normalnej ramy nie ma znaczenia.
  - **`LevelManager`** (nowy): 3 poziomy z `GameConfig.levels` (GALERIA 12, ULICA 15, HOL 18 przeciwników w 3 falach, do 2–6 naraz, rosnący udział broni i trudność). Start: „POZIOM n” i nazwa, znajdźki; fale „FALA 2/3”; koniec: „STOP.” / „KLATKA.” ×2, „POZIOM n ZALICZONY”, następny poziom; po 3: „KONIEC” i menu z napisem. **Śmierć:** czerwona winieta, „TRAFIONY”, świat prawie staje, kamera się nie rusza, po 1,5 s (`levels.restartDelay`) poziom od pierwszej fali. Pauza poza skanem wstrzymuje też odliczanie. `GameManager` zostaje przy starcie, skanie, menu i pauzie. **Stare fale bez końca** zostają: `levels.useLevels` = wyłączone.
  - **Scena testowa:** **przycisk Y** zmienia broń przeciwnika (pistolet → maszynowy → strzelba → bez broni) i od razu go przywraca; licznik `zabici`.
  - **Poprawka przy okazji:** `RoomService` używał `MRUKRoom.FloorAnchor`, w MRUK 207 oznaczonego jako przestarzały (ostrzeżenie CS0618 w Unity). Teraz `FloorAnchors[0]`.
  - **Wydajność:** pool odłamków, kawałków, pęknięć i błysków tworzony w trakcie wczytywania skanu (`Shatter.Prewarm`), żeby pierwsze zabicie nie tworzyło kilkudziesięciu obiektów naraz. Jedno zabicie: do ~20 kawałków i przełomów oraz do 60 odłamków (limity: `shatter.maxPieces` 72, odłamki 320).
  - **Porównanie ze `STYLE_GUIDE.md`:** zgadza się sylwetka (szerokie barki, wąska talia, długie nogi, długa wąska głowa bez twarzy, dłonie jak bryłki), żółty błysk, odłamki w dwóch odcieniach czerwieni (trójkąty i czworościany), świecące przełomy na dużych kawałkach, płaski świecący przekrój po cięciu i osobno odlatujące połowy, wachlarz odłamków z linii cięcia, pęknięcia od pięści, odłamki wiszące w zwolnionym czasie i znikające po 3–5 s, poza obronna z rękami przed twarzą, celowanie z wyprostowanej ręki. Odbiega: brak kreskowania (etap 5, shader); cięcie zawsze między częściami, nie dokładnie wzdłuż linii ostrza; przełom to jednolity dysk, nie dokładny kształt przekroju; kawałki przenikają przez prawdziwe ściany; przeciwnicy nie biegają (świadomie, bezpieczeństwo w prawdziwym pokoju), więc nie ma „biegu z szerokim krokiem”; pozy (osłona, zamach) i kolory są ustawione na oko, bez gogli.

- **Etap 5: gotowy (kompilacja testowa: 0 błędów, 0 ostrzeżeń; shader i mixer niesprawdzone w Unity i na goglach).** Główna sesja.
  - **Materiały według palety** (`StopklatkaBuilder.CreateMaterials`, kolory jako HEX z `STYLE_GUIDE.md` §3 i §5; te same w `Mats.CreateFallback`):
    - przeciwnik `#FF2B1C` / `#9E0A08`, rim `#FF6A5A` 0,6, połysk 0,5, gloss 40; ciemniejsze odłamki `#9E0A08` / `#6A0505`;
    - broń, przedmioty i pięść `#1E3242` / `#0B141A`, rim `#3A5A6E` 0,5, chłodny refleks `#8FB0C4`, gloss 64;
    - katana: czarne ostrze `#1A2630` ze stalowoszarą krawędzią (mocny rim `#AEB8BF`), wcześniej była jasnostalowa;
    - **butelka z ciemnego szkła** (nowy materiał `GlassDark`), bo wszystko, czego używasz, jest czarne (punkty 1 i 7); białe kryształy menu zostają na `GlassWhite`;
    - smuga wroga `#FF1E1E` α 0,8, twoja `#0B141A`; **błysk wystrzału i sygnał przed strzałem wroga są białe**: żółto-pomarańczowy akcent jest tylko tam, gdzie przeciwnik pęka (punkt 1);
    - cień pod przeciwnikiem chłodny szaroniebieski `#5E6E78` α 0,38 (na białej podłodze wychodzi około `#B9C6CC`, nigdy czarny);
    - nowe na etap 6: `StatueWhite` (`#ECEFF0` / `#D9E0E3`) i `PropWhite` (`#F2F4F5` / `#D3DCE0`) do białych rekwizytów.
  - **Kreskowanie** (`Crystal.shader`): cienkie ukośne paski pod 45° w przestrzeni obiektu, 60 na metr, `#B80E0A` przy 25%, tylko na materiałach przeciwnika (`_HatchStrength`). Wygładzane przez `fwidth` i wygaszane, gdy pasek robi się węższy niż około 1,5 piksela, więc widać je z bliska (do około 3 m), a z daleka nie migoczą. Wartość materiału zamiast słowa kluczowego: zero nowych wariantów, SRP Batcher bez zmian. **Jedyna zmiana shadera w tym etapie.**
  - **Smuga pocisku** (punkt 2, obraz 1): prawie stała grubość około 1 cm, pełna przez 60% długości i zanikająca na końcu; wroga do 3,5 m (`visuals.enemyStreakLength`, przewodnik: 3–6 m), twoja do 1 m.
  - **Filtr SUPERHOT** (`PassthroughStyle`): obraz z kamer odwzorowany na paletę: `#7E8E96` → `#B9C6CC` → `#E6EBED` → `#F2F4F5` → `#FFFFFF`. **Decyzja:** najciemniejszy próg jest ciemniejszy niż cień z przewodnika (`#B9C6CC`), żeby krawędzie prawdziwych mebli, schodów i drzwi były dobrze widoczne (bezpieczeństwo).
  - **Dźwięk** (GAME_SPEC 7):
    - **AudioMixer**: builder (**Stopklatka → 5. Create Audio Mixer**, też w Build Game) tworzy `Resources/Audio/StopklatkaMixer.mixer` z grupami World, UI i Ambient oraz wystawionymi parametrami `WorldPitch` i `AmbientVolume`. Unity nie ma do tego publicznego API, więc builder woła przez refleksję wewnętrzne metody edytora, te same co okno Audio Mixer (sprawdzone na DLL edytora 6000.0.85). Jeśli to się nie uda, w logu i w `CLAUDE.md` jest instrukcja ręczna w 6 krokach, a gra do tego czasu ustawia pitch na każdym dźwięku.
    - Dźwięki świata idą przez grupę World i zwalniają z czasem (pitch 35–100%, `sound.slowPitch` / `fullPitch`), dźwięki UI przez UI i nigdy nie zwalniają. Zachowana losowa wariacja wysokości każdego dźwięku.
    - **Ambient** (dwa zapętlone dźwięki syntetyzowane przy starcie, płynne pętle): niski szum, gdy czas stoi (cichnie, gdy czas przyspiesza), i cichy szum powietrza narastający z ruchem. Oba tylko w trakcie gry, cisza w menu i w pauzie. Zastępują dawny „drone”, który rósł z czasem.
    - Dźwięki świata nadal pozycyjne 3D.
  - **Porównanie ze `STYLE_GUIDE.md` (lista z punktu 11):**
    - [x] Tylko biel, czerwień, czerń, żółto-pomarańczowe świecenie (tylko przy pękaniu) i czerwone smugi. Białe błyski wystrzału.
    - [x] Cienie jasne i chłodne, bez czarnych plam (cień pod przeciwnikiem, filtr SUPERHOT).
    - [ ] Mgła cyan w korytarzach: w MR nie ma mgły na prawdziwym obrazie; zostaje do etapu 6 jako lekka mgła na wirtualnych rekwizytach.
    - [x] Przeciwnik fasetowany, czerwony, bez twarzy, z wydłużoną głową i kreskowaniem z bliska.
    - [x] Pocisk: mały ciemny, długa cienka czerwona smuga, widoczna w zwolnionym czasie. (Korpus pocisku używa materiału broni `#1E3242` zamiast `#121212`, różnica niewidoczna.)
    - [x] Rozpad: drobne odłamki w dwóch odcieniach, świecące przełomy, przekrój po cięciu (etap 4).
    - [x] Broń i dłonie czarne z granatowym połyskiem na krawędziach.
    - [ ] Pomieszczenia: płyty, białe bryły, okna. Prawdziwy pokój plus filtr; białe rekwizyty to etap 6.

- **Etap 6: gotowy (kompilacja testowa: 0 błędów, 0 ostrzeżeń; niesprawdzony w Unity i na goglach).** `unity-dev`.
  - `GameConfig`: nowe sekcje `props` (rekwizyty: włącznik, liczby, odległości bezpieczeństwa, kształty, czasy pojawiania się i znikania, drzwi w HOL, mgiełka), `menu` (sześciany albo dawne kryształy, rozmiary, łuk, potwierdzenie wyjścia), `ui` (TextMeshPro albo dawny TextMesh, czcionka, cień, rozmiary, gdzie stają napisy). W `levels.list`: `theme` (GALERIA / ULICA / HOL / None / Auto) i `doorSpawnChance` dla poziomu. W `debug`: `editorSampleRoom`. Czytane przez `Tuning.Props / Menu / Ui`.
  - **Menu z sześcianów** (GAME_SPEC 8): czarne fasetowane sześciany (13 cm, START 16 cm, materiał broni) na łuku w zasięgu ręki: górny rząd POZIOM 1 / 2 / 3 (z nazwą, zaliczone mają „ZALICZONY”), dolny STYL, START, TRYB i dalej w prawo WYJŚCIE. Chwyt gripem (`ControllerHand` pyta `MenuTarget.FindGrabbable`, gdy w zasięgu nie ma przedmiotu) rozbija sześcian na czarne odłamki i wykonuje akcję; szybki cios, pocisk i katana też działają, sam dotyk nie. STYL i TRYB wracają od razu z nową wartością. **WYJŚCIE** pyta o drugi chwyt w ciągu 4 s („WYJŚCIE? CHWYĆ JESZCZE RAZ”), potem `Application.Quit()`. Postęp (`Core/Progress.cs`, `PlayerPrefs`, bit na poziom): START zaczyna pierwszy niezaliczony poziom. **Decyzja:** jeśli blisko stoi ściana albo szafa, łuk obraca się w stronę wolnego miejsca (raycast po skanie), żeby sześciany nigdy nie wisiały w prawdziwej ścianie. Dawne kryształy zostają za flagą `menu.useCubes`.
  - **Rekwizyty poziomów** (`World/LevelProps.cs` motywy i przejścia, `World/PropPlanner.cs` geometria i bezpieczeństwo, `Visuals/PropKit.cs` kształty): GALERIA: białe bloki dokładnie na meblach z małymi posągami (sylwetka przeciwnika w 3 pozach, jedna siatka, `EnemyBody.StatueMesh`), półkolumny i miękkie białe panele „światła z okna” na ścianach z jasnym prostokątem na podłodze; ULICA: auta (z kilku brył) na kanapach, łóżkach i dużych meblach, bloki na reszcie, latarnie i barierki z prętów płasko przy ścianach, gzyms wysoko; HOL: lada recepcji na największym stole, bloki, półkolumny, ramy wind wokół zeskanowanych drzwi i częstsze wejścia drzwiami (0,9). Każdy rekwizyt to 1 draw call (posąg z postumentem 2), razem do 14 rekwizytów, kolizja na warstwie Room (zatrzymują pociski i wzrok przeciwników jak meble; panele światła bez kolizji).
  - **Bezpieczeństwo MR (decyzje):** wolnostojące tylko w obrysie mebli z MRUK (TABLE, COUCH, STORAGE, BED, OTHER) i nigdy szersze (blok 1 cm w środku obrysu, auto i posąg skalowane, żeby przy każdym obrocie mieściły się w obrysie); na ścianach tylko rzeczy do 18 cm głębokości na odcinkach bez ram drzwi, okien, innych kotwic ściennych (+20 cm zapasu) i bez mebli stojących przy ścianie (do 45 cm); drzwi blokują całą wysokość ściany, więc nic nie idzie nad ani przez otwór, a rama windy stoi obok otworu; nic na wolnej podłodze (jasny prostokąt pod oknem to płaska przezroczysta „naklejka” bez kolizji, tylko na wolnej podłodze w pokoju); nic bliżej niż 1,5 m od miejsca, w którym stoisz na starcie poziomu; tylko pokoje na twoim piętrze (±0,6 m), nic wyżej niż 2,3 m. Osie mebli i ścian liczone z transformów kotwic, więc nie zależą od konwencji osi MRUK. Rekwizyty znikają w pauzie i w menu; poziom gra tak samo bez rekwizytów (brak skanu, pusty pokój, `props.enabled` wyłączone).
  - **Przejścia** (bez ruchu kamery i bez wygaszania do czerni): start poziomu: rekwizyty wyrastają z podłogi ze świecącą krawędzią jak przeciwnicy (kopie materiałów z wariantem `_REVEAL`, 1,2 s), „POZIOM n” i nazwa; koniec: „STOP.” / „KLATKA.”, „POZIOM n ZALICZONY”, rekwizyty zapadają się w podłogę (0,9 s), następny poziom; po ostatnim „KONIEC” i menu (z listą niezaliczonych, jeśli zaczynałeś od 2 lub 3). Restart po śmierci zostawia stojące rekwizyty.
  - **Napisy** (STYLE_GUIDE 10): `UI/WorldText.cs`: TextMeshPro (LiberationSans SDF z TMP Essentials i jej dynamiczny fallback z polskimi znakami), pogrubione, białe albo czerwone, z miękkim ciemnym cieniem (underlay) i cienką ciemną obwódką, na kopii materiału „Drop Shadow” z Essentials (te warianty shadera są w buildzie). Zapas: dawny TextMesh z cieniem, gdy `ui.useTextMeshPro` wyłączone albo brak czcionki. Builder importuje TMP Essentials (**Stopklatka → 6. Import TextMeshPro Essentials**, też w Build Game). **Tytuły nie są przyklejone do twarzy:** nowy tytuł staje raz w stałym miejscu: na prawdziwej ścianie przed tobą (1,5–6 m), inaczej 2,5 m przed tobą, trochę nad oczami, rozmiar zależny od odległości; tylko obraca się do ciebie i przenosi się przed ciebie dopiero, gdy patrzysz gdzie indziej dłużej niż 1,2 s. Odczyt FPS (debug) dalej podąża za głową.
  - **Mgiełka** (STYLE_GUIDE 4): `Crystal.shader` dostał `_FogColor`, `_FogStart`, `_FogEnd`, `_FogStrength` (wartości materiału w tym samym CBUFFER, bez słowa kluczowego, zero nowych wariantów); dalekie rekwizyty (od 3 m, najmocniej 15 m) przechodzą w `#DDEFF4`. Tylko `StatueWhite` i `PropWhite` mają siłę > 0. **Jedyna zmiana shadera w tym etapie.**
  - **Nowe materiały** (builder, kolory z przewodnika): `WindowLight` (`#FFFFFF` α 0,85) i `FloorLight` (`#FFFFFF` α 0,35, bias okluzji 0,08), oba `Stopklatka/Transparent` z alfą w kolorach wierzchołków; `StatueWhite` i `PropWhite` z mgiełką `#DDEFF4`.
  - **Test w edytorze bez skanu:** tylko w edytorze (`#if UNITY_EDITOR`, `AssetDatabase`), gdy urządzenie nie daje pokoi, `RoomService` wczytuje przykładowy pokój MRUK (`debug.editorSampleRoom`, domyślnie `LivingRoom00`) przez `MRUK.LoadSceneFromPrefab`. W buildzie nigdy.
  - **Porównanie ze `STYLE_GUIDE.md` (lista z punktu 11):**
    - [x] Tylko biel, czerwień, czerń, żółto-pomarańczowe świecenie i czerwone smugi: rekwizyty są białe (`#F2F4F5` / `#D3DCE0`, posągi `#ECEFF0` / `#D9E0E3`), menu czarne, napisy białe lub czerwone z ciemnym cieniem.
    - [x] Cienie jasne i chłodne: rekwizyty mają chłodny cień `#D3DCE0`, bez czarnych plam.
    - [~] Mgła cyan: jest jako mgiełka na dalekich wirtualnych rekwizytach; prawdziwego pokoju nie da się zamglić (passthrough), a w małych pokojach rekwizyty są blisko, więc efekt jest słaby.
    - [x] Przeciwnik i pocisk: bez zmian (etapy 4–5).
    - [x] Rozpad: bez zmian; sześciany menu też pękają na czarne odłamki.
    - [x] Broń i dłonie: bez zmian; sześciany menu w tym samym czarnym materiale.
    - [~] Pomieszczenia: białe bryły (postumenty, posągi, auta, lada, kolumny, latarnie, barierki, gzymsy, ramy wind) i światło z okien są, ale tylko tam, gdzie pozwala skan (meble i wolne ściany), więc układ zależy od twojego pokoju. Brak płyt z fugą na podłodze (świadomie: wirtualna podłoga zasłoniłaby prawdziwe przeszkody) i brak prawdziwych otworów okiennych.
  - **Odbiega / do sprawdzenia na goglach:** rozmiary napisów TMP (font size 1 = 0,1 m) i sześcianów to szacunki; posągi i auta są małe na małych meblach; rekwizyty na meblach zasłaniają to, co leży na meblu albo pod stołem (w obrysie, przez który i tak nie przejdziesz); ramy wind i półkolumny nie są w NavMesh (do 18 cm od ściany, agent i tak trzyma 25 cm).

**Budżety wydajności (90 Hz, czyli 11,1 ms na klatkę):**
- do 150 draw calli,
- do 150 tys. trójkątów w kadrze,
- do 6 przeciwników naraz po około 12 draw calli każdy (SRP Batcher),
- odłamki do 320, pociski do 48,
- jedno światło (brak cieni czasu rzeczywistego, cień pod przeciwnikiem to krążek),
- passthrough i Depth API wliczone w koszt.

## Ograniczenia i ryzyka
- **Bez Unity w chmurze.** Shadery, sceny z buildera, AudioMixer i import TMP sprawdzi dopiero Unity na komputerze właściciela. Lista kontrolna jest w `CLAUDE.md`.
- **Subagenci.** Pliki `.claude/agents/unity-dev.md` i `unity-tweaks.md` są w repo, ale ta sesja nie ładuje nowych agentów w trakcie pracy. Jeśli przy etapie 3 nie będą widoczni, zlecam zwykłemu subagentowi z tymi samymi instrukcjami i ustawionym effort (`high` albo `medium`). Narzędzie do subagentów pozwala ustawić effort.
- **Obrazy referencyjne** to zrzuty z cudzej gry. Leżą lokalnie w `References/` i są w `.gitignore`, więc nie trafiają do publicznego repo. Właściciel ma je u siebie.
- **AudioMixer** nie ma oficjalnego API do tworzenia z kodu (patrz „Zmieniam”).
