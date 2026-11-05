# Queso Suizo — reglas de juego

## Objetivo

Jugar una partida de ajedrez completa, con jaque, jaque mate, enroque, al paso,
promociones, reloj y tablas, pero con **agujeros permanentes** que alteran el tablero.

## Agujeros iniciales

- La primera versión usa seis casillas bloqueadas: **c3, f3, d4, e5, c6 y f6**.
- Distribución fija, simétrica al rotar 180°: ambas partes juegan con las mismas
  restricciones desde el inicio.
- Ningún agujero contiene una pieza al comenzar; la posición es determinista para
  que PGN, partidas guardadas, deshacer/rehacer y repetición de posiciones funcionen
  sin añadir aleatoriedad a los archivos.
- Los agujeros no desaparecen y no pueden ser capturados ni ocupados.

## Movimientos y ataques

- Torre, alfil y dama: el primer agujero interrumpe el recorrido, incluso si la
  casilla posterior está libre. Tampoco dan jaque a través del agujero.
- Peón: no puede avanzar, capturar, promocionar ni capturar al paso hacia un agujero.
  El avance inicial de dos casillas tampoco puede saltar uno.
- Rey: no puede aterrizar en un agujero; las reglas normales de jaque y enroque siguen
  vigentes.
- Caballo: puede saltar sobre los agujeros, pero **nunca aterrizar** en uno.
- La condición de victoria es jaque mate (o los finales habituales), no llegar al
  centro ni derretir piezas.

## Implementación

- El dominio decide qué casillas están bloqueadas:
  `chess-core/Rules/SwissCheeseRules.cs` y `Board.IsBlocked`.
- Los generadores de movimiento y ataques aplican el bloqueo sin alterar la
  lógica del modo Clásico, Rey de la Picada o Fondue.
- El tablero usa sin recortes el PNG de UI original
  `res://assets/ui/overlays/hole_overlay.png` (48×48 nativos, x2 en casillas de 96).
- El selector habilita **QUESO SUIZO**, conserva la tarjeta pixel art y abre el
  preparador de partida estándar. Stockfish estándar queda deshabilitado en la
  variante porque desconoce los agujeros.
- Los guardados de versión 2 y los PGN reconocen el modo como `QuesoSuizo = 3`
  y `[Variant "Queso Suizo"]`, manteniendo los valores antiguos de Picada y Fondue.

## Pruebas

`chess-tests/SwissCheeseModeTests.cs`: patrón, legalidad, caminos, jaque,
invariantes, undo/redo, memento y PGN. Integración de UI y persistencia en
`chess-game/tests/MenuTests.cs` y `chess-game-tests/PersistenceTests.cs`.
