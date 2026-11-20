# Diagrama de clases UML — CHEESSE

**Trabajo práctico de Ingeniería de Software** · Modelo obtenido de las clases existentes en el repositorio.

Para que el diagrama se pueda leer sin reducir demasiado los nombres, se muestran **dos vistas del mismo diseño**: el dominio y sus dependencias, y las estrategias que resuelven los movimientos y las variantes. Se representan las relaciones relevantes; se omiten getters, clases de decoración visual, DTO y métodos auxiliares. Los diagramas son **editables en Mermaid** y se visualizan directamente en GitHub.

## 1. Dominio, historial y puertos

```mermaid
classDiagram
direction LR

class ChessGame {
  +Board Board
  +GameStatus Status
  +GameMode Mode
  +TryMove(move: Move) bool
  +Undo() bool
  +Redo() bool
  +CreateMemento() GameMemento
  +Restore(memento: GameMemento)
}

class Board {
  +PlayerColor Turn
  +GameMode Mode
  +Square HotSquare
  +SwissHoles
  +At(square: Square) Piece
  +WithMove(move: Move) Board
  +WithMode(mode: GameMode) Board
  +WithPiece(square: Square, piece: Piece) Board
}

class Piece {
  <<struct>>
  +PieceType Type
  +PlayerColor Color
}
class Square {
  <<struct>>
  +int File
  +int Rank
  +int Index
}
class Move {
  <<struct>>
  +Square From
  +Square To
  +PieceType Promotion
}
class GameCaretaker {
  +Execute(move: Move) bool
  +Undo() bool
  +Redo() bool
}
class ICommand {
  <<interface>>
  +Execute() bool
  +Undo() bool
}
class PlayMoveCommand {
  +Move Move
  +Execute() bool
  +Undo() bool
}
class GameMemento {
  +Board Board
  +History
  +GameStatus Status
}
class IGameState {
  <<interface>>
  +AcceptsMoves bool
}
class GameState {
  <<abstract>>
  +For(status: GameStatus) GameState
}
class IClock {
  <<interface>>
  +ElapsedSeconds double
}
class IEventPublisher {
  <<interface>>
  +Publish(event)
}
class IGameRepository {
  <<interface>>
  +Save(game: ChessGame)
  +Load(game: ChessGame) bool
}
class IAIPlayer {
  <<interface>>
  +ChooseMove(game: ChessGame) Move
}
class GameCompositionRoot {
  +CurrentGame ChessGame
  +StartHumanGame()
  +StartStockfishGame()
}
class GodotEventBus {
  +Publish(event)
}
class FileSaveRepository {
  +Save(game: ChessGame)
  +Load(game: ChessGame) bool
}
class StockfishAI {
  +ChooseMove(game: ChessGame) Move
}

ChessGame "1" --> "1" Board : posicion actual
ChessGame "1" *-- "1" GameCaretaker : undo/redo
ChessGame ..> IGameState : estado
GameState ..|> IGameState
ChessGame ..> IClock : tiempo
ChessGame ..> IEventPublisher : eventos
ChessGame ..> IGameRepository : persistencia

Board "1" *-- "0..64" Piece : casillas
Move "1" *-- "2" Square : origen y destino

GameCaretaker "1" o-- "0..*" PlayMoveCommand : pilas
PlayMoveCommand ..|> ICommand
PlayMoveCommand --> ChessGame : aplica jugada
PlayMoveCommand --> GameMemento : antes y despues
GameMemento --> Board : instantanea

FileSaveRepository ..|> IGameRepository
GodotEventBus ..|> IEventPublisher
StockfishAI ..|> IAIPlayer
GameCompositionRoot --> ChessGame : construye
GameCompositionRoot --> FileSaveRepository : usa
GameCompositionRoot --> GodotEventBus : usa
GameCompositionRoot ..> StockfishAI : turno negro
```

**Lectura:** los objetos de Godot quedan en el extremo de infraestructura. `ChessGame` conoce **interfaces del núcleo**, no `FileSaveRepository` ni `GodotEventBus`. `Board` contiene piezas por valor y las operaciones `With...` generan un nuevo estado.

## 2. Reglas, estrategias y variantes

```mermaid
classDiagram
direction LR

class Board {
  +GameMode Mode
  +WithMove(move: Move) Board
  +IsBlocked(square: Square) bool
}
class MoveGenerator {
  <<static>>
  +Generate(board: Board, side: PlayerColor) Moves
  +Legal(board: Board, side: PlayerColor) Moves
}
class CheckDetector {
  <<static>>
  +IsInCheck(board: Board, side: PlayerColor) bool
}
class IPieceMoveRule {
  <<interface>>
  +AddMoves(board: Board, from: Square, side: PlayerColor, moves: List)
}
class SlidingMoveRule {
  <<abstract>>
  #Directions
  +AddMoves(board: Board, from: Square, side: PlayerColor, moves: List)
}
class PawnMoveRule
class KnightMoveRule
class BishopMoveRule
class RookMoveRule
class QueenMoveRule
class KingMoveRule
class GameRules {
  <<static>>
  +Evaluate(game: ChessGame) GameStatus
}
class IGameModeRules {
  <<interface>>
  +SpecialOutcome(board: Board, lastMover: PlayerColor) GameStatus
  +InsufficientMaterialIsDraw bool
}
class GameModeRuleCatalog {
  <<static>>
  +For(mode: GameMode) IGameModeRules
}
class PicadaModeRules {
  +GoalSquares
  +SpecialOutcome(board: Board, lastMover: PlayerColor) GameStatus
}
class SwissCheeseRules {
  <<static>>
  +Generate(board: Board) Squares
  +ValidateLayout(board: Board, holes: Squares) ulong
}
class FondueRules {
  <<static>>
  +IsMeltingCapture(board: Board, move: Move) bool
  +HotLanding(board: Board, move: Move) Square
}

MoveGenerator ..> Board : inspecciona
MoveGenerator ..> CheckDetector : valida jaque
MoveGenerator "1" o-- "6" IPieceMoveRule : estrategias por tipo
SlidingMoveRule ..|> IPieceMoveRule
PawnMoveRule ..|> IPieceMoveRule
KnightMoveRule ..|> IPieceMoveRule
KingMoveRule ..|> IPieceMoveRule
BishopMoveRule --|> SlidingMoveRule
RookMoveRule --|> SlidingMoveRule
QueenMoveRule --|> SlidingMoveRule
GameRules ..> MoveGenerator : movimientos legales
GameRules ..> GameModeRuleCatalog : resultado especial
GameModeRuleCatalog ..> IGameModeRules : elige
PicadaModeRules ..|> IGameModeRules
Board ..> SwissCheeseRules : agujeros
Board ..> FondueRules : derretimiento y calor
```

**Lectura:** `MoveGenerator` selecciona una implementación de `IPieceMoveRule` según la pieza y después filtra los movimientos que dejarían al rey propio en jaque. `GameModeRuleCatalog` decide condiciones especiales como la victoria de Rey de la Picada. Queso Suizo y Fondue también tienen reglas particulares aplicadas a `Board`.

### Correspondencia con el código

- Dominio: [ChessGame](../chess-core/Model/ChessGame.cs), [Board](../chess-core/Model/Board.cs), [GameMemento](../chess-core/Model/GameMemento.cs).
- Historial: [GameCaretaker](../chess-core/Services/GameCaretaker.cs), [PlayMoveCommand](../chess-core/Services/PlayMoveCommand.cs).
- Reglas: [MoveGenerator](../chess-core/Rules/MoveGenerator.cs), [IPieceMoveRule y sus implementaciones](../chess-core/Rules/PieceMoveRules.cs), [GameModeRuleCatalog](../chess-core/Rules/GameModeRuleCatalog.cs).
- Adaptadores: [GameCompositionRoot](../chess-game/adapters/infra/GameCompositionRoot.cs), [FileSaveRepository](../chess-game/adapters/persist/FileSaveRepository.cs), [GodotEventBus](../chess-game/adapters/infra/GodotEventBus.cs), [StockfishAI](../chess-game/adapters/ai/StockfishAI.cs).

**Convenciones UML:** flecha vacía = implementación o herencia; flecha punteada = dependencia; rombo negro = composición; rombo blanco = agregación. `0..64` representa la cantidad de piezas que puede contener el tablero en el modelo, no el número de tipos de pieza.
