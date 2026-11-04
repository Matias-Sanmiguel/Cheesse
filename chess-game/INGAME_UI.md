# Cheesse - In-game UI pixel art (TPO)

This implementation uses the **61 original PNGs** supplied in `CHEESSE_InGame_UI_PixelArt_Assets`, and the `docs/ui/ingame/` files retained from the ZIP. None of the `previews/` images are runtime assets.

## Design and scaling

- The **board remains at its original resolution**: 8 x 96px = **768 x 768 screen pixels**, `BoardView.Scale = (1,1)` at every window size. No board, tile, piece or input scaling.
- The reference JSON used a 384px inner frame. Rather than shrink the 768px game, the original **422 x 422 board frame is drawn at exact x2 = 844 x 844**. Its 384px opening becomes 768px. Frame top-left `(206,24)`, board top-left `(244,62)`.
- The frame sprite is rendered at **ZIndex=1**, directly above the board's ZIndex=0: wooden rim and corners remain visible rather than cut by board tiles. Its 384x384 transparent opening (768x768 at x2) leaves every playable square and piece visible. `MouseFilter=Ignore` prevents any hit-test interference. The existing `BoardView`, input logic, themes and piece assets are unchanged.
- **Coordinate regression fix:** the wooden rim at ZIndex 1 hid the badges (which were at ZIndex 0) and the original rank/file text drawn by BoardView. All **16 coordinate badges now render above the frame at ZIndex 2**, and their corresponding labels `1..8`/`a..h` are drawn at **ZIndex 3** with Pixelify Sans Medium at 16px, centered on their existing 44x44 badges. Both badges and text ignore mouse input. The visible chessboard and its rules remain untouched. Godot tests verify all 16 pairs, their layering and input transparency.
- The surrounding **1440 x 900 desktop layout** is re-composed around that board: left-side team cards, right-side 340x808 HUD at `(1080,42)`, separate 9-slice parchment and scrollable history. Pixel art is nearest-neighbor and the board is untouched.
- The backdrop uses the provided authored sources and fills the window; decorative UI keeps native coordinates on larger windows and is centered without scaling. **At windows smaller than 1440x900, surrounding elements can be cropped rather than shrinking the board** (768px board cannot physically fit into a 720px-high window). The intended play window is at least 1440x900.
- All chess logic, `BoardView.cs`, piece PNGs, tile PNGs, overlays and mouse input remain unchanged.
- Rank/file badges are square, exact integer x2 sprites (22→44), aligned to the actual glyph centers using the same font metrics as `BoardView.DrawCoordinates()`. The `BoardView` coordinate drawing, tiles, pieces and pointer conversion remain unchanged.
- Team cards are **128x66 source** nine-patch assets, extended horizontally only to 188x66; their source left icon boxes, top and bottom borders and 32x32 team sprites are not resized.
- The mode and turn ribbons render at their **native 270x28 resolution**. The game status is **a 16x16 dot (green for ongoing, amber for check, red for finished) and simple text**, without a third rectangle.
- The player cards mirror the standard chess orientation: **black above** at y=184, **white below** at y=642. Both retain their source-pixel outlines, aligned icon, player name and functional time on a consistent two-line grid.
- The random-black-opponent `CheckButton` is now accessible through **in-game Settings** (gear icon), away from the turn ribbon. The same control is reparented at runtime: `Toggled`, save/load and ongoing opponent behavior remain connected.
- The interactive board continues to receive pointer input in its own original coordinates.
- Player cards, status bars, header, action buttons and scrollable history are **separate runtime Godot nodes**, not a flattened preview.

## Real interactions preserved

- Undo, Redo, Save, Load, New Game and Menu are the existing `SidePanelView` events: no duplicated chess logic.
- `Negras aleatorias` is in the **top-right settings popup**, not on the turn indicator. Its original signal and persistence behavior are preserved.
- Help and settings buttons toggle small in-game popups; the sound utility toggles the Master audio bus.
- The history text updates when `GodotEventBus.StateChanged` is emitted and can scroll.
- Current game mode is **Clásico**. Special modes are *not* implemented in this PR.
- The player cards now show **real per-player elapsed session time** (`00:00` increasing while that color has the move). Time stops in the menu and after a finished game. There is no invented 10-minute limit or timeout victory rule; session counters are informational and are **not saved**. This avoids displaying a nonfunctional clock.

## Resource naming

Original paths are preserved below `res://assets/ingame/`:

- `01_background/`: two independent aspect-specific backgrounds
- `02_board_ui/`: frame, eight reusable coordinate badges, player cards, team badges
- `03_sidebar/`: parchment, ribbon, status bars, history, scroll thumb, utilities
- `04_buttons/`: two materials (burgundy and cheese) with `idle/hover/pressed/disabled` states
- `05_icons/`: independent 16x16 and 24x24 assets
- `06_decor/`: standalone optional decorations (not duplicated over authored background)

Button styles use **Godot `StyleBoxTexture`**, original 48x32 PNG, *left/right 10px*, *top/bottom 9px* nine-slice margins. Text uses the bundled Pixelify Sans weights.

## Pixel integrity guardrails

- **Never** use independently stretched X/Y dimensions for decorations or icons.
- Native-size or exact integer-scaled `TextureRect` is preferred; when width must grow, use `NinePatchRect`/`StyleBoxTexture` so the frame corners and icon region are preserved.
- `SidePanelTests` now checks original sizes for cards, team icons, bars and coordinate badges, badge alignment, and both per-color counters advancing after a move.
- Two derived card textures (`player_card_black_aligned_128x66.png` and `player_card_white_aligned_128x66.png`) move the **built-in 37x36 icon well by +4px**, exactly matching each 32x32 icon's new position. The original supplied card PNGs remain unchanged for reference. No frames, glyphs, or board sprites are rescaled.
- `BoardView.cs`, `Board.tscn` and all chess resources remain untouched.

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