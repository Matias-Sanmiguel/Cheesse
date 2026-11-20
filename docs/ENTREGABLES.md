# Entregables del TPO — Ingeniería de Software

Este es el índice para la entrega de **CHEESSE**. Cada elemento exigido por la consigna está vinculado con su ubicación real en el repositorio.

| Entregable solicitado | Archivo o carpeta | Contenido |
| --- | --- | --- |
| **1. Código fuente completo** | [`Cheesse.sln`](../Cheesse.sln) · [`chess-core/`](../chess-core/) · [`chess-game/`](../chess-game/) | Dominio, reglas, escenas, interfaz, adaptadores y assets. |
| **2. Diagrama de clases (UML)** | [`UML_CLASES.md`](UML_CLASES.md) | Diagramas editables con relaciones, interfaces, composición, herencia y clases que existen en el código. |
| **3. Tests unitarios del núcleo** | [`chess-tests/`](../chess-tests/) · [proyecto de pruebas](../chess-tests/Chess.Tests.csproj) | NUnit: movimientos, jaque, partidas, estados, variantes y arquitectura. |
| **4. Documento breve de decisiones** | [`DECISIONES_DISENO.md`](DECISIONES_DISENO.md) | Qué decidimos, por qué y cuándo dejaría de ser adecuada cada decisión, con referencias a la implementación. |

## Comprobación del núcleo

Desde la raíz del repositorio:

```powershell
dotnet restore Cheesse.sln
dotnet build Cheesse.sln
dotnet test chess-tests/Chess.Tests.csproj
```

En `.github/workflows/dotnet-validation.yml` se ejecutan las verificaciones .NET. Los tests de integración con Godot están separados en [`chess-game-tests/`](../chess-game-tests/); complementan las pruebas del núcleo pero no las sustituyen.

## Organización de la entrega

El código se entrega **en el propio repositorio**. Para generar un ZIP del código fuente sin el historial de Git:

```powershell
git archive --format=zip --output=CHEESSE-TPO.zip main
```

Si el profesor acepta el enlace al repositorio, este índice permite encontrar directamente los cuatro entregables. El UML y el documento de decisiones también pueden guardarse como PDF desde la vista renderizada, si se exige ese formato.

**Documentación complementaria:** [Arquitectura](ARQUITECTURA.md) · [README](../README.md).
