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
