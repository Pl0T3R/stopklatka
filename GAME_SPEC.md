# Zadanie: przerób mój projekt VR w Unity na grę w stylu SUPERHOT

## Kontekst
- Silnik: **Unity**. Platforma docelowa: **Meta Quest 3** (standalone, build Android).
- Masz dostęp do mojego aktualnego projektu. NIE zaczynaj od zera, tylko przerób to, co jest. Zachowaj działające elementy (XR rig, sterowanie, konfigurację buildu).
- Główna idea gry: **czas płynie tylko wtedy, gdy gracz się rusza.** Gdy stoisz w miejscu, świat prawie się zatrzymuje. Każdy ruch głowy i rąk przyspiesza czas.
- Jeśli masz skille Meta dla Unity (hz-unity-*, metavr) albo dostęp do dokumentacji Meta / Horizon OS, sprawdzaj w nich API zamiast polegać na pamięci.

## Reference images (dołączone do tej wiadomości)
Obrazy referencyjne są dołączone do mojej wiadomości, nie ma ich w projekcie. Masz z nich aktywnie korzystać:
1. Jeśli masz do nich dostęp jako pliki, skopiuj je do folderu `References/` w projekcie.
2. **Zanim napiszesz kod**, przeanalizuj każdy obraz i opisz go bardzo szczegółowo w pliku `STYLE_GUIDE.md`: paleta kolorów (HEX), oświetlenie i cienie, materiały (matowe / błyszczące / szkliste) i ich ustawienia w URP, wygląd przeciwników, broni i przedmiotów, kształty i proporcje, układ pomieszczeń, napisy/UI, wygląd efektu rozpadu. Opis ma pozwolić odtworzyć ten wygląd komuś, kto obrazów nie widzi. Zrób to od razu, bo przy długiej sesji obrazy mogą wypaść z kontekstu.
3. Obrazy to **cel wizualny**. Przy konflikcie: wygląd → słuchaj obrazów, mechanika → słuchaj tego tekstu.
4. Nie kopiuj obrazów 1:1. Odtwórz ich klimat własnymi, prostymi modelami (prymitywy, low-poly).
5. Po każdym etapie porównaj efekt z obrazami i wypisz, co się zgadza, a co jeszcze odbiega.

Opis obrazów (uzupełniam ręcznie):
- Obraz 1: Obraz pokazuje jak wygląda umierający przeciwnik oraz jak wygląda lecący pocisk
- Obraz 2: obraz pokazuje jak wygląda walka wręcz 
- Obraz 3: obraz pokazuje przeciętego przez gracza przeciwnika 

## Krok przygotowawczy (zrób jako pierwszy)
1. Zapisz ten cały prompt w projekcie jako `GAME_SPEC.md`.
2. Zrób punkty 1–2 z sekcji „Reference images”.
3. Utwórz dwa subagenty (szczegóły w sekcji „Etapy pracy i poziom effort”):

`.claude/agents/unity-dev.md`
```
---
name: unity-dev
description: Implementacja większych etapów gry VR w Unity (bronie, przeciwnicy, poziomy, optymalizacja). Używaj do etapów 3, 4, 6 i 7.
model: inherit
effort: high
---
Jesteś programistą Unity (Meta Quest 3). Zanim zaczniesz, przeczytaj GAME_SPEC.md, PLAN.md i STYLE_GUIDE.md. Jeśli istnieje folder References/, obejrzyj też obrazy.
Rób tylko etap i zakres ze zlecenia. Trzymaj się sekcji „Zasady pracy w Unity” z GAME_SPEC.md.
Na koniec zwróć raport: zmienione pliki, jak przetestować (edytor i gogle), znane problemy.
```

`.claude/agents/unity-tweaks.md`
```
---
name: unity-tweaks
description: Drobne poprawki w grze VR w Unity: wartości w configu, kolory, materiały, pojedynczy bug w jednym lub dwóch plikach.
model: inherit
effort: medium
---
Wprowadzasz tylko małe zmiany wskazane w zleceniu. Przeczytaj potrzebne pliki, zmień tylko to, o co proszono, i zwróć krótką listę zmian.
```

## Rozpoznanie projektu (część etapu 1)
- Sprawdź: wersję Unity, render pipeline (oczekuję URP), SDK do VR (Meta XR SDK: Core + Interaction SDK albo XR Interaction Toolkit), czy używany jest OpenXR czy Oculus XR Plugin, strukturę folderów i scen.
- Wypisz, co już działa (rig, ręce, chwytanie, lokomocja, sceny).
- Trzymaj się SDK, które już jest w projekcie. Nie mieszaj dwóch systemów interakcji.
- Przed edycją zapisz plan w `PLAN.md`: co zostaje, co zmieniasz, co dodajesz.
- Jeśli czegoś nie da się zrobić, powiedz to i zaproponuj zamiennik.

## Zasady pracy w Unity
- **Nie edytuj ręcznie plików .unity / .prefab (YAML).** Pisz skrypty edytora (MenuItem w folderze `Editor`), które jednym kliknięciem budują prefaby, materiały i sceny, np. `Tools/Game/Build Enemy`.
- Jeśli coś muszę zrobić ręcznie w edytorze, wypisz to krok po kroku (co kliknąć, gdzie, jakie wartości).
- Każdy system w osobnym skrypcie (np. `TimeManager`, `PlayerHealth`, `Weapon`, `Bullet`, `Throwable`, `EnemyAI`, `Shatter`, `LevelManager`). Czytelne nazwy, krótkie komentarze.
- Wszystkie parametry gry w ScriptableObject z rozsądnymi wartościami domyślnymi.
- Nie usuwaj istniejących funkcji bez pytania. Jeśli coś przeszkadza, wyłącz to flagą.
- Do każdego etapu opisz, jak go przetestować w edytorze (Meta XR Simulator albo Quest Link) i na goglach.

## 1. System czasu (najważniejsza część)
- `timeScale` = funkcja prędkości ruchu headsetu i obu kontrolerów/dłoni. Prędkość licz z transformów rigu (OVRCameraRig / XR Origin) na `Time.unscaledDeltaTime`.
- Minimum ok. 0.03, maksimum 1.0. Przesunięcie ma większą wagę niż sam obrót głowy (obrót np. waga 0.3).
- Wygładzanie (`Mathf.SmoothDamp` / lerp): szybkie przyspieszanie, trochę wolniejsze zwalnianie, bez migotania.
- Akcje gracza (strzał, rzut, chwyt) dają krótki „impuls” czasu (np. 0.2 s przy timeScale 1.0).
- `TimeManager` (singleton) ustawia `Time.timeScale` oraz `Time.fixedDeltaTime = bazowyFixed * Time.timeScale` (bazowyFixed dopasowany do odświeżania gogli, np. 1/72 lub 1/90).
- **Gracz zawsze działa w czasie rzeczywistym**: śledzenie głowy i rąk oraz trzymane przedmioty nie mogą lagować. Animator dłoni gracza ustaw na `AnimatorUpdateMode.UnscaledTime`.
- Przeciwnicy, pociski, fizyka świata, cząsteczki i NavMeshAgent zostają na czasie skalowanym, więc zwalniają same.
- Tryb debug: wskaźnik timeScale na nadgarstku (wyłączony w normalnej grze).

## 2. Walka
- **Gracz ginie od jednego trafienia** (hitbox głowy i tułowia liczony od pozycji headsetu). Po śmierci krótki efekt i restart poziomu po ok. 1,5 s.
- **Przeciwnik ginie od jednego trafienia**: pocisk, cios bronią białą lub pięścią powyżej progu prędkości.
- Śmierć przeciwnika: **rozpada się na odłamki** z tego samego materiału co ciało. Użyj gotowych prefabów z odłamkami trzymanych w poolu, bez cięcia siatki w trakcie gry. Odłamki znikają po kilku sekundach.
- Martwy przeciwnik upuszcza broń. Broń można złapać w powietrzu.

## 3. Bronie i przedmioty
- Pistolet (ok. 4 naboje), strzelba (2 strzały, rozrzut), pistolet maszynowy (krótkie serie). **Bez przeładowania**: pusta broń służy do rzucania. Pusty strzał daje klik i wibrację.
- Rzucona broń lub przedmiot przy dużej prędkości ogłusza przeciwnika i wytrąca mu broń (parametr `killOnThrow` do przełączenia).
- Broń biała (np. miecz, pałka) może przeciąć pocisk w locie.
- Przedmioty do rzucania: butelki, kubki, popielniczki, proste kształty.
- Chwytanie przyciskiem grip. Jeśli projekt obsługuje hand tracking, ma działać też tam.
- **Trzymane przedmioty** są w ręce kinematyczne i podążają za dłonią w czasie rzeczywistym, bez lagu przy małym timeScale. Przy puszczeniu policz prędkość rzutu z historii ruchu ręki (unscaled), potem oddaj obiekt fizyce.
- **Pociski to fizyczne, widoczne obiekty** (nie hitscan), z krótkim śladem (TrailRenderer). Rigidbody z `CollisionDetectionMode.ContinuousDynamic` albo raycast między poprzednią a obecną pozycją w każdym FixedUpdate, żeby nie przelatywały przez cienkie obiekty. W zwolnionym czasie widać je w powietrzu, więc da się je ominąć, zestrzelić albo przeciąć.
- Wibracje (haptics): strzał, chwyt, trafienie, cios.

## 4. Przeciwnicy
- Wygląd zgodny z reference images. Domyślnie: czerwone, lekko szkliste humanoidy low-poly bez twarzy.
- Pojawiają się falami w punktach spawnu (wchodzą drzwiami, zza rogu, z windy).
- Z bronią: podchodzą, celują z widocznym opóźnieniem, strzelają z lekkim rozrzutem, żeby dało się unikać. Bez broni: biegną do gracza i biją wręcz.
- Nawigacja przez NavMeshAgent (pakiet AI Navigation). Animacje mogą być proste lub proceduralne.

## 5. Gracz i komfort
- **Bez sztucznej lokomocji**: gra room-scale na ok. 2×2 m. Jeśli projekt ma lokomocję, wyłącz ją domyślnie (zostaw opcję w configu).
- Zagrożenia ustawione tak, żeby unikało się ich pochyleniem, kucnięciem i krokiem w bok.
- Śmierć bez gwałtownego ruchu kamery: krótki błysk lub wygaszenie, napis, restart.

## 6. Styl wizualny (jeśli obrazy nie mówią inaczej)
- Otoczenie białe lub jasnoszare, prosta geometria, miękkie cienie z baked lightmaps.
- Przedmioty interaktywne czarne lub ciemnoszare, matowe.
- Przeciwnicy w intensywnej czerwieni, lekko emisyjni lub szkliści.
- Minimum tekstur. Kluczowy jest kontrast: biel, czerń, czerwień.
- UI w przestrzeni (duże proste napisy na ścianach i w powietrzu, TextMeshPro w world space), bez HUD-a przyklejonego do twarzy.
- **Nie używaj nazwy, logo, fontów, muzyki ani assetów z oryginalnej gry.** Bierzesz tylko inspirację mechaniką i klimatem. Napisy i nazwy wymyśl własne.

## 7. Dźwięk
- Pitch przez AudioMixer (exposed parameter) zależny od timeScale: spowolnione strzały, niski szum, gdy czas stoi.
- Dźwięk pozycyjny 3D, cichy ambient narastający z ruchem.

## 8. Poziomy i menu
- 3 krótkie poziomy (30–90 s), każdy w innym prostym pomieszczeniu inspirowanym obrazami. `LevelManager` obsługuje start, koniec i restart.
- Start poziomu: gracz chwyta obiekt startowy (np. czarny sześcian).
- Koniec poziomu: wszyscy przeciwnicy martwi, krótki własny napis, przejście dalej.
- Menu w przestrzeni: zamiast przycisków przedmioty do chwycenia (start, wybór poziomu, wyjście).

## 9. Wydajność (Quest 3)
- Stabilne 72 FPS minimum, docelowo 90.
- Build: Android, IL2CPP, ARM64, Multiview / Single Pass Instanced, MSAA 4x, bez HDR, Fixed Foveated Rendering.
- Baked lightmaps, maksymalnie jedno światło realtime, proste shadery URP (Lit/Unlit), bez ciężkiego post-processingu.
- Pooling pocisków, odłamków i efektów. Niska liczba draw calli i trójkątów, podaj wartości.
- Sprawdź Project Settings > XR i wypisz, co zmieniasz.

## Etapy pracy i poziom effort
Nie możesz sam zmienić effort sesji (ustawiam go ja, na start będzie `xhigh`). Dlatego zmieniaj effort przez subagentów utworzonych w kroku przygotowawczym: każdy ma własny poziom effort w swoim pliku.

| Etap | Kto robi | Effort |
|---|---|---|
| 1. Rozpoznanie projektu, analiza obrazów, `STYLE_GUIDE.md`, `PLAN.md` | Ty (główna sesja) | xhigh |
| 2. System czasu + scena testowa | Ty (główna sesja) | xhigh |
| 3. Chwytanie, rzucanie, bronie, pociski | `unity-dev` | high |
| 4. Przeciwnicy, odłamki, śmierć i restart | `unity-dev` | high |
| 5. Styl wizualny i dźwięk | Ty (główna sesja, bo widzisz obrazy) | xhigh |
| 6. 3 poziomy, menu, przejścia | `unity-dev` | high |
| 7. Optymalizacja pod Quest 3 | `unity-dev` | high |
| Drobne poprawki po testach | `unity-tweaks` | medium |
| Sprawdzenie każdego etapu | Ty (główna sesja) | xhigh |

Zasady:
- Jesteś koordynatorem: planujesz, robisz etapy 1, 2 i 5, resztę zlecasz subagentom. Po każdym etapie sam sprawdzasz wynik z kryteriami akceptacji, `STYLE_GUIDE.md` i obrazami. Jeśli coś jest źle, zlecasz poprawkę.
- Subagent nie widzi naszej rozmowy ani obrazów z wiadomości. W każdym zleceniu podaj: numer etapu, dokładny zakres, pliki do przeczytania i warunki zakończenia. Wszystko ważne zapisuj w `PLAN.md` i `STYLE_GUIDE.md`.
- Etapy rób po kolei, jeden subagent naraz, bo pracują na tych samych plikach.
- Po każdym etapie napisz mi: co zmieniłeś, w jakich plikach, jak to przetestować na goglach, znane problemy. Potem poczekaj na moje „dalej”.
- Jeśli subagenci nie są widoczni od razu po utworzeniu albo pole `effort` nie działa w mojej wersji Claude Code, powiedz mi to. Wtedy przed każdym etapem napisz, jaki effort mam ustawić ręcznie.

## Kryteria akceptacji
- [ ] Gdy stoję, przeciwnicy i pociski prawie stoją. Gdy się ruszam, płynnie przyspieszają.
- [ ] Ręce, głowa i trzymane przedmioty zawsze reagują natychmiast.
- [ ] Pocisk da się ominąć, zestrzelić i przeciąć.
- [ ] Przeciwnik ginie od 1 trafienia i rozpada się na odłamki.
- [ ] Ja ginę od 1 trafienia i poziom się restartuje.
- [ ] Mogę złapać broń w locie i rzucić pustą bronią.
- [ ] Wygląd zgadza się ze `STYLE_GUIDE.md` i reference images.
- [ ] Stabilny framerate na Quest 3, brak błędów w konsoli, build się uruchamia na goglach.

Jeśli czegoś nie wiesz o projekcie albo o moich oczekiwaniach, zapytaj zamiast zgadywać.
