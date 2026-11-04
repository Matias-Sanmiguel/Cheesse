# CHEESSE · Pixel UI v3

Este bloque importa los **91 PNG originales** de `CHEESSE_UI_V3_LIMPIO` en `chess-game/assets/ui/` y `chess-game/assets/fx/`, sin redibujarlos, recomprimirlos ni cambiar su canvas. Las 9 previews del pack son documentación, no sprites de juego, por lo que no se importan.

## Qué cambia

- `PixelUiSkin.cs` aplica los recursos a los controles existentes: fondo del menú 360x225, logo 192x64, cards de modos, botones con estados `idle/hover/pressed/disabled`, paneles 9-slice e indicadores del panel lateral.
- Se respeta `Nearest`. Los botones/paneles se escalan a x2 por `Image.Resize(..., Nearest)` **en memoria** y después usan `StyleBoxTexture` con márgenes de 8 px originales (16 px tras x2).
- El fondo llena la ventana de 1440x900 (escala x4 a esa resolución). En otros tamaños se ajusta con `Nearest`.
- El menú conserva exactamente los nombres y rutas de nodos utilizados por `GameCompositionRoot` y tests. Los cuatro modos aparecen ilustrados, pero solo el ajedrez clásico tiene reglas activas: los otros tres previews son informativos, no botones jugables.
- Se mantienen elección de queso/fiambre/tablero, Aleatorio, Continuar, guardado, deshacer, rehacer y las reglas de `Chess.Core`.

## Fuente Pixeboy para uso personal

La fuente suministrada se llama `Pixeboy-z8XGD.ttf`. El ZIP de la fuente indica **Freeware, Non-Commercial** y **FOR PERSONAL USE ONLY**. Para respetar esa condición, el `.ttf` no se incluye en este repositorio público. `PixelUiSkin` lo detecta opcionalmente en:

`chess-game/assets/ui/fonts/Pixeboy-z8XGD.ttf`

En tu copia local, descomprimí `pixeboy-font.zip` y copiá **solo ese archivo TTF** a dicha carpeta. Godot lo importará al abrir el proyecto y el menú lo utilizará automáticamente para títulos y botones. La ruta está en `.gitignore` para evitar subir accidentalmente el archivo. Si no está presente, Godot muestra su fuente de respaldo sin fallar al cargar la escena. Se usa en textos cortos sin acentos; los textos dinámicos y textos en castellano con tildes quedan en la fuente de respaldo.

## Verificación pendiente en la máquina con Godot .NET

Esta rama se preparó sin una instalación local de `dotnet`/Godot en el entorno de la integración. Antes de mergear:

1. `dotnet build` y `dotnet test` desde la raíz.
2. Abrir Godot .NET, esperar a que importe los PNG y ejecutar `F5`.
3. Menú: ver el fondo de madera, logo, cuatro cards, botones amarillos y los estilos de enfoque/hover.
4. Seleccionar `Picada personalizada`: las cards decorativas se ocultan para que entren los tres selectores y los botones, sin recortar la ventana a 1440x900.
5. Empezar ajedrez clásico, mover `e2-e4`, deshacer/rehacer y guardar/continuar. Hacer lo mismo eligiendo un tablero y quesos/fiambres temáticos.
6. Verificar el HUD lateral de pergamino y los botones reutilizables, y redimensionar a 1280x720.
7. Ejecutar pruebas de UI en Godot con el runner documentado en `chess-game-tests/README.md`.

Esta entrega **no implementa** todavía las reglas de Rey de la Picada, Queso Suizo o Fondue, ni rediseña los diálogos de promoción/fin de partida. Sus assets quedan disponibles para los siguientes PR.