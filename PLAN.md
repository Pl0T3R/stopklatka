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
| 3 poziomy w różnych pomieszczeniach | 3 **scenariusze** w zeskanowanej przestrzeni. Każdy ma własny zestaw fal (typy przeciwników i broni, 30–90 s) i własne białe wirtualne rekwizyty ustawione przez MRUK na wolnej podłodze przy ścianach: 1. Galeria (postumenty z białymi posągami), 2. Ulica (latarnie, barierki, niska osłona jak maska auta), 3. Hol (rama „windy” przy ścianie, z której wychodzą przeciwnicy, i lada) |
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
| 3 | `unity-dev` | Pistolet maszynowy, pusty strzał (klik i wibracja), `killOnThrow`, nowe przedmioty do rzucania, pięść, zestrzeliwanie pocisków, łapanie w locie, impulsy czasu przy chwycie i rzucie | Kryteria 2, 3 i 6 możliwe do sprawdzenia |
| 4 | `unity-dev` | Cięcie na pół, świecące przełomy i przekroje, `LevelManager` z restartem poziomu po 1,5 s, fale w poziomach | Kryteria 4 i 5 |
| 5 | główna sesja | Styl: gradient filtra SUPERHOT według palety, kreskowanie, kolory i materiały, dłonie i broń. Dźwięk: AudioMixer i ambient narastający z ruchem | Lista ze `STYLE_GUIDE.md` |
| 6 | `unity-dev` | 3 poziomy (rekwizyty i fale), menu z sześcianów, wybór poziomu, wyjście, przejścia i napisy w TMP | Pełna gra od menu do końca poziomu 3 |
| 7 | `unity-dev` | Optymalizacja: budżety niżej, pooling, ustawienia XR i buildu (raport z wartościami) | Budżety spełnione na papierze, lista pomiarów na gogle |

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
