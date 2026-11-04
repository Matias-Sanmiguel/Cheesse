# Fondue (MAT-298)

## Reglas

- Se activa con `board.WithMode(GameMode.Fondue)` antes de construir `ChessGame`.
  El valor por defecto sigue siendo `Standard`.
- Una captura derrite la pieza capturada, la atacante en su destino y las piezas
  de ambos colores en las ocho casillas vecinas (diagonales incluidas).
- Cada pieza derretida propaga el efecto a sus ocho vecinas. Las casillas vacías
  cortan la propagación; cada casilla se procesa una sola vez. No hay inmunidad
  de peones. El origen del atacante ya está vacío cuando se resuelve la cadena.
- Al paso empieza en la casilla de destino y también en la del peón capturado.
  Una promoción que captura se derrite como cualquier otra pieza.
- No se permite derretir el propio rey, ni siquiera si también cae el rival.
  Se mantienen el jaque y la prohibición de capturar directamente al rey; además,
  una captura rival que podría derretir nuestro rey sin perder el suyo es jaque.
  Derretir al rey rival con el propio a salvo gana inmediatamente, aun si había
  otra amenaza. El enroque no puede atravesar una amenaza de derretimiento.
- Mate, ahogado y reglas de tablas por movimientos/repetición siguen vigentes.
  Los finales con piezas menores no usan la regla de material insuficiente del
  ajedrez normal; dos reyes solos sí son tablas.

## Contrato para la UI (Nico, MAT-299)

`Board.Mode` identifica el modo y se conserva al mover y en los mementos.
La vista obtiene destinos de `ChessGame.LegalMoves()` y lee el tablero completo
después de cada jugada; nunca calcula la cadena. `PieceMoved` se publica cuando
la posición final ya está resuelta. En capturas Fondue, `PiecesMelted` aporta la
jugada y la lista inmutable de `(Square, Piece)` eliminadas, incluida la víctima
original y la atacante en su destino (dos entradas en la misma casilla).
Después se publica `GameEnded(GameStatus.KingMelted)` si cayó el rey rival;
`ChessGame.Winner` identifica al ganador. Undo/Redo conserva tablero, modo,
resultado, relojes e historial como una sola jugada. Para nueva partida crear
otro tablero inicial con el modo seleccionado.

El `GodotEventBus` actual ya redibuja con `PieceMoved`/`StateChanged`; para
animar la lista de `PiecesMelted`, MAT-299 debe agregar su puente en el bus.
También debe añadir `KingMelted` al texto de fin de partida y el selector de
modo. Una promoción que se derrite no publica `PromotionOccurred`.

FEN representa solo la posición: al cargar un FEN para este modo aplicar
`WithMode(GameMode.Fondue)`. PGN incluye `[Variant "Fondue"]` y conserva el modo.
Stockfish estándar no conoce Fondue: usar humano contra humano o `RandomAI`.
Las posiciones preparadas de Fondue admiten amenazas indirectas al rey del
jugador que no mueve, para demostrar la victoria; conservan la validación de
reyes, piezas, enroques, al paso y jaque directo del importador.

## Demo reproducible

FEN inicial: `7k/8/5n2/3pB3/8/8/8/K2R4 w - - 0 1`.
En modo Fondue jugar `d1d5`: desaparecen el peón d5, la torre que captura,
el alfil e5 y el caballo f6. La diagonal e5-f6 demuestra la cadena.

Para mostrar victoria: `7k/6p1/5n2/8/8/8/8/K5R1 w - - 0 1`.
Jugar `g1g7`: el derretimiento llega a h8 y gana blancas. Deshacer restaura
el rey negro y rehacer vuelve a mostrar la victoria.

## Decisiones de implementación

Se reutilizan el tablero inmutable, el generador y el caretaker existentes.
Un modo en el tablero hace que las simulaciones, la IA aleatoria y el historial
apliquen las mismas reglas. No se duplica `ChessGame` ni se agrega un segundo
motor; las pantallas quedan en MAT-299.
