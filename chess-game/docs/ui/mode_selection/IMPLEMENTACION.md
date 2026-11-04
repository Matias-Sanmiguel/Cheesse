# Pantalla de seleccion de modos (96x112, pixel art)

La pantalla `Menu/Stage/ModesPage` se compone a partir del pack original CHEESSE_TARJETAS_MODOS_96x112_ASSETS.

- **Fuente runtime:** componentes/ (card_base, title_plaque, botones por estado, lock_icon, focus_hover y focus_selected), ilustraciones/ (64x64) y Pixelify Sans para todos los textos.
- Las cuatro cartas terminadas en `cards/` son **referencia/alternativa**, NO se dibujan por encima de los controles del juego; eso duplicaria titulo y estado. La variante compacta 64x80 de Clasico tampoco se utiliza.
- Todas las cards: 96x112, presentadas uniformemente a escala 3x, sin deformacion. Las ilustraciones 64x64 se ubican en (16,15) dentro de cada carta; 3x -> 192x192 px.
- Los titulos, `JUGAR`/`PROXIMAMENTE` y descripciones son etiquetas reales de Godot con Pixelify Sans.
- Las tres cartas especiales permanecen **deshabilitadas** hasta que se implementen sus reglas. Clasico abre la preparacion de partida actual.
- El fondo de ladrillos se genera por codigo a resolucion 480x300 y se escala x3 con Nearest para reproducir la atmosfera del mockup sin inventar una imagen externa.
- `focus_hover` y `focus_selected` se muestran solo cuando corresponde; boton con textura hover y pressed tambien usa componentes originales.
- `description_tooltip_160x40` y `classic_compact_64x80` se preservan en assets para futuros usos, sin mostrar decoraciones redundantes.
- No se toca `BoardView`, tablero, piezas, reglas ni otros menus.
