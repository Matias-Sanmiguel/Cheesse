# Cheesse - In-game UI pixel art (TPO)

This implementation uses the **61 original PNGs** supplied in `CHEESSE_InGame_UI_PixelArt_Assets`, and the `docs/ui/ingame/` files retained from the ZIP. None of the `previews/` images are runtime assets.

## Design and scaling

- The **board remains at its original resolution**: 8 x 96px = **768 x 768 screen pixels**, `BoardView.Scale = (1,1)` at every window size. No board, tile, piece or input scaling.
- The reference JSON used a 384px inner frame. Rather than shrink the 768px game, the original **422 x 422 board frame is drawn at exact x2 = 844 x 844**. Its 384px opening becomes 768px. Frame top-left `(206,24)`, board top-left `(244,62)`.
- The surrounding **1440 x 900 desktop layout** is re-composed around that board: left-side team cards, right-side 340x808 HUD at `(1080,42)`, separate 9-slice parchment and scrollable history. Pixel art is nearest-neighbor and the board is untouched.
- The backdrop uses the provided authored sources and fills the window; decorative UI keeps native coordinates on larger windows and is centered without scaling. **At windows smaller than 1440x900, surrounding elements can be cropped rather than shrinking the board** (768px board cannot physically fit into a 720px-high window). The intended play window is at least 1440x900.
- All chess logic, `BoardView.cs`, piece PNGs, tile PNGs, overlays and mouse input remain unchanged.
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