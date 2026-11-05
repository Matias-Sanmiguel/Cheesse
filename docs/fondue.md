# Fondue 2.0 (MAT-298 / MAT-299)

## Reglas definitivas

- Se activa mediante `board.WithMode(GameMode.Fondue)`; las demás variantes y el modo estándar no cambian.
- **Capturar un peón es una captura normal de ajedrez.** El atacante permanece en el destino, no hay derretimiento, y la captura al paso también es normal.
- **Capturar una pieza distinta de un peón** (caballo, alfil, torre o dama) activa Fondue: desaparecen tanto la pieza capturada como la atacante, además de las piezas **que no sean peones** que ocupen cualquiera de las ocho casillas adyacentes al destino, incluidas diagonales. El centro siempre se derrite, aunque la atacante sea un peón que capturó una pieza mayor.
- **Los peones vecinos son inmunes y no propagan nada**. Ninguna pieza derretida inicia otra reacción. El efecto siempre termina en la región de 3×3 centrada en la casilla de captura.
- La propia casilla de origen del atacante queda vacía antes de calcular el radio de 3×3. En los bordes del tablero no existe envolvimiento.
- No se permite una captura que derrita al rey propio, aunque también derrita al enemigo. La captura directa del rey está prohibida; un rey solo puede capturar un peón de manera normal, no capturar una pieza que lo derretiría.
- Derretir al rey rival manteniendo el propio significa victoria inmediata (`KingMelted`). Una amenaza de captura de **no peón** que pueda derretir al rey adversario sin derretir el propio cuenta como jaque indirecto. Capturar un peón junto al rey **nunca** crea un jaque por derretimiento.
- Siguen existiendo jaque directo, mate, ahogado, enroques, promociones y reglas de tablas; material insuficiente admite solo dos reyes solos como tablas en Fondue.

## Nueva regla: calor residual

- Cada **captura que activa derretimiento** (capturar una pieza que no es peón) deja la **casilla de captura vacía y caliente**. Capturar peones no genera calor.
- Se mantiene caliente **durante la única respuesta inmediata del rival**. Si el rival juega a otra casilla, se enfría al terminar esa jugada. No acumula rondas adicionales.
- **Cualquier pieza que termine el movimiento allí se derrite ella sola**, incluidos los peones, las piezas mayores y el caballo. No se extiende a vecinos ni crea otra casilla caliente. La pieza que se sacrifica cuenta como una jugada realizada. No se puede sacrificar el rey: el generador de movimientos lo prohíbe por seguridad.
- Se puede **pasar o saltar por encima** sin riesgo: alfiles, torres, damas y caballos solo reciben el efecto si aterrizan allí. La torre de un enroque también puede derretirse si termina en la casilla caliente, sin afectar al rey en otra casilla.
- Si el rival hace otra captura que activa Fondue durante su respuesta, se enfría el primer lugar y **solo el nuevo destino** queda caliente para la siguiente jugada. Nunca hay más de una casilla activa.
- El calor forma parte del valor inmutable `Board.HotSquare` y se reproduce con Undo/Redo, movimientos legales, guardado y carga. `GameRules.PositionKey` diferencia posiciones con y sin calor activo.

## Interfaz / experiencia de juego

- Al seleccionar una pieza y pasar por encima de un **destino legal que captura una pieza no peón**, el tablero marca las casillas exactas que se derriten. El destino marca el sacrificio del atacante; los peones vecinos no aparecen en el efecto.
- `heat_overlay.png` se muestra sobre la casilla ardiente con pulso naranja y `steam_16x16.png` flotando; si el jugador está por aterrizar allí, el cursor resalta el peligro en rojo. El panel lateral indica la coordenada y aclara que se enfría al terminar la respuesta rival.
- Si una pieza aterriza en esa casilla, `PiecesMelted` publica **una sola pieza**, y el tablero reutiliza `melted_square.png`, `steam_16x16.png` y `fondue_drop_16x16.png`, sin activar una nueva reacción de Fondue.
- Las capturas de peones se muestran como capturas normales. El panel lateral distingue `JAQUE POR FONDUE` únicamente si existe una amenaza real de derretimiento del rey, identificando el objetivo.
- Si el jugador intenta una captura ilegal, el panel explica si la propia captura derretiría su rey o si lo dejaría en jaque.
- `Board.Mode` se conserva con Undo/Redo, mementos, nueva partida, guardado y carga; la UI **nunca** aplica las reglas ni decide qué piezas desaparecerán. `GameRules.MeltingSquares(board, move)` proporciona la vista previa exacta para una jugada que la UI ya verificó como legal.
- `PiecesMelted` se emite **solo** en capturas de una pieza no peón; la lista incluye tanto víctima como atacante (dos entradas para el mismo destino) y vecinos que efectivamente desaparecen. Las capturas normales publican `PieceMoved`, sin `PiecesMelted`.
- El overlay de `KingMelted` se muestra después del efecto visual del tablero. Una promoción que se derrite no publica `PromotionOccurred`.

## Demostraciones / regresiones

1. **Captura normal de peón (sin efecto):** posición
   `rnbqkbnr/pp3pp1/3p3p/2p1P3/8/8/PPPP2PP/RNBQKBNR w KQkq - 0 1`.
   `e5d6` es legal y conserva el peón blanco en d6. `e5e6` tampoco causa un jaque por amenaza a f7, porque f7 tiene un peón.
2. **Derretimiento local sin cadena:** posición
   `7k/8/2p2n2/3rB3/8/8/8/K2R4 w - - 0 1`.
   `d1d5` derrite torre negra d5, torre blanca atacante y alfil blanco e5. El peón c6 sobrevive por inmunidad y el caballo f6 sobrevive porque está fuera del radio de la captura.
3. **Victoria por pieza no peón:** posición
   `7k/6n1/5p2/8/8/8/8/K5R1 w - - 0 1`.
   `g1g7` derrite el caballo negro, la torre blanca y el rey h8; el peón f6 permanece. La misma posición con negras al turno tiene jaque por Fondue: la captura amenazada es g7.

## Compatibilidad

FEN codifica la posición; al cargar una posición de este modo aplicar `WithMode(GameMode.Fondue)`. PGN incluye `[Variant "Fondue"]`. Stockfish estándar desconoce estas reglas: en el menú se habilita humano contra humano, sin activar Stockfish.

**Cambios respecto de Fondue 1.0:** desaparece la propagación ilimitada, los peones vecinos sobreviven, las capturas de peones son normales y el indicador de jaque deja de reaccionar a ellas. Los guardados y partidas históricas que usaban capturas con cadena se interpretarán según el nuevo reglamento; no se mezclan ambos sistemas.

**Guardado:** el PGN reproduce la casilla caliente por historial, y el archivo JSON de guardado incluye `HotSquare` como coordenada o `"-"`, validada al cargar. Los guardados antiguos sin este campo todavía se aceptan. Un FEN convencional de seis campos no codifica el calor temporal; para conservarlo hay que guardar la partida completa (PGN o guardado JSON), no solo su posición FEN.

**Regresión:** captura de caballo en `d5` → `d5` caliente para las negras → torre negra `d7-d5` se derrite sin reacción, o `Rh8-h7` enfría la casilla. Las pruebas también cubren paso de torre por `d5`, sacrificio de rey prohibido, renovación del calor, Undo/Redo, PGN y JSON.

