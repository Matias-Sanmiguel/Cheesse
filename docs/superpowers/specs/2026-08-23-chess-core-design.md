# Chess Core — diseño del primer corte jugable

Fecha: 2026-08-23  
Estado: pendiente de revisión del autor  
Alcance: ajedrez legal en capas, jugable en Godot, C# POO. Sin Balatro.

Este documento congela el diseño acordado para el primer producto: un ajedrez de dos jugadores en la misma máquina, con dominio C# puro y Godot como presentación. El código lo escribe quien implementa; este spec define tipos, responsabilidades, firmas e invariantes.

## 1. Objetivo

Una persona puede abrir el juego, jugar una partida de ajedrez contra otra en el mismo PC (hotseat), ver el tablero, seleccionar una pieza, ver destinos legales y mover. Un click ilegal no muta el tablero. El rey no puede quedar en jaque propio.

El mismo modelo tiene que soportar, sin reescribir el agregado `ChessGame`:

- reglas FIDE que faltan, agregadas por capas;
- un oponente no humano vía un puerto;
- más adelante, encuentros de un roguelike tipo Balatro.

## 2. Fuera de alcance

No forma parte de este spec:

- `Run`, encuentros, scoring, jokers, tienda, economía;
- persistencia, cloud save, backend, multiplayer de red;
- contenedor `Microsoft.Extensions.DependencyInjection` (el composition root es explícito con `new`);
- drag-and-drop, animaciones, audio, menús más allá de la escena de juego;
- motor de ajedrez fuerte.

Las carpetas `GameLogic` y `Backend` del repo no se llenan en este corte.

## 3. Decisiones

| Tema | Decisión |
|---|---|
| Entregable | Ajedrez jugable en Godot; dominio C# sin tipos de Godot |
| Quién codea | Quien implementa escribe el código; el acompañamiento es por firmas, invariantes y tests |
| Reglas | FIDE completo por capas: subconjunto jugable primero, especiales después |
| Jugadores | Puerto de jugador; corte 1 = dos humanos. No hay flag `IsInteractive` en una sola interfaz |
| Tests | TDD en dominio (value objects y reglas); Godot a mano + smoke al cerrar el tablero visible |
| Módulos vivos | `ChessLogic` + `Frontend` + tests NUnit + bootstrap mínimo |
| Piezas | Entidad `Piece` sin jerarquía `King : Piece`. El movimiento es `IMoveRule` |

## 4. Arquitectura

Tres capas. Las dependencias apuntan hacia adentro.

```text
Frontend (Godot Nodes + ViewModels)
    -> ChessLogic.Application  (ChessSession, NewGame, PlayMove)
        -> ChessLogic.Domain   (Board, Piece, reglas, ChessGame)
```

Regla de oro: el dominio no referencia `Node`, `Resource`, `Vector2`, `Vector2I`, archivos del motor ni singletons de Godot. Las coordenadas de ajedrez son `Square`.

`Bootstrap` es el único composition root: construye el grafo y entrega el `BoardViewModel` a la escena mediante `Initialize`. Ningún Node resuelve servicios con `Get<T>()` ni `GetNode` para obtener el motor.

```text
GameBootstrap (Autoload)
    ChessComposition.Build()
        MoveRuleSet → ChessRulesEngine → ChessGame
        HumanChessPlayer × 2 → ChessSession → BoardViewModel
    Main.Initialize(viewModel)
```

## 5. Mapa de tipos (dominio)

| Tipo | Rol POO | Hace | No hace |
|---|---|---|---|
| `Side`, `PieceType` | enums | Identificar color y tipo | Comportamiento |
| `Square` | value object | Casilla 0–7, notación algebraica | Saber de piezas |
| `Move` | value object | Origen, destino, promoción opcional | Validar legalidad |
| `PieceId` | value object | Identidad de una pieza | — |
| `Piece` | entidad | Id, color, tipo, `HasMoved` | Calcular movimientos |
| `IBoardView` | interfaz de lectura | Consultar ocupación | Mutar |
| `Board` | entidad | Ocupación e invariantes | Turnos, jaque, legales |
| `IMoveRule` | estrategia | Pseudo-legales y ataques de un tipo | Filtrar jaque |
| `IMoveRuleSet` | catálogo | `For(PieceType) → IMoveRule` | — |
| `ChessRulesEngine` | domain service | Legales, jaque, casillas atacadas | Mutar el tablero real |
| `ChessGame` | aggregate root | Turno, historial, `TryPlay`, estado | Pintar, leer input, poseer jugadores |
| `GameStatus` | value object | En curso / jaque / mate / ahogado | — |
| `MoveResult` | value object | Aceptada o rechazo | — |
| `IChessPlayer` | puerto | Identidad de un lado | Elegir jugadas |
| `IAutonomousChessPlayer` | puerto | Elegir un legal | Conocer Godot |
| `IInteractiveChessPlayer` | puerto | Marca “este lado mueve por UI” | Implementar `ChooseMove` |
| `ChessSession` | application | Une juego + dos jugadores | Reglas de movimiento |

## 6. Firmas e invariantes

### 6.1 Enums y orientación

```csharp
public enum Side { White, Black }
public enum PieceType { King, Queen, Rook, Bishop, Knight, Pawn }
```

`Side` tiene un contrario (`Other()`). Vive como método de extensión o tipo pequeño `Sides`, no como helper suelto en `ChessGame`.

Blancos están en ranks 0–1 (`1–2` algebraico); negros en 6–7 (`7–8`). El peón blanco avanza `rank + 1`.

### 6.2 `Square`

```csharp
public readonly record struct Square
{
    public int File { get; } // 0 = a … 7 = h
    public int Rank { get; } // 0 = 1 … 7 = 8

    public Square(int file, int rank);
    public static Square FromAlgebraic(string notation);
    public string ToAlgebraic();
    public Square? TryOffset(int fileDelta, int rankDelta);
}
```

- Constructor: file/rank fuera de `0..7` → excepción de dominio.
- `FromAlgebraic`: `"e4"` válido; `"e9"`, `"ee"`, vacío → excepción de dominio con mensaje claro.
- `TryOffset`: fuera del tablero → `null`.
- Igualdad por valor.

### 6.3 `Move`

```csharp
public readonly record struct Move
{
    public Square From { get; }
    public Square To { get; }
    public PieceType? Promotion { get; }
}
```

En las capas 0–4, `Promotion` es siempre `null`. La legalidad no vive aquí.

### 6.4 `Piece` y `PieceId`

```csharp
public readonly record struct PieceId(Guid Value);

public sealed class Piece
{
    public PieceId Id { get; }
    public Side Side { get; }
    public PieceType Type { get; }
    public bool HasMoved { get; }

    public Piece(Side side, PieceType type);
    public void MarkMoved();
}
```

- Dos peones blancos son entidades distintas (`Id` distinto).
- No hay `King : Piece`.
- `PromoteTo` no existe hasta la capa de promoción.
- `MarkMoved` lo llama quien aplica la jugada, no la UI.
- `Clone` de `Board` clona cada `Piece` (nuevo objeto, mismo `Id`, mismo `HasMoved` y tipo). El `Id` se conserva para que la vista pueda seguir el sprite.

### 6.5 `IBoardView` y `Board`

```csharp
public interface IBoardView
{
    Piece? PieceAt(Square square);
    bool IsOccupied(Square square);
    bool IsOccupiedBy(Square square, Side side);
    Square FindKing(Side side);
}

public sealed class Board : IBoardView
{
    public Piece? PieceAt(Square square);
    public bool IsOccupied(Square square);
    public bool IsOccupiedBy(Square square, Side side);
    public Square FindKing(Side side);

    public void Place(Piece piece, Square square);
    public Piece Remove(Square square);
    public void Relocate(Square from, Square to);
    public Board Clone();

    public static Board StandardSetup();
}
```

Invariantes (si se rompen → excepción de dominio, no `false`):

1. Una casilla, como máximo una pieza.
2. Una pieza, como máximo una casilla.
3. `Place` sobre ocupada → error.
4. `Remove` / `Relocate` desde vacía → error.
5. `Relocate` hacia ocupada → error. La captura es `Remove(to)` y después `Relocate`.
6. Exactamente un rey por lado. `FindKing` no devuelve `null`.
7. El diccionario interno no se expone.

`StandardSetup`: posición FIDE inicial, 32 piezas, `a1` torre blanca, `e1` rey blanco, `e8` rey negro.

`Clone`: copia profunda de ocupación y piezas. Sirve para simular legales. Apply/undo se reserva para repetición (capa 6).

### 6.6 `IMoveRule`

Ataque y movimiento no son lo mismo. El peón camina adelante y ataca en diagonal. Una pieza protege a una aliada (casilla atacada) aunque no pueda capturarla.

```csharp
public interface IMoveRule
{
    IReadOnlyList<Move> GetPseudoLegalMoves(IBoardView board, Square from);
    IReadOnlyList<Square> GetAttackedSquares(IBoardView board, Square from);
}
```

- Pseudo-legal: geometría correcta y no capturar propia. No mira jaque.
- Si `from` está vacío o la pieza no es del tipo de esa regla → excepción de dominio (bug de quien llamó).
- `IsSquareAttacked` usa solo `GetAttackedSquares`. Nunca llama a movimientos legales (recursión prohibida).

Implementaciones: `PawnMoveRule`, `KnightMoveRule`, `KingMoveRule`. Alfil, torre y dama reutilizan `SlidingMoveRule` con direcciones (composición, no copia).

Peón, capas 1–4: avance 1, avance 2 desde fila inicial, captura diagonal. Sin al paso. Sin destino en la última fila (eso espera promoción).

`IMoveRuleSet.For(PieceType)` es el único lugar que asocia tipo → regla. `ChessRulesEngine` no instancia reglas en cada llamada.

### 6.7 `ChessRulesEngine`

Domain service sin estado de partida.

```csharp
public sealed class ChessRulesEngine
{
    public ChessRulesEngine(IMoveRuleSet rules);

    public IReadOnlyList<Move> GetLegalMoves(Board board, Side sideToMove);
    public IReadOnlyList<Move> GetLegalMovesFrom(Board board, Square from, Side sideToMove);
    public bool IsInCheck(IBoardView board, Side side);
    public bool IsSquareAttacked(IBoardView board, Square square, Side bySide);
}
```

Legales, corte inicial:

1. Pseudo-legales del lado que mueve.
2. Por cada uno, `Clone`, aplicar captura + `Relocate` + `MarkMoved` en el clon.
3. Descartar los que dejan al rey propio en jaque.

Mate y ahogado no se calculan hasta la capa 4. El tipo `GameStatus` ya los nombra.

### 6.8 Estado y resultado de jugada

```csharp
public enum GameStateKind { InProgress, Check, Checkmate, Stalemate }
public enum MoveRejection { GameOver, WrongSide, NotLegal }

public sealed class GameStatus
{
    public GameStateKind Kind { get; }
    public Side? Winner { get; }
    public bool IsOver { get; }
}

public sealed class MoveResult
{
    public bool IsAccepted { get; }
    public MoveRejection? Rejection { get; }
    public GameStatus Status { get; }
}
```

- `Winner` tiene valor solo en `Checkmate`.
- `IsOver` es verdadero en mate y ahogado (y, en capa 6, tablas).
- Jugada ilegal de un jugador → `MoveResult`, nunca `throw`.
- Invariante rota por un bug → excepción de dominio.

Hasta la capa 4, `Kind` es `InProgress` o `Check`. `Checkmate` / `Stalemate` se calculan a partir de “en jaque y sin legales” / “sin jaque y sin legales”.

### 6.9 `ChessGame`

```csharp
public sealed class ChessGame
{
    public IBoardView Board { get; }
    public Side SideToMove { get; }
    public GameStatus Status { get; }
    public IReadOnlyList<Move> History { get; }

    public static ChessGame Standard(ChessRulesEngine rules);

    public IReadOnlyList<Move> LegalMoves();
    public IReadOnlyList<Move> LegalMovesFrom(Square from);
    public MoveResult TryPlay(Move move);
}
```

- El `Board` expuesto es `IBoardView`.
- `TryPlay`: si `Status.IsOver` → `GameOver`; si el movimiento no está en `LegalMoves` → `NotLegal`; si vale, aplica, cambia turno, recalcula `Status`, agrega al historial.
- No posee jugadores. No pregunta “cuál es tu jugada”.
- Aplicación, capas 1–4: si `to` ocupada por rival → `Remove(to)`; `Relocate`; `MarkMoved`; flip de turno. Sin enroque, al paso ni promoción.

`WrongSide` no lo decide `ChessGame` (no conoce jugadores). Lo decide `ChessSession` cuando un humano intenta mover en turno autónomo.

### 6.10 Jugadores (ISP)

Una sola interfaz con `IsInteractive` y `ChooseMove` que a veces devolvía `null` rompía segregación y Liskov. El contrato queda partido:

```csharp
public interface IChessPlayer
{
    Side Side { get; }
}

public interface IInteractiveChessPlayer : IChessPlayer
{
}

public interface IAutonomousChessPlayer : IChessPlayer
{
    Move ChooseMove(IReadOnlyList<Move> legalMoves);
}

public sealed class HumanChessPlayer : IInteractiveChessPlayer
{
    public Side Side { get; }
}
```

`RandomChessPlayer` (capa 7) implementa `IAutonomousChessPlayer` y recibe `IRandomProvider`. No se implementa en el corte 3.

### 6.11 `ChessSession`

```csharp
public sealed class ChessSession
{
    public ChessGame Game { get; }
    public IChessPlayer White { get; }
    public IChessPlayer Black { get; }

    public MoveResult PlayHumanMove(Move move);
    public void AdvanceNonInteractiveTurns();
}
```

- `PlayHumanMove`: si el jugador del turno no es `IInteractiveChessPlayer` → `WrongSide`. Si lo es, `Game.TryPlay`.
- `AdvanceNonInteractiveTurns`: mientras el juego sigue y el jugador actual es `IAutonomousChessPlayer`, `ChooseMove` + `TryPlay`. Con dos humanos no hace nada.
- Godot no instancia `ChessGame` a mano. Habla con `ChessSession` (o con commands que delegan a ella).

## 7. Frontend Godot

El motor no sabe de píxeles. Godot no sabe de jaque. El puente es ViewModel + geometría.

```text
GameBootstrap (Autoload)
Main
  BoardView (Node2D)
    TilesLayer
    HighlightsLayer
    PiecesLayer
  BoardInputController
  HudView
```

Los Nodes se crean por el motor: `Initialize(BoardViewModel vm)`, no constructor con DI.

### 7.1 Geometría

```csharp
public interface IBoardGeometry
{
    Vector2 SquareToLocal(Square square);
    Square? LocalToSquare(Vector2 local);
}
```

Único tipo del corte que puede usar `Vector2`. Click fuera del tablero → `null`, se ignora.

### 7.2 Presentación

```csharp
public sealed class PieceSpriteState
{
    public PieceId Id { get; }
    public Square Square { get; }
    public PieceType Type { get; }
    public Side Side { get; }
}

public sealed class BoardPresentation
{
    public IReadOnlyList<PieceSpriteState> Pieces { get; }
    public Square? Selected { get; }
    public IReadOnlyList<Square> LegalTargets { get; }
    public Move? LastMove { get; }
    public Square? CheckedKingSquare { get; }
    public Side SideToMove { get; }
    public GameStateKind State { get; }
    public MoveRejection? LastRejection { get; }
}
```

La vista sincroniza sprites por `PieceId`, no por “hay un peón en e4”.

### 7.3 ViewModel, vista, input, HUD

`BoardViewModel` guarda selección (estado de UI, no de dominio), expone `Current` y `Changed`, y responde a `OnSquareClicked` / `ClearSelection`. Puede llamar a `ChessSession`. No puede llamar a `Board.Place` ni a `IMoveRule`.

Clicks:

| Click | Efecto |
|---|---|
| Pieza propia | Seleccionar; `LegalMovesFrom` |
| Misma pieza otra vez | Deseleccionar |
| Otra pieza propia | Cambiar selección |
| Destino en `LegalTargets` | `PlayHumanMove` |
| Cualquier otra | Deseleccionar; no llamar al motor |

`BoardView` se suscribe a `Changed`, coloca sprites, pinta highlights. Un diccionario `PieceType + Side → textura` es presentación, no reglas. No lee input.

`BoardInputController` traduce click → `LocalToSquare` → `OnSquareClicked`. No conoce `ChessGame`.

`HudView` muestra turno, jaque/mate/ahogado y `LastRejection`. Solo lee `BoardPresentation`.

## 8. Flujo de una jugada

1. Click en pieza propia → ViewModel pide `LegalMovesFrom` → highlights.
2. Click en destino legal → `PlayHumanMove`.
3. `ChessSession` → `TryPlay` → `MoveResult`.
4. Si no aceptada, el tablero de dominio no cambia; la UI puede mostrar rechazo.
5. Si aceptada, ViewModel refresca ocupación, turno y jaque.
6. `AdvanceNonInteractiveTurns()` (no-op con dos humanos).

## 9. Capas de implementación

Una capa termina (tests verdes o smoke hecho) antes de empezar la siguiente.

| Capa | Dominio | Godot |
|---|---|---|
| 0. Fundación | Enums, `Square`, `Move`, `Piece`, `Board` | Proyecto Godot .NET + escena que compile |
| 1. Pseudo-legales | Seis `IMoveRule` + ataques | — |
| 2. Legales | Engine, clone, jaque, `ChessGame.TryPlay` | — |
| 3. Tablero jugable | `ChessSession` + dos `HumanChessPlayer` | View, VM, click-click, HUD |
| 4. Finales | Mate y ahogado | HUD; no se aceptan más jugadas |
| 5. Especiales | Promoción, después enroque, después al paso | Diálogo de promoción |
| 6. Tablas | 50 movimientos, triple repetición, material insuficiente | Texto de tablas |
| 7. Oponente | `RandomChessPlayer` + `IRandomProvider` | Toggle vs random |

Capa 3 está lista cuando: hotseat funciona, las seis piezas se mueven, no se puede dejar el rey en jaque, un click ilegal no muta el tablero.

Orden TDD dentro de la capa 0: `Square` → `Piece`/`Move` → `Board`.  
Primera `IMoveRule`: caballo. Última de la capa 1: peón.

## 10. Tests

- NUnit sobre proyectos C# puros. Un test de dominio no referencia Godot.
- Value objects y reglas: tests primero (rojo → verde). Tablas de casos, sobre todo peón, jaque y clavadas.
- `Board`: doble ocupación, `Relocate` a ocupada, `StandardSetup`, un rey por lado.
- `ChessGame`: legal, ilegal, captura, cambio de turno, jugada con el juego terminado.
- En dominio se usa `Board` real. No hace falta un `IBoardView` falso para las reglas.
- Godot: smoke manual al cerrar la capa 3. GdUnit queda para después.

## 11. Modo learning

Un turno de trabajo = un tipo (a veces dos si son triviales).

1. Responsabilidad, firma, invariantes, lo que el tipo no debe hacer.
2. Lista de tests (nombres y casos).
3. Quien implementa escribe el código.
4. Si no compila o un test no cierra, se depura juntos. No se entrega la clase implementada de antemano.

## 12. POO y SOLID

El diseño es orientado a objetos (entidades, value objects, encapsulamiento, polimorfismo, composición) y aplica SOLID así:

**Responsabilidad única.** `Square` no mueve. `Piece` no genera destinos. `Board` no sabe de turnos. `IMoveRule` no filtra jaque. `ChessRulesEngine` no muta el tablero real. `ChessGame` no pinta. `ChessSession` no implementa geometría de caballo. `BoardView` no calcula legales.

**Abierto/cerrado.** Una pieza nueva o una regla distinta es otra `IMoveRule` registrada en el set, no un `switch` nuevo en `ChessGame`. Un oponente nuevo es otra implementación de `IAutonomousChessPlayer`. Alfil/torre/dama cierran sobre `SlidingMoveRule` por composición. Los modificadores futuros (Balatro) deben decorar o componer, no heredar de `Piece`.

**Sustitución (Liskov).** No hay jerarquía `King : Piece` que fuerce a tratar un rey como “pieza que a veces no puede”. Todas las `IMoveRule` son intercambiables detrás del set. Un humano no implementa `ChooseMove` devolviendo `null`: no es un jugador autónomo.

**Segregación de interfaces.** `IBoardView` es solo lectura; las reglas no ven `Place`. `IChessPlayer` no obliga a elegir jugadas. Input, geometría y render son interfaces distintas. Godot no depende de `Board` concreto.

**Inversión de dependencias.** El dominio no depende de Godot. El engine depende de `IMoveRuleSet`, no de clases concretas de peón. `ChessSession` depende de `IChessPlayer`. El ViewModel depende de la sesión, no de `PawnMoveRule`. Bootstrap es el único que conoce concreciones.

**Composición antes que herencia.** Pieza = datos + regla. Deslizamiento compartido. Jugadores enchufables. Sin service locator y sin estado global mutable de la partida.

## 13. Relación con el resto del repo

Este spec es el subproyecto “Chess Core + tablero Godot”. Encaja con `docs/technical/STACK.md` y con los hitos Chess Core / Vertical Slice UI del roadmap, recortados: sin `GameLogic`, sin DI de Microsoft todavía, sin persistencia.

El gancho hacia Balatro ya está: dominio sin Godot, `IChessPlayer` / `IAutonomousChessPlayer`, `ChessGame` como agregado que un `Encounter` podrá poseer.
