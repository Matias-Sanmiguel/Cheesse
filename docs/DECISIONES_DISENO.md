# Decisiones de diseño — CHEESSE

**Trabajo práctico de Ingeniería de Software**  
**Alcance:** decisiones que tomamos al desarrollar el ajedrez y sus variantes. Se describen alternativas y límites concretos del proyecto, no definiciones teóricas de patrones.

## 1. Separar las reglas de Godot

**Qué decidimos.** Implementamos las reglas y el estado de partida en `Chess.Core`, un proyecto C# independiente. Godot se ocupa de representar el tablero, tomar entradas y conectar servicios mediante `GameCompositionRoot`.

**Por qué.** Queríamos probar movimientos, jaques y resultados sin abrir el motor gráfico. También evita que una animación o el cambio de una pantalla obligue a modificar reglas del ajedrez.

**Cuándo se rompería.** Si una regla necesitara consultar directamente nodos, sprites o el árbol de escenas, el núcleo dejaría de funcionar por separado. En ese caso extraeríamos ese dato a un contrato del dominio en lugar de introducir referencias a Godot.  
**Evidencia:** `chess-core/`, `GameCompositionRoot.cs`, `ArchitectureTests.cs`.

## 2. Representar el tablero con 64 posiciones y estados nuevos

**Qué decidimos.** `Board` contiene un arreglo de 64 posiciones indexado mediante `Square.Index` y ofrece operaciones `WithMove`, `WithPiece` y `WithMode` que devuelven otro tablero, sin modificar el anterior.

**Por qué.** Para un tablero de 8×8, localizar una casilla es directo. Mantener los estados anteriores facilita simular movimientos, comprobar jaques y recuperar una posición exacta al deshacer.

**Cuándo se rompería.** Si quisiéramos tableros de tamaño variable, miles de piezas o una simulación en tiempo real con altísima frecuencia de movimientos, las 64 posiciones fijas y las copias completas serían limitaciones. Reconsideraríamos la representación y mediríamos memoria/CPU antes de optimizar.  
**Evidencia:** `Board.cs`, `Square.cs`, `BoardImmutabilityTests.cs`.

## 3. Separar movimientos por pieza y validar la seguridad del rey

**Qué decidimos.** `MoveGenerator` usa seis estrategias que implementan `IPieceMoveRule`; primero obtiene candidatos y luego descarta los movimientos que dejan al rey en jaque.

**Por qué.** Los movimientos de un caballo no se calculan como los de una torre. Separarlos permite corregir o ampliar una regla sin concentrar toda la lógica en un único `switch`. La validación de jaque queda en un solo lugar.

**Cuándo se rompería.** Si incorporáramos piezas nuevas con efectos que dependen del historial completo, del tiempo o de varias jugadas simultáneas, la interfaz `AddMoves` y la verificación actual de una jugada por vez podrían quedarse cortas.  
**Evidencia:** `IPieceMoveRule.cs`, `PieceMoveRules.cs`, `MoveGenerator.cs`, `LegalMoveTests.cs`.

## 4. Representar los agujeros de Queso Suizo como una máscara

**Qué decidimos.** Guardamos la disposición de los seis agujeros en un `ulong` de 64 bits. Generamos distribuciones aleatorias simétricas, descartamos aperturas que bloquean el desarrollo y calculamos las disposiciones iniciales seguras mediante `Lazy<ulong[]>` solo cuando hacen falta.

**Por qué.** Cada bit corresponde a una casilla, así que verificar un agujero es una operación binaria. La selección aleatoria mantiene variación entre partidas sin permitir un inicio injugable; `Lazy` evita recalcular todas las aperturas en cada partida nueva.

**Cuándo se rompería.** Si el tablero dejara de tener 64 casillas, si los agujeros cambiaran en cada turno o si la cantidad de disposiciones seguras creciera demasiado, necesitaríamos otra estructura y otra estrategia de generación/caché.  
**Evidencia:** `SwissCheeseRules.cs`, `Board.cs`, `SwissCheeseModeTests.cs`.

## 5. Limitar el derretimiento de Fondue y registrar una sola casilla caliente

**Qué decidimos.** Al capturar una pieza no peón, Fondue derrite atacante, víctima y piezas no peón dentro del área local de 3×3, sin propagación en cadena. La casilla de captura queda caliente durante la respuesta rival y se guarda en `Board.HotSquare`. Solo el caballo puede saltarla sin quemarse.

**Por qué.** La propagación ilimitada hacía que capturas aparentemente simples provocaran jaques difíciles de anticipar y movimientos bloqueados. El efecto local y un único estado temporal son más previsibles, más fáciles de explicar visualmente y de verificar con tests.

**Cuándo se rompería.** Si agregáramos varias casillas calientes a la vez, duraciones distintas o efectos acumulables, un solo `Square?` ya no bastaría; necesitaríamos una colección de efectos con su duración y cambiar guardado, claves de repetición y pruebas.  
**Evidencia:** `FondueRules.cs`, `Board.cs`, `FondueTests.cs`.

## 6. Usar Command y Memento para deshacer y rehacer

**Qué decidimos.** `GameCaretaker` administra pilas de `PlayMoveCommand`, y `GameMemento` conserva tableros, historial y estado necesario para restaurar una partida.

**Por qué.** Las variantes cambian más que la ubicación de las piezas: también importan enroques, relojes, agujeros y calor residual. Restaurar un estado completo reduce el riesgo de deshacer una jugada solo parcialmente.

**Cuándo se rompería.** Si las partidas tuvieran millones de acciones, el historial completo podría consumir demasiada memoria. Si hubiera multijugador remoto, tampoco alcanzaría con deshacer localmente sin acordar un estado con el servidor.  
**Evidencia:** `GameCaretaker.cs`, `PlayMoveCommand.cs`, `GameMemento.cs`, `GameCaretakerTests.cs`.

## 7. Mantener Stockfish como servicio local y opcional

**Qué decidimos.** Integramos Stockfish mediante su protocolo UCI en `StockfishAI`, aislado del núcleo por `IAIPlayer`. Se usa solo en Clásico, ya que el motor estándar no conoce nuestras variantes.

**Por qué.** Podemos ofrecer un rival fuerte sin implementar un algoritmo propio de búsqueda y sin convertir una partida local en una dependencia obligatoria de Internet. El instalador descarga y verifica el motor cuando falta.

**Cuándo se rompería.** Si quisiéramos que Stockfish evaluara Fondue o Queso Suizo, no bastaría con enviarle las posiciones: necesitaríamos un motor compatible con esas reglas. Si buscáramos partidas en línea o cálculos remotos, crearíamos otro adaptador y una API en vez de acoplar red a `ChessGame`.  
**Evidencia:** `IAIPlayer.cs`, `StockfishAI.cs`, `StockfishEngineInstaller.cs`, `StockfishAITests.cs`.

---

Estas decisiones describen el **estado efectivamente implementado**. Las condiciones de ruptura no son fallas actuales: son cambios concretos de requisitos en los que deberíamos revisar la elección inicial. El proyecto no presenta Kubernetes ni servicios remotos como funcionalidades ya desarrolladas.
