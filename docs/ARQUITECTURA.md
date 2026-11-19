# Arquitectura de CHEESSE

Este documento resume decisiones **implementadas** en el proyecto, relacionadas con los temas de Ingeniería de Software. El juego es una aplicación de escritorio; la infraestructura no forma parte de las reglas de ajedrez.

## Frontera entre dominio e infraestructura

La organización sigue el criterio de **puertos y adaptadores (arquitectura hexagonal)**: las dependencias del juego apuntan hacia el núcleo, no hacia Godot.

~~~text
                       Godot (escenas, interfaz, entrada)
                                   |
                   GameCompositionRoot (composición)
                          /                  \
                 adaptadores              Chess.Core
       Stockfish / archivos / reloj   Model · Rules · Services
                          \                  /
                           Ports (interfaces)
~~~

- `chess-core/Model/`: `Board`, `ChessGame`, `Move`, estados y resultados.
- `chess-core/Rules/`: reglas de movimientos, jaque y variantes.
- `chess-core/Services/`: evaluación de partidas, notación, comandos e historial.
- `chess-core/Ports/`: abstracciones `IGameRepository`, `IClock`, `IEventPublisher` e `IAIPlayer`.
- `chess-game/adapters/`: implementación de las interfaces y representación visual en Godot.

El núcleo no referencia tipos de Godot. Esto se comprueba en `chess-tests/ArchitectureTests.cs`.

## SOLID e inyección de dependencias

- **Responsabilidad única (S):** las reglas están en `Chess.Core`; las escenas y sprites, en `chess-game/adapters/ui`; los archivos guardados, en `FileSaveRepository`.
- **Abierto/cerrado (O):** el contrato `IPieceMoveRule` permite encapsular el comportamiento de cada tipo de pieza y el catálogo `IGameModeRules` separa condiciones especiales de victoria.
- **Inversión de dependencias (D):** `ChessGame` recibe `IEventPublisher`, `IGameRepository` e `IClock` en su constructor, sin crear implementaciones de Godot.
- **Composición sobre herencia:** el comportamiento se combina mediante contratos y adaptadores donde aporta claridad, sin una jerarquía de clases de piezas vinculada a la UI.

Estas decisiones siguen la consigna de preservar un **núcleo testeable sin infraestructura**. No se agregan abstracciones para clases que no las necesitan.

## Patrones utilizados

| Patrón | Evidencia en el proyecto | Para qué se usa |
| --- | --- | --- |
| **Strategy** | `IPieceMoveRule`, `MoveGenerator` y `GameModeRuleCatalog` | Encapsular reglas de movimiento y comportamiento específico de modos. |
| **State** | `IGameState` y `GameState.For` | Determinar si una partida admite nuevos movimientos según su estado. |
| **Command** | `ICommand`, `PlayMoveCommand` y `GameCaretaker` | Representar jugadas y ofrecer deshacer/rehacer. |
| **Memento** | `GameMemento` | Capturar el estado necesario para restaurar partida e historial. |
| **Observer / eventos** | `IEventPublisher` y `GodotEventBus` | Informar cambios de partida sin acoplar el dominio a controles visuales. |

Los patrones se usan para resolver responsabilidades concretas; no reemplazan las reglas del ajedrez.

## Optimización y estructuras de datos

- El tablero almacena **64 casillas** en un arreglo indexado desde `Square`.
- En Queso Suizo, los seis agujeros se representan mediante una **máscara de 64 bits** (`ulong`), para comprobar casillas bloqueadas con operaciones binarias.
- `SwissCheeseRules` emplea `Lazy<ulong[]>` para calcular las distribuciones iniciales seguras solo cuando se necesitan por primera vez.
- Los movimientos candidatos se generan por reglas de pieza y se filtran para impedir que el rey propio quede en jaque.

Son decisiones observables en el código, relacionadas con los contenidos de optimización de CPU y memoria. No se afirman mejoras numéricas sin mediciones.

## Testing y automatización

- **Unitarios (NUnit):** jugadas legales, jaque, enroque, promoción, tablas, modos especiales, historial y formatos FEN/PGN. Se ejecutan sin abrir la interfaz gráfica.
- **Dobles de prueba:** permiten aislar eventos, tiempo y persistencia mediante los contratos del núcleo.
- **Integración (Godot):** escenas reales, selección de modos, controles, visualización, Stockfish y guardados.
- **CI (GitHub Actions):** compilación, pruebas .NET y pruebas de integración con Godot .NET.

El criterio de prueba es **Arrange–Act–Assert**: preparar un estado, ejecutar una acción y comprobar el resultado. Los tests de arquitectura controlan que el núcleo permanezca independiente de Godot.

## Alcance de la entrega

La implementación actual es **Godot + C# + Stockfish local**. No se presenta como realizada una arquitectura de microservicios o Kubernetes; cualquier evolución de infraestructura es independiente del alcance de este código.
