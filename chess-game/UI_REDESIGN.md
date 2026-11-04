# CHEESSE - rediseño de interfaz (PR #14)

## Qué cambia respecto de la primera versión

La primera versión simplemente reemplazaba StyleBoxFlat por imágenes. Esta revisión reorganiza el menú como un flujo explícito de pantallas y aplica los assets a componentes reales, con tamaños legibles:

1. **Inicio:** logo visible y grande, botones claros para comenzar, continuar, opciones y salir; panorama de los cuatro modos.
2. **Modos:** cuatro tarjetas grandes con ilustraciones y descripción. Solo Clásico permite avanzar, porque las reglas de Rey de la Picada, Queso Suizo y Fondue siguen pendientes de implementación.
3. **Preparar partida:** seleccionar apariencia normal o picada personalizada, tablero de 8 skins, queso blanco entre 6 y fiambre negro entre 6, o Aleatorio como equipo completo. Vista previa de la combinación antes de empezar.
4. **Opciones:** control funcional de sonido (bus Master) e indicaciones de teclado/mouse.
5. **HUD:** marca, estados, iconos, controles e historial de últimas jugadas; presentación adaptable al flujo de partida existente.
6. **Resultado:** panel temático, banner y botones para revancha o menú.
7. **Tablero:** indicadores pixel art de movimiento legal, captura, última jugada, selección y jaque (sin afectar Chess.Core).

## Tamaño y filtros

- Diseño del menú: **1440x900**. El escenario conserva proporciones al bajar a **1280x720** sin cortar controles.
- Assets del pack: pixel art original y PNG idénticos, no se redibujan.
- Filtro: **Nearest**; botón/íconos en x2 y background 360x225 hasta llenar el viewport.
- Paneles y botones: **StyleBoxTexture 9-slice**, sin estiramiento incorrecto de bordes.
- Interacción: los botones no reciben visual de hover al tener foco por teclado, y no se fuerza la selección inicial de ningún botón.

## Tipografía Pixelify Sans (SIL OFL 1.1)

La familia **Pixelify Sans** está incluida permanentemente en `assets/ui/fonts/`: las cuatro variantes TTF originales `Regular`, `Medium`, `SemiBold` y `Bold`. La licencia completa con el copyright oficial está en `assets/ui/fonts/OFL.txt`. No hay instalación manual ni dependencia de fuentes locales.

Jerarquía aplicada de forma consistente en controles dinámicos de Godot:
- **Bold:** títulos grandes de páginas y modales y botones principales del inicio.
- **SemiBold:** botones secundarios, nombres de modos, pequeños encabezados del HUD y texto de cancelar promoción.
- **Medium:** etiquetas de formularios, selectores y desplegables, estados y coordenadas del tablero.
- **Regular:** explicaciones, descripciones, mensajes, instrucciones e historial de jugadas.

El logo y el texto integrado en PNG de diseño son imágenes originales, no se cambian. Los símbolos Unicode de las piezas en la promoción usan la fuente de respaldo porque no están incluidos en Pixelify Sans.


## Conservar el contrato de gameplay

- `GameCompositionRoot` mantiene `MenuView.HumanRequested/RandomRequested/ContinueRequested`.
- `CreateVisualTheme()` funciona sin modificar `Chess.Core`; el catálogo actual se sigue usando.
- Los movimientos, Undo/Redo y guardado/continuar no cambian.
- Los modos especiales no pueden seleccionarse como jugables todavía: activar su mecánica requiere PRs de reglas separados.

## Validación pendiente en Godot .NET

Desde el directorio raíz:

```powershell
git fetch origin
git switch feat/pixel-ui
git pull --ff-only origin feat/pixel-ui
dotnet build
dotnet test
python chess-game-tests/RunUiTests.py --godot 'RUTA_A_GODOT_MONO' --scene MenuTests
python chess-game-tests/RunUiTests.py --godot 'RUTA_A_GODOT_MONO' --scene SidePanelTests
```

Si no hay `python` o el comando exacto cambia, usar el runner explicado en `chess-game-tests/README.md`.

Verificar visualmente en Godot 1440x900 y 1280x720: menús completos, botones sin foco falso, apertura de listas, vista previa de skins, modo bloqueado, movimiento de piezas, captura/jaque, historial, guardar/continuar, audio, y overlays de fin de partida.

**PR en borrador:** no integrar en `main` hasta ejecutar tests y revisar las capturas en Godot.
## 4 de noviembre - menu principal con logo y fondo nuevos
- El home usa exclusivamente los PNG entregados: `assets/ui/logos/logo_cheese.png` (192x64), `assets/ui/logos/logo_cheese_compact.png` (128x32) y `assets/backgrounds/menu_background.png` (360x225).
- Composicion: fondo 360x225 a escala x4, logo principal x4 y cuatro botones centrados **JUGAR / MODOS / CONFIGURACION / SALIR**. No hay fondo ni textos horneados en el logo o en los botones.
- JUGAR abre la preparacion de partida con reglas clasicas; MODOS abre las cuatro tarjetas (tres especiales aun no jugables). CONTINUAR queda disponible en la preparacion, si hay una partida guardada.
- El logo compacto aparece en otras pantallas y en el HUD. Se preservan 8 tableros, equipos independientes, IA aleatoria y guardado.
- La composicion se escala uniformemente desde 1440x900 usando Nearest y mantiene la relacion de aspecto en otras resoluciones. No se importaron los previews del ZIP ni se reescalaron los PNG originales.
