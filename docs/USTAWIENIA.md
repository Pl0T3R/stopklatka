# Stopklatka: przygotowanie komputera i gogli

Robisz to raz. Potem Claude Code na Twoim komputerze sam buduje grę i wgrywa ją na gogle.

## 1. Konto dewelopera Meta (około 10 minut)

1. Wejdź na <https://developers.meta.com/horizon/> i zaloguj się swoim kontem Meta.
2. Utwórz **organizację**. Nazwa może być dowolna, na przykład Twój nick.
3. Zweryfikuj konto w panelu dewelopera (Developer Dashboard → Verification). Meta zwykle prosi o numer telefonu albo kartę. Tryb dewelopera wymaga też ukończonych 18 lat.

## 2. Gogle: parowanie i tryb dewelopera

1. Na telefonie, w aplikacji **Meta Horizon**, zaloguj się tym samym kontem.
2. Sparuj gogle z telefonem: **Menu → Urządzenia → Sparuj nowe gogle**.
   - Na goglach są już dwa cudze konta. Jeśli aplikacja nie pozwoli Ci ich sparować, tryb dewelopera musi włączyć właściciel gogli ze swojego telefonu. Druga możliwość to reset gogli do ustawień fabrycznych.
3. W aplikacji wejdź w gogle → **Ustawienia gogli (Headset Settings)** → **Tryb dewelopera (Developer Mode)** i włącz go.
4. Potrzebujesz **kabla USB-C do przesyłu danych**. Kabel tylko do ładowania nie zadziała. Podłącz gogle do komputera, załóż je i przy pytaniu „Allow USB debugging” wybierz **Always allow from this computer**.

## 3. Komputer z Windows

Zainstaluj po kolei:

1. **Unity Hub**: <https://unity.com/download>.
2. W Unity Hub: **Installs → Install Editor**, wybierz najnowsze **Unity 6 LTS** (6000.0.66 lub nowsze). Zaznacz moduł **Android Build Support** razem z **OpenJDK** i **Android SDK & NDK Tools**.
3. **Git for Windows**: <https://git-scm.com/download/win>.
4. **Node.js LTS**: <https://nodejs.org>. Jest potrzebny dla narzędzia Meta `metavr`.
5. **Claude Code**: otwórz **PowerShell** (nie CMD) i wpisz:
   ```powershell
   irm https://claude.ai/install.ps1 | iex
   ```
   Potem zamknij i otwórz PowerShell ponownie, wpisz `claude` i zaloguj się.

## 4. Pobranie gry i start

W PowerShell:

```powershell
cd $HOME\Documents
git clone https://github.com/Pl0T3R/stopklatka
cd stopklatka
claude
```

Przy pierwszym uruchomieniu Claude Code zapyta o serwer **metavr** z pliku `.mcp.json`. Zgódź się, bo to narzędzie Meta do gogli i Unity.

Potem napisz do Claude:

> Zbuduj grę według CLAUDE.md i wgraj ją na moje gogle.

Claude:
1. tworzy projekt Unity,
2. kompiluje go i poprawia błędy,
3. wgrywa grę na gogle,
4. mierzy, czy działa w 120 klatkach.

Pierwsze otwarcie Unity trwa 5–10 minut.

## 5. Skan miejsca gry (Space Setup)

W goglach: **Ustawienia → Przestrzeń fizyczna → Space Setup**.

**Szkoła**
1. Zeskanuj korytarz i sale, po których chcesz chodzić. Każde pomieszczenie skanuj osobno, ale w tej samej przestrzeni.
2. Sprawdź, czy ławki i stoły są oznaczone jako stoły. Wtedy przeciwnicy je omijają, a broń może na nich leżeć.

**Dom**
1. Zeskanuj każdy pokój, w którym chcesz grać, w tej samej przestrzeni (razem z przedpokojem, jeśli łączy pokoje).
2. Sprawdź, czy **drzwi** są zaznaczone w skanie. Przez drzwi lecą pociski, a przeciwnicy wchodzą nimi z innych pokoi. Bez drzwi ściana jest pełna.
3. Inne piętro skanuj, stojąc na nim. Przeciwnicy pojawiają się tylko na piętrze, na którym jesteś.
4. Schody nie są pokojem, więc kiedy na nie wejdziesz, gra się zatrzyma. To celowe: po schodach chodzisz w zwykłym obrazie z kamer.
5. Kanapa, łóżko i szafy oznaczone w skanie są przeszkodami. Przeciwnicy je omijają, a pociski się na nich zatrzymują.

## Menu gry

Menu to trzy kryształy, które strzelasz albo dotykasz kontrolerem:

| Kryształ | Co robi |
| --- | --- |
| **START** (środek) | Zaczyna grę |
| **STYL** (po lewej) | Wygląd prawdziwego świata: **NORMALNY** (zwykły obraz), **BIAŁY** (wyblakły, domyślny), **SUPERHOT** (białe ściany i szare cienie, czerwień zostaje tylko na przeciwnikach) |
| **TRYB** (po prawej) | **SZKOŁA** albo **DOM**. W domu przeciwnicy pojawiają się bliżej (od 1,8 m), jest ich naraz mniej, a ich pociski lecą wolniej, bo pokoje są małe |

Gra pamięta wybrany styl i tryb. Na pierwszy start jest SZKOŁA i styl BIAŁY.

## Bezpieczeństwo

- Graj tam, gdzie wolno, najlepiej w pustym korytarzu.
- Gra nie wymaga biegania. Czas płynie tylko wtedy, kiedy się ruszasz, więc wolne, spokojne ruchy wystarczą.
- Kiedy wyjdziesz poza zeskanowany obszar, gra sama się zatrzymuje i pokazuje zwykły obraz z kamer.
- Ludzie i przeszkody, które są bliżej Ciebie, zasłaniają przeciwników, więc ich widzisz.
- W stylu **SUPERHOT** prawdziwe przedmioty są mniej widoczne (wszystko robi się białe). Jeśli grasz w ciasnym pokoju albo między meblami, wybierz NORMALNY albo BIAŁY.
- W domu uważaj na zwierzęta, porozrzucane rzeczy i progi, bo gogle ich nie zasłaniają. Zrób miejsce przed grą.

## Sterowanie

| Co | Jak |
| --- | --- |
| Podnieś broń albo butelkę | **Grip** (przycisk pod środkowym palcem) |
| Strzał | **Spust** (palec wskazujący) |
| Rzut | Zamach i puść grip |
| Złap coś w locie | Grip, kiedy przedmiot jest przy dłoni |
| Menu / wróć do startu | Przycisk **☰** na lewym kontrolerze |
| Licznik klatek i rozdzielczości | Wciśnij lewą gałkę |

Start gry: weź pistolet, który unosi się obok Ciebie, i strzel w czerwony kryształ **START**. Możesz go też dotknąć kontrolerem. Kryształy **STYL** i **TRYB** opisuje sekcja „Menu gry”.
