# Cheesse - In-game UI pixel art (TPO)

This implementation uses the **61 original PNGs** supplied in `CHEESSE_InGame_UI_PixelArt_Assets`, and the `docs/ui/ingame/` files retained from the ZIP. None of the `previews/` images are runtime assets.

## Design and scaling

- UI-only virtual coordinate space: **960 x 540** as in `layout_referencia_960x540.json`.
- A **separate background** is selected from two authored sources: 960x540 for wide windows, 1440x900 for 16:10; the background stretches to fill the viewport using Nearest.
- The foreground uses uniform `min(viewportWidth/960, viewportHeight/540)` scaling and centers itself. It is never stretched separately along x/y.
- The **board itself is not modified**: same `BoardView.cs`, scene, piece textures, tile textures, rules and legal-move indicators. The existing board node is positioned and scaled from its native 96px cells to the **384px opening** of the 422x422 frame. Layout at the virtual resolution: frame `(170,75)`; inner board `(189,94)`, tiles `(48x48)`, sidebar `(629,58)`.
- Rank and file badges are independent decorative sprites behind the board's already-rendered labels. The interactive board continues to receive pointer input in its own transformed coordinates.
- Player cards, status bars, header, action buttons and scrollable history are **separate runtime Godot nodes**, not a flattened preview.

## Real interactions preserved

- Undo, Redo, Save, Load, New Game and Menu are the existing `SidePanelView` events: no duplicated chess logic.
- `Negras aleatorias` remains accessible through the small switch beside the turn bar; its tooltip explains the action.
- Help and settings buttons toggle small in-game popups; the sound utility toggles the Master audio bus.
- The history text updates when `GodotEventBus.StateChanged` is emitted and can scroll.
- Current game mode is **Clásico**. Special modes are *not* implemented in this PR.
- The player cards show **`--:--` deliberately**: the chess engine does not yet implement timed matches, so this UI change does not add fake countdowns or alter game outcomes.

## Resource naming

Original paths are preserved below `res://assets/ingame/`:

- `01_background/`: two independent aspect-specific backgrounds
- `02_board_ui/`: frame, eight reusable coordinate badges, player cards, team badges
- `03_sidebar/`: parchment, ribbon, status bars, history, scroll thumb, utilities
- `04_buttons/`: two materials (burgundy and cheese) with `idle/hover/pressed/disabled` states
- `05_icons/`: independent 16x16 and 24x24 assets
- `06_decor/`: standalone optional decorations (not duplicated over authored background)

Button styles use **Godot `StyleBoxTexture`**, original 48x32 PNG, *left/right 10px*, *top/bottom 9px* nine-slice margins. Text uses the bundled Pixelify Sans weights.

## Validation before merging PR

Run from repository root (Godot .NET and .NET 8 installed):

```powershell
dotnet build
dotnet test
python chess-game-tests/RunUiTests.py --godot godot-mono --scene MenuTests
python chess-game-tests/RunUiTests.py --godot godot-mono --scene SidePanelTests
python chess-game-tests/RunUiTests.py --godot godot-mono --scene MvpTests
```

Manually test at **1440x900** and **1280x720**, game versus human and versus random black, board selection and valid moves, move indicators, Undo/Redo, save/load/continue, history scroll, Help/Settings/Sound buttons and promotion. This UI PR should stay in draft until Godot-level QA passes.