# CHEESSE — tarjetas de modos de juego (pixel art nativo)

## IMPORTANTE
Este ZIP solo incluye assets NUEVOS de las tarjetas. No incluye piezas, tiles, logo ni fondo de menu.
Los sprites se dibujaron a resolucion nativa: NINGUN asset se obtuvo achicando la imagen del mockup.
La preview 1440x900 esta ampliada por NEAREST x3 y NO es un asset del juego.

## Carpeta
assets/ui/mode_selection/
  cards/            4 tarjetas TERMINADAS 96x112 con titulo y estado, listas para mostrar.
  illustrations/    4 ilustraciones diferenciadas 64x64, independientes.
  components/       tarjeta vacia, plaque, botones, foco, candado, clasico compacto y tooltip.
previews/           imagenes de inspeccion. No importar como sprites.
docs/              inventario de medidas y textos sugeridos.

## Integrar en Godot 4
- Usar `TextureRect` para tarjetas terminadas o `TextureButton` si son clicables.
- `Texture Filter = Nearest` para que el pixel art no se suavice.
- No redimensionar en fracciones: usar escalas enteras x2 / x3 / x4.
- Si queres interaccion, montar desde `components/card_base_96x112.png` +
  `illustrations/<modo>_64x64.png` en posicion (16,15), titulo dinamico y un boton
  dinamico, o usar la tarjeta terminada con un boton invisible por encima.
- `focus_hover_96x112.png` y `focus_selected_96x112.png` son PNG transparentes
  que deben estar encima del panel base solo al interactuar.
- `description_tooltip_160x40.png` admite textos dinamicos desde Godot.
- No agregar `PROXIMAMENTE` como texto encima de las cards de picada, swiss y fondue: YA esta escrito.

## Criterio de diseno
- Marco de madera con cresta de queso, plaque de queso y boton incorporado.
- Cada ilustracion esta dibujada para su modo (no es un recorte del mockup).
- El modo clasico tiene tambien una tarjeta compacta original de 64x80.
- 96x112 es el tamano base pedido para cards; 64x64 para ilustraciones.
- No se repiten piezas ni tiles que ya existian.
