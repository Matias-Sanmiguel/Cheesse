# Complete assigned Cheesse tasks

> Execute inline in the existing isolated Orca worktree, as requested by the user.

Goal: integrate outstanding branches, satisfy MAT-281/282/283/284/285/287/290 and close MAT-280 after verification.
Architecture: retain the C# core and existing Godot scenes; integrate branch event publication, move history into a shared GameCaretaker, use immutable mementos and PlayMoveCommand, and retain persistence compatibility.
Tech stack: .NET 8, NUnit, Godot .NET 4.7.2. No new dependencies.
Spec: acceptance criteria of the eight Linear issues in project Cheesse.

- [ ] Merge avancesLinear (includes avances-f6); preserve current FEN, persistence, repetition and special moves. Adapt branch event tests to current ports and Board.Turn.
- [ ] Add failing regression tests for events/order, memento restoration, seeded legal AI, finished states, and the UI's click/shortcut/overlay flow.
- [ ] Implement GameMemento, ICommand/PlayMoveCommand and IGameState.AcceptsMoves. GameCaretaker owns undo/redo; preserve ChessGame.TryMove/Undo and history views for existing callers and save reconstruction.
- [ ] Extract IPieceMoveRule and one class per piece, with a shared sliding template. Pawn/king strategies delegate special moves without duplicates.
- [ ] Seed RandomAI and test it in chess-tests by linking its Godot-free source.
- [ ] Clear off-board selections, add Ctrl+Z, refresh overlay after Undo/Redo, block input behind overlay.
- [ ] Run all NUnit tests, build solution, run isolated Godot persistence/menu/panel/MVP integration scenes. Check save isolation, loaded history, underpromotion, terminal states and random reply after Undo.
- [ ] Review diff, merge tested commits into origin/main without force, then mark verified issues Done and re-read statuses.
