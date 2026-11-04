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

## Tipografía Pixeboy

La interfaz puede cargar automáticamente `assets/ui/fonts/Pixeboy-z8XGD.ttf` para títulos, botones y selectores, priorizando textos en mayúsculas sin tildes. Las frases largas usan la fuente legible de Godot. El archivo fuente que entregó el usuario incluye una nota de **uso personal / no comercial**; no se ha vuelto a publicar el binario en este repositorio público. Para usar la copia personal localmente, extraerla en `chess-game/assets/ui/fonts/Pixeboy-z8XGD.ttf` (ignorado por Git).

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