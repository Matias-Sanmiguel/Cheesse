# Pantalla de reglas por modo — CHEESSE

## Recorrido de navegación

Inicio (JUGAR) → Modos (VER REGLAS) → Reglamento del modo → Preparación de partida → Jugar.

- **Volver** desde el reglamento regresa a la lista de modos.
- **Entendido - Continuar** desde el reglamento pasa a personalizar tablero, piezas y rival.
- **Volver** desde la preparación regresa al reglamento del modo seleccionado (no salta la explicación).
- ESC realiza los mismos retrocesos: preparación → reglas → modos → inicio.
- Cargar una partida guardada continúa funcionando desde la preparación, sin reiniciar el estado.

## Aspecto

Se reutilizan los paneles `dark_9slice_32x32.png` y `parchment_9slice_32x32.png`, los botones dorados y sus iconos, la tipografía Pixelify Sans y el fondo del selector de modos. No se alteran los PNG de los elementos originales.

Los cuatro logos del pack del usuario están en `chess-game/assets/ui/logos/modes/alternativo_192x64/` y se dibujan en escala entera ×3 (576×192), con filtro *nearest* y proporciones originales.

Cada modo usa una ficha legible con una condición de victoria y cinco reglas resumidas. Estas explicaciones son contenido de UI, no sustituyen ni duplican la implementación de reglas de `Chess.Core`.

## Mantenimiento y pruebas

El contenido está centralizado en `ModeRulesPageBuilder` y se actualiza en `Configure` cuando el jugador elige tarjeta. `MenuView.SelectedMode` sigue siendo la fuente de verdad para la configuración y el inicio de partida. `MenuTests` valida los cuatro logos, las instrucciones y la navegación; `ReleaseTests` valida el flujo con clics reales y produce capturas de los reglamentos.

Dependencia: este cambio se basa en `feat/fondue-playable-mode` (PR #23), porque Fondue todavía no está fusionado a `main`. Fusionar #23 antes de cambiar el PR de esta pantalla a base `main`.
