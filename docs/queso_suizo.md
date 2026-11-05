# Queso Suizo — reglas y generación de tableros

## Objetivo

Jugar ajedrez con las reglas habituales de jaque, jaque mate y tablas, pero
con **seis agujeros permanentes y aleatorios** que cambian de posición entre
partidas nuevas.

## Generación segura del tablero

- Al iniciar cada partida nueva, se elige al azar un patrón de **seis casillas**;
  no son siempre c3, f3, d4, e5, c6 y f6.
- Los agujeros aparecen en las filas **3 a 6**, nunca debajo de las piezas que
  ocupan sus posiciones iniciales. Nunca se superponen con una pieza.
- Se agrupan en tres pares rotados 180°: ambos jugadores enfrentan la misma
  dificultad relativa.
- Se descartan patrones que dejen menos de **cuatro columnas totalmente
  transitables**, bloqueen más de dos entradas inmediatas de peones por lado o
  reduzcan el desarrollo de las piezas por debajo del umbral de seguridad.
- El generador valida las jugadas legales **de ambos bandos** desde el inicio:
  cada uno debe disponer de al menos **14 movimientos**, al menos **seis peones
  distintos con salida** y ambos caballos con alguna jugada posible.
- El generador evita repetir el patrón inmediatamente anterior durante la misma
  ejecución del juego, incluso al iniciar una revancha.
- La distribución permanece fija durante una partida, incluso al deshacer,
  rehacer, reanudar o cargar un guardado.

Esto asegura aperturas jugables y con varias alternativas; no pretende impedir
las posiciones de ahogado o mate que puedan aparecer **durante** una partida.

## Movimientos

- Torres, alfiles y damas: un agujero interrumpe el recorrido, incluso si las
  casillas posteriores están vacías. Sus rayos de ataque también se cortan.
- Peones: no pueden avanzar, capturar, promocionar ni capturar al paso hacia un
  agujero; el avance de dos pasos tampoco puede saltar uno.
- Rey: no puede entrar en un agujero; mantiene las reglas de jaque y enroque.
- Caballos: sí pueden **saltar sobre agujeros**, pero no aterrizar en ellos.
- Jaque mate, tablas, relojes y deshacer/rehacer mantienen sus reglas habituales.
- Por ahora se juega entre **dos personas**. Stockfish estándar no conoce los
  agujeros y permanece deshabilitado.

## Persistencia y arquitectura

- El tablero guarda la configuración mediante una máscara inmutable de 64 bits,
  propagada a todas las copias de `Board` y a los mementos.
- `chess-core/Rules/SwissCheeseRules.cs` genera y valida las distribuciones
  seguras. `Board.WithSwissHoles(...)` permite reconstruir una distribución
  exacta sin volver a sortear.
- Los PGN incluyen `[Variant "Queso Suizo"]` y
  `[SwissHoles "a3,b4,..."]` (en orden canónico). Guardar/cargar internamente
  usa el PGN para restaurar las casillas con precisión.
- Exportar a FEN estándar en este modo devuelve un aviso en vez de eliminar
  silenciosamente los agujeros; para compartir una partida usá **PGN**.
- Los PGN antiguos de Queso Suizo **sin** `SwissHoles` siguen cargando su patrón
  original fijo `c3,f3,d4,e5,c6,f6`.
- El cálculo de repetición distingue el modo y la distribución de agujeros.
- La UI usa el asset **original** `res://assets/ui/overlays/hole_overlay.png`
  (48×48 a escala x2) sin modificar el PNG ni los gráficos de las tarjetas.
- No se cambian reglas ni assets de Clásico, Rey de la Picada o Fondue.

## Pruebas

- `chess-tests/SwissCheeseModeTests.cs`: distribución aleatoria, ausencia de
  repeticiones consecutivas, simetría, alternativas legales, movimientos,
  ataques, enroque, deshacer/rehacer, PGN y compatibilidad antigua.
- `chess-game-tests/PersistenceTests.cs`: guardar y cargar patrón aleatorio
  exacto, reloj e historial, incluso después de deshacer/rehacer.
- `chess-game/tests/ReleaseTests.cs` y `MenuTests.cs`: UI y partida jugable.

**Validación pendiente:** ejecutar tests .NET y la escena Godot para revisar el
equilibrio visual y el gameplay antes de integrar el PR.
