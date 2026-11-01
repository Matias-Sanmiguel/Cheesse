# Cheesse

A desktop chess game for playing against Stockfish 19.

Run the package for your computer:

- **Linux x86-64:** open `Cheesse-linux-x64`, then run `./Cheesse.x86_64`.
- **Windows x86-64:** open `Cheesse-windows-x64`, then double-click `Cheesse.exe`.

Keep the whole folder together. Both packages include the .NET runtime and Stockfish;
players do not need Godot, .NET, Python, or a separate engine installation.
Choose human-vs-human or Stockfish (you play White), a time control and a difficulty.
Click a piece and its destination. The side panel shows both clocks and the SAN move
history. It offers Undo/Redo (Ctrl+Z), save/load including clocks, resignation, and draw
agreement or claims. Stockfish has four approximate strength levels; it runs offline.

Import/export opens a file picker for FEN positions or PGN games. Imports open in
human analysis mode without a clock; FEN carries only a position, while PGN also carries
moves and a result. Saves retain the exact game, engine settings, and clocks. Existing
v1 saves are still supported. Timers pause in menus, dialogs and history review;
play an alternative move or finish Redo to resume. Triple repetition and fifty moves
require a claim; fivefold repetition and seventy-five moves end the game automatically.
Against Stockfish, use a rule-based draw claim rather than an agreement offer.

To run from source, install **Godot 4.7.2 .NET** and the **.NET 8 SDK**. Prepare the engine:

```sh
python3 scripts/release.py --engines-only
dotnet build Cheesse.sln
godot-mono --path chess-game --editor
```

Use your Godot .NET executable name in place of `godot-mono` if different, then press F5
in Godot to run the project. The ordinary Godot editor does not support C# projects.

To build both release folders:

```sh
python3 scripts/release.py --godot godot-mono
```

The Python script uses only the standard library. It downloads pinned official Stockfish
archives and only the needed Godot .NET export templates, verifies SHA-256 checksums,
generates ignored export presets, and exports self-contained packages into
`dist/Cheesse-linux-x64/` and `dist/Cheesse-windows-x64/`, plus `.tar.gz` and `.zip` archives.
Existing verified downloads in
`.release-cache/` are reused. `--platform linux` or `--platform windows` builds one target;
`--prepare-only` downloads and configures without exporting. The build machine needs
Python 3.9+, the .NET 8 SDK, Godot 4.7.2 .NET, and internet access on the first build.
No shared Godot templates are overwritten. The game build is reproducible from pinned
dependencies; final executable bytes may differ with SDK patch versions and timestamps.

Run the release tooling checks with `python3 -m unittest discover -s scripts -p 'test_*.py'`.
Stockfish's GPLv3 license and exact source details are in [third-party](third-party/Stockfish-SOURCE.txt),
and the source archive is included in each release. Its neural-network weights come from
the official engine; this project performs no neural-network training.

Godot's [C# requirements](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html)
and [export guide](https://docs.godotengine.org/en/stable/tutorials/export/exporting_projects.html)
describe the editor, SDK, runtime, and export-template requirements.
