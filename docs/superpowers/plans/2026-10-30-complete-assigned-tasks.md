# Complete assigned Cheesse tasks

> Execute inline in the existing isolated Orca worktree, as requested by the user.

Goal: integrate outstanding branches, satisfy MAT-281/282/283/284/285/287/290 and close MAT-280 after verification.
Architecture: retain the C# core and existing Godot scenes; integrate branch event publication, move history into a shared GameCaretaker, use immutable mementos and PlayMoveCommand, and retain persistence compatibility.
Tech stack: .NET 8, NUnit, Godot .NET 4.7.2. No new dependencies.
Spec: acceptance criteria of the eight Linear issues in project Cheesse.

- [x] Merge avancesLinear (includes avances-f6); preserve current FEN, persistence, repetition and special moves. Adapt branch event tests to current ports and Board.Turn.
- [x] Add failing regression tests for events/order, memento restoration, seeded legal AI, finished states, and the UI's click/shortcut/overlay flow.
- [x] Implement GameMemento, ICommand/PlayMoveCommand and IGameState.AcceptsMoves. GameCaretaker owns undo/redo; preserve ChessGame.TryMove/Undo and history views for existing callers and save reconstruction.
- [x] Extract IPieceMoveRule and one class per piece, with a shared sliding template. Pawn/king strategies delegate special moves without duplicates.
- [x] Seed RandomAI and test it in chess-tests by linking its Godot-free source.
- [x] Clear off-board selections, add Ctrl+Z, refresh overlay after Undo/Redo, block input behind overlay.
- [x] Run all NUnit tests, build solution, run isolated Godot persistence/menu/panel/MVP integration scenes. Check save isolation, loaded history, underpromotion, terminal states and random reply after Undo.
- [x] Review diff with an independent reviewer: no Important/Critical findings remain.

Publication: merge tested commits into origin/main without force, then mark verified issues Done and re-read statuses.

Progress: avancesLinear merge a5d39ff includes avances-f6. Also integrating the previously unpublished local main commit c9cac46 and preserving its original event/architecture tests.

Integration decision: local main's event order and MAT-279's explicit contract take precedence over avancesLinear's different promotion/draw order. Publish PieceMoved, CheckRaised, GameEnded, draw-specific facts, then PromotionOccurred when applicable. The original event tests are retained unchanged.

Review: fixed direct command/caretaker desynchronization with a failing mixed-API regression; fixed historical events during valid/rejected load with a failing persistence regression. The 273 core/AI tests pass. UI verification: menu 35 checks, panel 62, MVP 23 before adding the own-piece selection regression; persistence cross-process save 2/load 246 checks. Final UI rerun follows the selection fix.

Final verification: 273/273 NUnit tests; solution and Godot persistence project build with zero warnings/errors; Godot menu 35, panel 62, MVP 24 checks; cross-process save 2 and load 246 checks. Independent final review found no Important/Critical blockers. The original local main event tests are green without changes. Windows-specific locked-file replacement was not exercised on Linux.
