# Rey de la Picada — variante jugable

## Regla

La partida conserva el ajedrez clásico, pero un jugador también gana
**inmediatamente al completar una jugada legal con su propio rey** en
una de estas casillas: **d4, e4, d5 o e5**.

- Entrar en jaque está prohibido, incluso si es una casilla objetivo.
- Jaque mate sigue ganando.
- Ahogado, regla de 50 movimientos y triple repetición siguen existiendo.
- Material insuficiente **no** provoca tablas automáticas: dos reyes aún pueden
  competir por el centro.
- El juego no permite seguir moviendo luego de una victoria por centro.

## Identidad visual (sin alterar tablero ni piezas base)

- La tarjeta «Rey de la Picada» queda habilitada en la selección de modos.
- Se abre el setup con tema de picada predeterminado, personalizable.
- En partida aparece un distintivo de corona con el objetivo.
- Las cuatro casillas objetivo se señalan con
  `assets/ui/overlays/goal_square.png` (48×48 a x2) y un pulso suave de 8 fps.
- Cuando un rey alcanza legalmente el centro salen pequeñas partículas de
  `assets/fx/sparkle_16x16.png` y `assets/fx/cheese_crumb_8x8.png`.
- El modal de victoria espera 0,72 s para que se vea el efecto y después
  muestra el ganador y «REY AL CENTRO», con corona y aparición gradual.
- Clásico no muestra estas señales ni efectos.

## Arquitectura

- Reglas de variantes: `Chess.Core.Model.GameMode`,
  `Chess.Core.Rules.IGameModeRules`, `GameModeRuleCatalog` y
  `PicadaModeRules` (Strategy).
- Estado final específico: `GameStatus.PicadaVictory`.
- `ChessGame` y `GameMemento` conservan el modo para mover,
  restaurar, deshacer y rehacer.
- `FileSaveRepository` serializa `Mode` y reproduce el PGN con reglas
  de la variante. Guardados anteriores usan `Classic` por omisión.
- `MenuView` selecciona modo independientemente de la apariencia.
- `BoardView` solo representa objetivos y efectos; no decide legalidad.

## Pruebas

- `chess-tests/PicadaModeTests.cs`: blancos/negras, casilla atacada,
  mate clásico, escasez de material, Undo/Redo/Restore, PGN.
- `chess-game-tests/PersistenceTests.cs`: guardado y carga con victoria por centro.
- `chess-game/tests/MenuTests.cs`: acceso a modo, setup,
  overlay, HUD y regreso a clásico.

Requiere ejecutar validación en Godot .NET 4.7.2 y `dotnet test Cheesse.sln`.
