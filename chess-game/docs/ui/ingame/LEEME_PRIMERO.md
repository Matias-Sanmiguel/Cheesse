# CHEESSE - Kit de interfaz durante la partida

Este ZIP contiene componentes PNG REALES, independientes, pixel art y con los tamanos del propio archivo. No contiene ni copias de las piezas de ajedrez, ni texturas de casillas, ni el logo ya aprobado.

## Carpeta de assets

- `01_background/`: DOS versiones del mismo entorno, compuestas de forma independiente para 960x540 y 1440x900. Se dibujaron desde cero en lienzos logicos de 480x270 y 480x300 y se ampliaron mediante escalado ENTERO nearest x2 y x3. NO se redujo ni reutilizo la imagen panoramica generada anteriormente.
- `02_board_ui/`: marco del tablero con hueco central EXACTO de 384x384, etiqueta reutilizable de coordenadas, tarjetas de jugador e iconos de queso/fiambre.
- `03_sidebar/`: panel principal, titulo, estados, separadores, historial, scrollbar y botones utilitarios.
- `04_buttons/`: solo dos materiales reutilizables (vino y queso) con 4 estados (idle/hover/pressed/disabled), 48x32 cada uno, pensados para `StyleBoxTexture` 9-slice.
- `05_icons/`: iconos dibujados directamente a 16x16. Solo los seis que requieren presencia extra tienen tambien version 24x24 redibujada a esa resolucion.
- `06_decor/`: diez props PNG transparentes independientes para usar opcionalmente alrededor de la escena.

## Donde estan los archivos finales

**SOLO los PNG que hay dentro de `assets/` deben importarse en el juego.**

`previews/` contiene imagenes DE INSPECCION: una pantalla armada con las piezas y los tiles anteriores (que NO estan en este pack), fondos aislados y hojas visuales; NO son archivos que haya que superponer en Godot.

## Disposicion de referencia (960x540)

- Fondo `01_background/ingame_picada_960x540.png` ocupa toda la pantalla.
- Logo existente se coloca arriba a la izquierda (NO duplicado en este pack).
- Marco `02_board_ui/frame_board_422x422.png`: `x=170, y=75`.
- Tus tiles de 48x48 y tus piezas originales: tablero en `x=189, y=94`; ocupa 384x384. Las coordenadas se colocan aparte.
- Tarjetas de jugador: `x=18`, `y=239` y `313`.
- HUD: `03_sidebar/sidebar_parchment_306x454.png` en `x=629, y=58`.

Los textos de botones, turno, historiales y modo son **dinamicos**. Se muestran en la preview como ejemplo; no estan incrustados en los fondos de botones. Los iconos se insertan por separado para que los controles sean interactivos.

## 9-slice de botones

El borde de los botones es reescalable y mantiene esquinas intactas.

`StyleBoxTexture` / margenes: `left=10, top=9, right=10, bottom=9`.
Los nombres `burgundy_*` y `cheese_*` representan material y estado, no botones distintos o copias.

## Resolucion y filtrado en Godot

Los archivos entregados son PNG RGBA al tamano nativo indicado en `INVENTARIO.csv`.

- En Godot: `CanvasItem > Texture Filter > Nearest`.
- No activar filtrado bilineal para estos assets.
- En resoluciones mayores, priorizar escalados enteros cuando sea posible.
- Para una ventana real de 1440x900 se incluye un fondo creado nativamente con esa proporcion; no hay que estirar el de 960x540.
- Si trabajas con otra resolucion logica, tendras que adaptar posiciones/anchors en la UI, no deformar el PNG.

## Licencia y edicion

Assets originales confeccionados especificamente para esta UI. Las fuentes de texto no se incluyen: usen la fuente pixel art que ya maneja el proyecto.
