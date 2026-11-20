# CHEESSE

**Trabajo práctico de Ingeniería de Software** — juego de ajedrez de escritorio desarrollado con **Godot 4.7.2 .NET y C# (.NET 8)**.

El proyecto incluye el ajedrez clásico y tres variantes con reglas propias. Se puede jugar entre dos personas en una misma computadora; el modo clásico también permite jugar contra **Stockfish 19**.

## Modos de juego

| Modo | Objetivo y reglas principales |
| --- | --- |
| **Clásico** | Ajedrez tradicional: movimientos FIDE, jaque, jaque mate, enroque, promoción, captura al paso y tablas. |
| **Rey de la Picada** | Además de las victorias habituales, se gana al llevar legalmente el rey a d4, e4, d5 o e5. |
| **Queso Suizo** | Se generan seis agujeros aleatorios que bloquean casillas y trayectorias. Se comprueba que ambos bandos puedan desarrollar sus piezas. |
| **Fondue** | Capturar una pieza que no sea un peón derrite atacante, víctima y piezas no peón adyacentes (3×3, sin reacción en cadena). La casilla queda caliente durante la siguiente jugada rival; solo el caballo puede saltarla sin quemarse. |

El jugador puede consultar las reglas de cada modo desde el menú antes de comenzar la partida.

## Otras funcionalidades

- Selección de tablero, piezas y rival; interfaz pixel art.
- Reloj configurable, historial de movimientos y controles de partida.
- Deshacer/rehacer; guardar y continuar partidas.
- Importación y exportación de posiciones **FEN** y partidas **PGN**.
- Niveles de dificultad para Stockfish en **Clásico**. El motor estándar no se utiliza para las variantes con reglas especiales.

## Requisitos y ejecución desde el código

- **Godot 4.7.2 .NET** (la edición normal de Godot no carga proyectos C#).
- **.NET SDK 8**.
- Windows o Linux x86-64 para la instalación automática de Stockfish.

Desde la raíz del repositorio:

~~~sh
dotnet restore Cheesse.sln
dotnet build Cheesse.sln
~~~

Abrir `chess-game/project.godot` con **Godot .NET** y ejecutar la escena principal con **F5**. Alternativamente, si Godot .NET está en el `PATH`:

~~~sh
godot-mono --path chess-game --editor
~~~

**Stockfish:** no se versiona su ejecutable dentro del repositorio. En la primera partida clásica contra el motor, CHEESSE busca una instalación local y, si hace falta, descarga y verifica la versión oficial para reutilizarla. Esto requiere conexión solamente la primera vez. También se puede configurar `CHEESSE_STOCKFISH_PATH` con la ruta de un ejecutable existente.

## Organización del proyecto

| Directorio | Contenido |
| --- | --- |
| `chess-core/` | Modelo, reglas, casos de uso y contratos; sin dependencias de Godot. |
| `chess-game/` | Escenas, interfaz, assets y adaptadores de Godot, Stockfish y persistencia. |
| `chess-tests/` | Tests unitarios del dominio y de reglas. |
| `chess-game-tests/` | Tests de integración del juego y persistencia en Godot. |
| `scripts/` | Preparación de motores, exportación y pruebas de los paquetes. |
| `third-party/` | Avisos de licencia y procedencia de dependencias externas. |
| `docs/ARQUITECTURA.md` | Justificación del diseño y relación con los conceptos de la materia. |

La solución principal es `Cheesse.sln`. El núcleo se mantiene independiente del motor gráfico para permitir pruebas sin abrir Godot.

## Entregables de Ingeniería de Software

Los cuatro requisitos de la consigna están reunidos en **[Entregables del TPO](docs/ENTREGABLES.md)**: código fuente, diagrama UML de clases, tests unitarios del núcleo y decisiones de diseño en formato *qué / por qué / cuándo se romperían*.

## Pruebas

Pruebas unitarias y de la solución:

~~~sh
dotnet test Cheesse.sln
~~~

Pruebas de integración del juego (requieren Python 3 y Godot .NET; el runner admite indicar la ruta del ejecutable):

~~~sh
python scripts/release.py --engines-only
python chess-game-tests/RunUiTests.py --godot godot-mono --scene MenuTests
python chess-game-tests/RunUiTests.py --godot godot-mono --scene MvpTests
python chess-game-tests/RunUiTests.py --godot godot-mono --scene ReleaseTests
~~~

Las pruebas de interfaz se ejecutan en un entorno aislado y no modifican la partida guardada del usuario. Hay validaciones automatizadas en **GitHub Actions** para .NET y la integración de Godot. Las instrucciones para todas las suites están en [chess-game-tests/README.md](chess-game-tests/README.md).

## Generar un ejecutable

El script de exportación prepara dependencias oficiales verificadas y genera los paquetes para Windows y Linux:

~~~sh
python scripts/release.py --godot godot-mono
~~~

Los resultados se guardan en `dist/` (ignorado por Git). Los paquetes incluyen las dependencias necesarias para ejecutar el juego.

## Licencias de terceros

El código propio del TPO se entrega junto con los recursos del juego. El motor **Stockfish** se distribuye por separado bajo **GPLv3**; su licencia y fuente están documentadas en `third-party/`. La tipografía **Pixelify Sans** conserva su licencia **SIL Open Font License** en `chess-game/assets/ui/fonts/OFL.txt`. También se incluyen los avisos correspondientes a Godot.

Este repositorio contiene el **código fuente del juego de escritorio**; no incluye un servidor web ni un despliegue de Kubernetes.
