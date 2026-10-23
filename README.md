# Stopklatka

Gra mixed reality w stylu Superhot na Meta Quest 3. Chodzisz po prawdziwej szkole albo domu zeskanowanym w Space Setup, a czerwone kryształowe postacie pojawiają się w zeskanowanych pomieszczeniach. Czas płynie tylko wtedy, kiedy się ruszasz.

- **Tryby:** SZKOŁA (duże sale i korytarz) i DOM (cały dom, kilka pokoi, także piętra). Wybierasz w menu.
- **Wygląd:** biały świat, czerwoni przeciwnicy, czarna broń. Styl obrazu z kamer do wyboru w menu: NORMALNY, BIAŁY albo SUPERHOT.

- **Sterowanie:** kontrolery Touch. Grip chwyta, spust strzela, puszczenie gripu rzuca.
- **Bronie:** pistolet (4 naboje), strzelba (2 strzały po 6 śrutów), katana (tnie przeciwników i pociski), butelki do rzucania.
- **Prawdziwe ściany i meble** zatrzymują pociski, a przeciwnicy je omijają. Ludzie i przeszkody zasłaniają przeciwników (Depth API).
- **Wydajność:** 120 Hz, około 2024 px na oko z dynamiczną rozdzielczością (płynność ma pierwszeństwo).
- **Bezpieczeństwo:** gra nie wymaga biegania. Po wyjściu poza zeskanowany obszar gra staje i pokazuje zwykły obraz z kamer.

## Jak zacząć

1. Przygotuj komputer i gogle według [docs/USTAWIENIA.md](docs/USTAWIENIA.md).
2. Sklonuj to repo, uruchom w nim `claude` i napisz: *„Zbuduj grę według CLAUDE.md i wgraj ją na moje gogle.”*

Szczegóły projektu: [docs/PLAN.md](docs/PLAN.md). Instrukcje dla Claude Code: [CLAUDE.md](CLAUDE.md).

## Stan

Kod (`Assets/Stopklatka/`) jest gotowy i skompilowany testowo z bibliotekami Unity 6, ale nie był jeszcze otwierany w edytorze Unity ani uruchamiany na goglach. Pierwsze uruchomienie zrobi Claude Code na Twoim komputerze i poprawi to, co trzeba.

| Folder | Zawartość |
| --- | --- |
| `Assets/Stopklatka/Scripts/` | Kod gry: czas, przeciwnicy, bronie, skan pokoju, dźwięk, wydajność |
| `Assets/Stopklatka/Shaders/` | Kryształowy wygląd z przesłanianiem przez prawdziwe obiekty |
| `Assets/Stopklatka/Editor/` | Menu „Stopklatka → Build Game”: ustawienia projektu i scena jednym kliknięciem |
