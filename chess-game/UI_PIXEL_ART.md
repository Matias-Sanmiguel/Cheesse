# CHEESSE · Pixel UI v3

Este bloque importa los **91 PNG originales** de `CHEESSE_UI_V3_LIMPIO` en `chess-game/assets/ui/` y `chess-game/assets/fx/`, sin redibujarlos, recomprimirlos ni cambiar su canvas. Las 9 previews del pack son documentación, no sprites de juego, por lo que no se importan.

## Qué cambia

- `PixelUiSkin.cs` aplica los recursos a los controles existentes: fondo del menú 360x225, logo 192x64, cards de modos, botones con estados `idle/hover/pressed/disabled`, paneles 9-slice e indicadores del panel lateral.
- Se respeta `Nearest`. Los botones/paneles se escalan a x2 por `Image.Resize(..., Nearest)` **en memoria** y después usan `StyleBoxTexture` con márgenes de 8 px originales (16 px tras x2).
- El fondo llena la ventana de 1440x900 (escala x4 a esa resolución). En otros tamaños se ajusta con `Nearest`.
- El menú conserva exactamente los nombres y rutas de nodos utilizados por `GameCompositionRoot` y tests. Los cuatro modos aparecen ilustrados, pero solo el ajedrez clásico tiene reglas activas: los otros tres previews son informativos, no botones jugables.
- Se mantienen elección de queso/fiambre/tablero, Aleatorio, Continuar, guardado, deshacer, rehacer y las reglas de `Chess.Core`.

## Fuente Pixelify Sans

Las cuatro variantes estáticas originales de **Pixelify Sans** ya están versionadas en `chess-game/assets/ui/fonts/`. Godot las importa al abrir el proyecto sin que nadie tenga que instalar una fuente manualmente.

| Variante | Uso |
| --- | --- |
| `PixelifySans-Bold.ttf` | Títulos grandes, encabezados y JUGAR / MODOS / CONFIGURACION / SALIR |
| `PixelifySans-SemiBold.ttf` | Botones secundarios, nombres de modos, subtítulos e historial (encabezados) |
| `PixelifySans-Medium.ttf` | Etiquetas de formularios, selectores y listas desplegables, estados y coordenadas |
| `PixelifySans-Regular.ttf` | Descripciones, ayudas, mensajes y líneas de historial |

Licencia **SIL Open Font License 1.1** y copyright de 2021 de The Pixelify Sans Project Authors, incluidos en `assets/ui/fonts/OFL.txt`. Se verificó la licencia con la versión de Google Fonts. Las fuentes incluyen los caracteres acentuados, eñes y signos invertidos del español.

Los símbolos Unicode de las piezas de ajedrez en `PromotionWheel` utilizan la fuente de respaldo de Godot por cobertura de glifos; el botón CANCELAR usa Pixelify Sans SemiBold. Los logos y cualquier lettering previamente dibujado en assets PNG permanecen intactos.


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