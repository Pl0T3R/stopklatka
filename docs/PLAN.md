# Plan: „Stopklatka”, gra mixed reality w stylu Superhot na Quest 3 (Unity)

## Kontekst

Chcesz grę jak Superhot, w której chodzisz po prawdziwej szkole zeskanowanej w Space Setup. Widzisz szkołę przez kamery gogli, a przeciwnicy pojawiają się w zeskanowanych miejscach. Twoje wymagania i wybory:
- sterowanie kontrolerami,
- 120 klatek na sekundę,
- rozdzielczość około 2024 px na oko,
- dobry wygląd,
- gra ma po prostu działać,
- bronie: pistolet, strzelba, katana i rzucanie przedmiotami,
- platforma: Unity (aplikacja APK) na komputerze z Windows,
- tryb dewelopera włączasz Ty (masz 18+),
- jeśli wydajności zabraknie, ważniejsze jest 120 klatek niż rozdzielczość,
- poprawki robi Claude Code uruchomiony na Twoim komputerze.

Robocza nazwa „Stopklatka” omija nazwę Superhot, bo to cudzy znak towarowy.

## Fakty sprawdzone w dokumentacji Meta (przed pisaniem kodu)

- **120 Hz:** Quest 3 ma je w aplikacjach natywnych. Ustawia się je przez `OVRPlugin.systemDisplayFrequency = 120f`, a dostępne wartości odczytuje się z `OVRManager.display.displayFrequenciesAvailable`.
- **Rozdzielczość:**
  - ekran Quest 3: 2064×2208 na oko,
  - render scale 1.0 to 1680×1760,
  - render scale około 1.2 odpowiada natywnej rozdzielczości.
  
  Cel to `eyeTextureResolutionScale ≈ 1.205`, czyli około 2024×2120 na oko. Ekran oka nie jest kwadratowy, więc 2024 dotyczy szerokości. Dynamiczna rozdzielczość zejdzie do około 1.0, gdy trzeba utrzymać 120 klatek.
- **Chodzenie po szkole:** wpis `com.oculus.feature.BOUNDARYLESS_APP` w manifeście wyłącza granicę (guardian). Warunek: cała aplikacja musi być w mixed reality, bez części VR. Pozycje rzeczy w świecie trzyma world locking z MRUK, a nie przestrzeń Stage.
- **Zeskanowana szkoła:** MRUK (Mixed Reality Utility Kit) wczytuje pomieszczenia ze Space Setup. Komponent `SceneNavigation` buduje z nich NavMesh: podłoga to obszar do chodzenia, meble to przeszkody.
- **Przesłanianie przez prawdziwe rzeczy (Depth API):** wybieram tańszą wersję „hard occlusion”. Dzięki niej prawdziwi ludzie, ściany i meble zasłaniają przeciwników.
- **Tryb dewelopera:** wymaga osoby pełnoletniej, zweryfikowanego konta dewelopera Meta i gogli sparowanych z jej telefonem w aplikacji Meta Horizon.
- **Wersje:** Meta XR SDK `com.meta.xr.sdk.core` i `com.meta.xr.mrutilitykit` w wersji 207.0.1 to najnowsze w rejestrze `npm.developer.oculus.com`, który jest stąd osiągalny. Zalecane Unity 6 LTS.
- **Bezpieczeństwo:** Meta odradza gry, które zmuszają do szybkiego cofania się, uników albo biegania, oraz stawianie wirtualnych postaci za prawdziwą ścianą. Mechanika Superhot, w której czas płynie tylko wtedy, gdy się ruszasz, nagradza właśnie powolny ruch, więc dobrze do tego pasuje.

## Podział pracy

### Etap A: tutaj, w chmurze, od razu po akceptacji
1. Prośba do Ciebie o założenie pustego, publicznego repo `Pl0T3R/stopklatka` (tak jak przy Brickfall). Potem podłączam je do sesji przez `add_repo`.
2. Pobieram paczki SDK 207.0.1 (core i mrutilitykit) z rejestru Meta i sprawdzam w ich kodzie każde API, którego użyję. Dzięki temu nie zgaduję nazw klas ani metod.
3. Piszę i wypycham do repo:
   - **`CLAUDE.md`:** instrukcje dla Claude Code na Twoim komputerze: narzędzia metavr MCP (`metavr_unity_setup`, device, app, perf), kolejność kroków, lista kontrolna sprawdzania.
   - **`docs/PLAN.md`:** ten plan, żeby sesja na komputerze miała pełny kontekst.
   - **`docs/USTAWIENIA.md`:** instrukcja Etapu B po polsku, krok po kroku.
   - **`Assets/Stopklatka/Scripts/`**, kod gry:
     - `Core/TimeController.cs`: tempo czasu zależne od ruchu,
     - `Core/GameManager.cs`: stany gry i fale,
     - `Core/PerformanceManager.cs`: 120 Hz, render scale, dynamiczna rozdzielczość, foveation, licznik FPS w trybie debug,
     - `World/RoomService.cs`: MRUK, kolizje ścian i mebli, NavMesh, punkty pojawiania się, pauza poza skanem,
     - `World/PassthroughStyle.cs`,
     - `Player/PlayerBody.cs`, `Player/ControllerGrabber.cs`,
     - `Weapons/` z pulą pocisków: `Weapon`, `Pistol`, `Shotgun`, `Katana`, `Throwable`, `Bullet`,
     - `Enemies/` z prostym AI na NavMeshAgent: `Enemy`, `EnemyBody` (proceduralna, fasetowana postać z prostym chodem), `Shatter`,
     - `UI/` z panelem menu,
     - `Audio/`: dźwięki generowane w kodzie, jak w Brickfall, z wysokością dźwięku zależną od tempa czasu.
   - **`Assets/Stopklatka/Editor/StopklatkaBuilder.cs`:** pozycja w menu Unity „Stopklatka → Zbuduj grę”. Tworzy materiały URP, prefaby i scenę (OVRCameraRig, MRUK, EnvironmentDepth, passthrough) oraz ustawia projekt:
     - nazwa pakietu `com.pl0t3r.stopklatka`,
     - ARM64, IL2CPP, Vulkan, ASTC, multiview,
     - minimalne i docelowe API Androida według aktualnej dokumentacji,
     - uprawnienia do sceny, passthrough i trybu bez granicy.
     
     Dzięki temu nie trzeba ręcznie klikać w Unity, a sceny nie są pisane ręcznie w YAML.
   - **Bez zewnętrznych modeli i dźwięków:** grafikę generuje kod, więc nic nie trzeba kupować ani pobierać.

Ograniczenie: tutaj nie skompiluję Unity, bo nie ma tu edytora ani licencji. Pierwsza kompilacja odbywa się w Etapie C.

### Etap B: Ty (instrukcja w `docs/USTAWIENIA.md`)
1. Konto dewelopera na developer.meta.com: utwórz organizację i zweryfikuj konto.
2. Sparuj gogle ze swoim telefonem w aplikacji Meta Horizon, na swoim koncie. Potem włącz tryb dewelopera (Headset Settings → Developer Mode). Ryzyko: gogle mają dwa cudze konta. Jeśli aplikacja nie pozwoli Ci ich sparować, tryb dewelopera musi włączyć właściciel albo trzeba zrobić reset.
3. Kabel USB-C do przesyłu danych. Kabel tylko do ładowania nie wystarczy.
4. Na komputerze z Windows:
   - Unity Hub i najnowsze Unity 6 LTS z modułem Android Build Support (OpenJDK, SDK i NDK),
   - Git,
   - Node.js LTS,
   - Claude Code,
   - `npx metavr@latest init` oraz `metavr mcp install claude-code`.
5. Sklonuj repo `stopklatka` i uruchom w tym folderze Claude Code.
6. Space Setup w szkole: zeskanuj korytarz i sale jako pomieszczenia w jednej przestrzeni. Graj tam, gdzie wolno, najlepiej w pustych korytarzach.

### Etap C: Claude Code na Twoim komputerze
1. `metavr_unity_setup` (inject i open) w folderze repo. Uruchamia się Simplified VR Setup, potem dochodzą MR Utility Kit i `com.unity.ai.navigation`.
2. „Stopklatka → Zbuduj grę”, kompilacja i poprawki aż do zera błędów.
3. Build and Run na gogle, odczyt logów przez metavr, poprawki.
4. Pomiar wydajności, opisany w sekcji „Jak sprawdzimy”.
5. Test z Tobą w szkole i dostrojenie: tempo czasu, celność przeciwników, siła rzutu.

## Projekt gry

- **Czas:**
  - `Time.timeScale` liczony z prędkości głowy i kontrolerów: od 0.03 w bezruchu do 1.0 w szybkim ruchu, wygładzony.
  - `fixedDeltaTime` skaluje się razem z nim, więc fizyka zostaje płynna w zwolnionym tempie.
  - Strzał i rzut na chwilę przyspieszają czas.
  - Śledzenie Twojego ciała nie zwalnia nigdy.
- **Przeciwnicy:**
  - czerwone, fasetowane, „kryształowe” postacie,
  - chodzą po NavMeshu wokół prawdziwych mebli,
  - podchodzą, celują z opóźnieniem reakcji i strzelają wolnymi pociskami ze smugą,
  - po trafieniu rozpadają się na odłamki i upuszczają broń, którą można złapać w locie.
- **Bronie:**
  - pistolet: 4 naboje, pusty można rzucić,
  - strzelba: 2 strzały po 6 śrutów,
  - katana: zabija przy szybkim cięciu i odbija pociski,
  - rzucane przedmioty (butelka, pusta broń): ogłuszają albo zabijają.
  - Chwytasz przyciskiem grip, strzelasz spustem. Siła rzutu liczona jest z najszybszego ruchu dłoni, tak jak w Brickfall.
- **Prawdziwe ściany jako osłona:** kolizje ze skanu (EffectMesh) zatrzymują pociski, więc możesz się chować za prawdziwą ścianą albo szafką.
- **Fale:** przeciwnicy pojawiają się tylko wewnątrz zeskanowanych pomieszczeń, co najmniej 2,5 m od Ciebie, nigdy w meblach ani za ścianami. Kolejne fale są trudniejsze. Po wyczyszczeniu fali pojawia się napis „STOP. KLATKA.”. Po trafieniu przez przeciwnika obraz miga na czerwono i fala zaczyna się od nowa.
- **Wygląd:**
  - lekko odbarwiony, jaśniejszy obraz z kamer, żeby szkoła była „biała” jak w Superhot (umiarkowanie, z przełącznikiem w menu),
  - czerwień przeciwników z efektem fresnela, czarne bronie, smugi pocisków, odłamki w zwolnionym tempie,
  - bez postprocessingu.
- **Bezpieczeństwo:**
  - wolne pociski, brak potrzeby biegania,
  - przesłanianie przez Depth API, więc ludzie i przeszkody są widoczne,
  - informacje na ekranie nie zasłaniają dołu i środka pola widzenia,
  - automatyczna pauza i czysty obraz z kamer, gdy wyjdziesz poza zeskanowany obszar.

## Wydajność: 8,3 ms na klatkę przy 120 Hz

- URP, Vulkan, multiview, MSAA 4x, fixed foveated rendering, poziomy CPU i GPU ustawione na wysoką wydajność, Application SpaceWarp wyłączony.
- Mniej niż 100 wywołań rysowania: GPU instancing, wspólne materiały, pula obiektów zamiast tworzenia ich w trakcie gry.
- Jednocześnie najwyżej około 8 przeciwników, low-poly.
- Cel: GPU poniżej około 7 ms, CPU poniżej około 6 ms.
- Rozdzielczość: start od 1.205. Gdy klatka przekracza budżet, dynamicznie schodzi do 1.0, żeby utrzymać 120 klatek.

## Ryzyka (uczciwie)

- 120 Hz razem z natywną rozdzielczością i przesłanianiem może nie zmieścić się w budżecie w najcięższych momentach. Wtedy rozdzielczość na chwilę spada, zgodnie z Twoim wyborem.
- Tryb dewelopera na goglach z cudzym właścicielem (zobacz Etap B, punkt 2).
- Skan dużej szkoły może być niedokładny. Wtedy dobrze sprawdza się poprawianie skanu w Space Setup.
- Kod z Etapu A nie jest skompilowany. Pierwsze uruchomienie w Unity prawie na pewno wymaga poprawek, a robi je Claude Code na Twoim komputerze.

## Jak sprawdzimy, że działa

- **Chmura:** każde wywołanie API Meta i MRUK porównane z kodem paczek 207.0.1 przed wypchnięciem.
- **Komputer, kompilacja:** zero błędów i ostrzeżeń w konsoli Unity. Meta Project Setup Tool bez czerwonych pozycji.
- **Gogle:**
  - w logach nie ma wyjątków,
  - log pokazuje `systemDisplayFrequency = 120` i aktualny render scale,
  - pomiar przez OVR Metrics albo `metavr perf` w fali 8 przeciwników daje stabilne 120 klatek, a czas klatki p95 jest poniżej 8,3 ms.
- **Lista kontrolna rozgrywki:**
  - czas staje, gdy się nie ruszasz,
  - każda z 4 broni działa,
  - pociski zatrzymują się na prawdziwych ścianach,
  - przeciwnicy omijają ławki,
  - po wyjściu poza skan gra się zatrzymuje,
  - granica nie wyskakuje na korytarzu.

## Notatki z realizacji (Etap A)

- **Granica (guardian):** zamiast wpisu `BOUNDARYLESS_APP` w manifeście użyty jest kontekstowy Boundary API z SDK: `OVRManager.shouldBoundaryVisibilityBeSuppressed` (aktywny tylko przy włączonym passthrough) oraz `boundaryVisibilitySupport = Required` w OVRProjectConfig. SDK obsługuje to wprost, bez ręcznej edycji manifestu. Obie metody wykluczają się nawzajem.
- **Rozdzielczość:** dynamiczna rozdzielczość OVRManager w zakresie 1.0–1.205 (`quest3Min/MaxDynamicResolutionScale`), ustawiana przez builder.
- **Uprawnienie do sceny:** `RoomService` sam prosi o `com.oculus.permission.USE_SCENE` i dopiero wtedy wywołuje `MRUK.LoadSceneFromDevice`. Dlatego `LoadSceneOnStartup = false`.
- **Pistolet i strzelba** to jedna klasa `Gun` z `GunKind`, a nie dwie osobne klasy.
- **Napisy:** `TextMesh` z wbudowaną czcionką i własnym shaderem `Stopklatka/Text` (zgodnym z renderowaniem stereo), żeby nie wymagać importu TMP Essentials.

## Weryfikacja w chmurze

- Każde użyte API Meta i MRUK sprawdzone w kodzie paczek `com.meta.xr.sdk.core` i `com.meta.xr.mrutilitykit` w wersji 207.0.1, pobranych z `npm.developer.oculus.com`.
- Kompilacja testowa skryptów gry i buildera kompilatorem C# (Roslyn) z prawdziwymi bibliotekami Unity 6000.0.85f1. Typy paczek (Meta, MRUK, URP, AI Navigation) zastąpione zaślepkami o sygnaturach skopiowanych z kodu źródłowego tych paczek. Wynik opisany w historii commitów.
- Nie sprawdzone tutaj: shadery (kompiluje je dopiero Unity) i zachowanie na goglach.

## Etap A2: tryb DOM i grafika v2

**Tryb DOM** (`Scripts/Core/GameMode.cs`), wybierany kryształem TRYB w menu i zapamiętywany w `PlayerPrefs`:

| | SZKOŁA | DOM |
| --- | --- | --- |
| Odległość pojawiania się | 2,5–9 m | 1,8–7 m |
| Przeciwników naraz | do 6 | do 4 |
| Przeciwników w fali | 2 + fala (max 12) | 2 + ¾·fala (max 10) |
| Dystans strzelców | 4 m | 3 m |
| Prędkość chodu | 1,0 m/s | 0,9 m/s |
| Pociski wrogów | 6,5 m/s | 5,5 m/s |
| Czas reakcji | ×1,0 | ×1,15 |
| Pojawianie się w Twoim pokoju | 70% | 50% |

- **Drzwi:** `EffectMesh.CutHoles = DOOR_FRAME` wycina otwory w siatkach ścian, a kolizje ścian powstają z tych siatek. `Physics.queriesHitBackfaces = true`, żeby cienkie ściany zatrzymywały pociski z obu stron.
- **Piętra:** wysokość podłogi co klatkę z bieżącego pokoju MRUK (`PlayerRig.FloorY` i globalny `_StopklatkaFloorY` dla shaderów). Przeciwnicy pojawiają się tylko na tym samym piętrze (różnica wysokości do 0,6 m i pełna ścieżka NavMesh).
- **Małe pokoje:** jeśli 40 prób zawiedzie, druga runda szuka miejsca z mniejszą minimalną odległością (60%, nie mniej niż 1,2 m).

**Grafika v2:**
- `Crystal.shader`:
  - dwa tony (`_ShadowColor`/`_BaseColor`),
  - połysk Blinn-Phong,
  - przyciemnienie przy podłodze,
  - wariant `_REVEAL` do materializacji od podłogi.
- `Transparent.shader`: kolor wierzchołków (zanikanie) i `_OcclusionBias` dla rzeczy na podłodze.
- Efekty:
  - smugi pocisków z gradientem,
  - cień pod przeciwnikami,
  - czerwony pierścień przy pojawianiu się,
  - błysk i dwa odcienie czerwonych odłamków przy śmierci,
  - czerwona winieta przy trafieniu.
- Model przeciwnika: barki, łokcie i kolana jako kryształowe stawy, dłuższa szyja, głowa z wyraźną szczęką.
- Napisy: pogrubione z ciemnym cieniem; „STOP.” i „KLATKA.” pulsują.
- Styl obrazu z kamer w menu: NORMALNY, BIAŁY albo SUPERHOT (gradient jasności: ciemne → chłodny szary, jasne → biel).
- Kompilacja testowa skryptów i buildera jak w Etapie A: 0 błędów, 0 ostrzeżeń. Shadery sprawdzi dopiero Unity (lista kontrolna w `CLAUDE.md`).
