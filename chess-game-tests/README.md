# Pruebas de integración

Este directorio contiene las pruebas que requieren **Godot .NET** y las escenas reales del juego. Los tests unitarios del dominio están en `chess-tests/`.

## Requisitos

- Godot **4.7.2 .NET** y SDK **.NET 8**.
- Python 3 para el runner multiplataforma.
- Para las pruebas contra el motor real: preparar Stockfish con `python scripts/release.py --engines-only`, desde la raíz del repositorio.

## Interfaz y jugabilidad

Desde la raíz del proyecto, con el ejecutable de Godot .NET en el `PATH`:

~~~sh
python chess-game-tests/RunUiTests.py --godot godot-mono --scene MenuTests
python chess-game-tests/RunUiTests.py --godot godot-mono --scene SidePanelTests
python chess-game-tests/RunUiTests.py --godot godot-mono --scene MvpTests
python chess-game-tests/RunUiTests.py --godot godot-mono --scene ReleaseTests
~~~

En Windows se puede pasar la ruta completa a Godot .NET:

~~~powershell
./chess-game-tests/RunMenuTests.ps1 -Godot "C:/ruta/Godot_console.exe" -TestScene MenuTests
~~~

El runner compila el proyecto y ejecuta la escena de prueba dentro de un entorno aislado (guardados y caché independientes). Los tests verifican navegación, movimientos, controles, estados de partida y motor Stockfish.

## Persistencia

Para comprobar el guardado y la carga entre procesos diferentes:

~~~sh
dotnet build chess-game-tests/Chess.Game.PersistenceTests.csproj
godot-mono --headless --path chess-game-tests -- --save
godot-mono --headless --path chess-game-tests -- --load
~~~

También se puede ejecutar la suite en un único proceso con `-- --run`.

## Pruebas unitarias

~~~sh
dotnet test Cheesse.sln
~~~

Los workflows de GitHub Actions ejecutan las comprobaciones de .NET y de interfaz antes de integrar cambios. Ver [README principal](../README.md).
