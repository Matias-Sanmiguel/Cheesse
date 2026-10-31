# Persistence integration tests

These tests run the production FileSaveRepository source inside Godot .NET, using
an isolated user:// directory for the "Cheesse Persistence Tests" project.
They never access the normal Cheesse save.

Build with the same Godot .NET SDK as chess-game:

    dotnet build chess-game-tests/Chess.Game.PersistenceTests.csproj

With the Godot .NET executable on PATH, run these commands in order:

    godot --headless --path chess-game-tests -- --save
    godot --headless --path chess-game-tests -- --load

The first process saves and exits. The second process loads that save, verifies
the restored game, and runs the remaining persistence checks. It exits with
code 1 on failure and code 0 on success. To run the checks without the
cross-process scenario, use:

    godot --headless --path chess-game-tests -- --run

Coverage includes FEN fields, custom starting positions, PGN history, legal moves,
castling, en passant, promotions, check, mate, stalemate, fifty-move and
repetition draws, Undo, missing/corrupt/inconsistent saves, overwrite, and
preserving the previous save on a Windows file replacement failure.

## Main menu integration tests

Run from PowerShell, passing the Godot .NET console executable:

    ./chess-game-tests/RunMenuTests.ps1 -Godot 'C:/path/to/Godot_console.exe'

This builds the production game and runs its actual scenes and assembly inside
an ignored sandbox with a separate user:// directory (Cheesse Menu Tests).
It checks startup, human turns, random replies, save/continue, persisted opponent
mode, old saves, corrupt/missing saves, and resetting to a fresh game. Mouse
input travels through the viewport and the scene buttons emit their real signals.
The script fails if the test scene does not finish successfully.

## Side panel integration tests

    ./chess-game-tests/RunMenuTests.ps1 -Godot 'C:/path/to/Godot_console.exe' -TestScene SidePanelTests

The isolated Godot test verifies layout at 1440x900, buttons, command/caretaker
Undo and Redo, bus updates for turn/check/mate, save/load/new game, random-black
toggle, branching history, and the actual white/black pixels of the SVG atlas.

## Cross-platform UI runner

With Python 3 and Godot .NET (the standard Godot executable cannot load C#):

    python chess-game-tests/RunUiTests.py --godot godot-mono --scene MenuTests
    python chess-game-tests/RunUiTests.py --godot godot-mono --scene SidePanelTests
    python chess-game-tests/RunUiTests.py --godot godot-mono --scene MvpTests

Each scene runs the production assembly and resources in an ignored sandbox with
its own user directory. The runner checks both exit status and the PASS marker.
The MVP scene checks off-board/illegal clicks, both players, Ctrl+Z, Undo/Redo
across mate and draws, winner/rematch, underpromotion/cancellation, input blocking,
and resizing to 1280x720. The pixel art layout uses a 1440x900 canvas and scales
with the window. Persistence tests also verify that loading (including rejected
saves) does not emit domain events for historical moves.
