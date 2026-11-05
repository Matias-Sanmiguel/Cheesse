using Chess.Core.Model;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

/// <summary>
/// Read-only rules board between the mode cards and match setup. Uses the
/// original CHEESSE parchment, gold buttons and the supplied mode logos.
/// The description is intentionally separate from the rules engine.
/// </summary>
internal static class ModeRulesPageBuilder
{
    private const string LogoRoot = "res://assets/ui/logos/modes/alternativo_192x64/";

    private readonly record struct Rules(
        string Logo, string Objective, string Summary, string[] Steps);

    private static readonly Dictionary<GameMode, Rules> RuleCards = new()
    {
        [GameMode.Classic] = new(
            "mode_clasico.png",
            "HACE JAQUE MATE AL REY RIVAL.",
            "EL AJEDREZ DE SIEMPRE. DOS JUGADORES, UN TABLERO.",
            [
                "PIEZAS: cada una tiene sus propios movimientos y capturas.",
                "TURNOS: empiezan las blancas; se juega una vez por turno.",
                "JAQUE: nunca podes dejar a tu rey bajo ataque.",
                "ESPECIALES: enroque, captura al paso y promocion.",
                "FINAL: jaque mate, tablas o tiempo agotado."
            ]),
        [GameMode.ReyDeLaPicada] = new(
            "mode_rey_de_la_picada.png",
            "LLEVA TU REY A D4, E4, D5 O E5 PARA GANAR.",
            "LA PICADA SE GANA CONQUISTANDO EL CENTRO.",
            [
                "META: las cuatro casillas centrales son el objetivo.",
                "REY: debe llegar legalmente a una de esas casillas.",
                "JAQUE: tu rey no puede quedar bajo ataque.",
                "RESTO: las piezas se mueven como en ajedrez clasico.",
                "VICTORIA: ocupar el centro termina la partida."
            ]),
        [GameMode.QuesoSuizo] = new(
            "mode_queso_suizo.png",
            "ESQUIVA LOS AGUJEROS Y HACE JAQUE MATE.",
            "CADA PARTIDA GENERA UN TABLERO DIFERENTE.",
            [
                "AGUJEROS: aparecen aleatoriamente al comenzar.",
                "BLOQUEO: ninguna pieza puede ocupar un agujero.",
                "CAMINOS: torres, alfiles y damas no pueden cruzarlos.",
                "PEONES: tampoco pueden avanzar ni saltar agujeros.",
                "RESTO: jaque, enroque, promocion y tablas normales."
            ]),
        [GameMode.Fondue] = new(
            "mode_fondue.png",
            "GANA CON JAQUE MATE O DERRITIENDO AL REY RIVAL.",
            "LAS CAPTURAS PUEDEN CAMBIAR TODO EL TABLERO.",
            [
                "PEONES: capturarlos funciona como en el ajedrez normal.",
                "FONDUE: otra captura derrite atacante, victima y vecinos.",
                "RADIO: solo piezas no peon en 3X3; sin reacciones en cadena.",
                "CALOR: la casilla queda caliente una respuesta rival.",
                "PELIGRO: solo el caballo salta; cruzar o caer derrite."
            ])
    };

    public static void Build(Control page, Action onBack, Action onContinue)
    {
        page.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        page.AddChild(new TextureRect
        {
            Name = "BrickWall",
            Texture = ModeSelectionPageBuilder.BrickWallTexture(),
            Position = Vector2.Zero,
            Size = new Vector2(1440, 900),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        // Two existing nine-slice skins make a framed wooden rules board.
        // The inner cream surface keeps the pixel text legible at x4.
        page.AddChild(PixelUiSkin.MakePanel("WoodenBoard", "dark",
            new Vector2(148, 100), new Vector2(1144, 742)));
        var parchment = PixelUiSkin.MakePanel("Parchment", "parchment",
            new Vector2(183, 273), new Vector2(1074, 448));
        page.AddChild(parchment);
        parchment.MouseFilter = Control.MouseFilterEnum.Ignore;

        // Native 192x64 assets, displayed at exactly x3; no stretching/cropping.
        page.AddChild(PixelUiSkin.Image("ModeLogo",
            LogoRoot + "mode_clasico.png",
            new Vector2(432, 95), new Vector2(576, 192), 3));

        page.AddChild(PixelUiSkin.Label("Section", "REGLAS DEL MODO",
            new Vector2(235, 300), new Vector2(970, 43), 26,
            pixel: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Bold));

        var objective = PixelUiSkin.Label("Objective", "",
            new Vector2(235, 349), new Vector2(970, 46), 30,
            pixel: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Bold);
        objective.AddThemeColorOverride("font_color", PixelUiSkin.Burgundy);
        page.AddChild(objective);
        page.AddChild(new ColorRect
        {
            Name = "Separator",
            Color = new Color("b57c43"),
            Position = new Vector2(243, 407),
            Size = new Vector2(954, 4),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        for (int i = 0; i < 5; i++)
        {
            int y = 426 + 52 * i;
            var number = PixelUiSkin.Label("Number" + i, (i + 1).ToString("00"),
                new Vector2(240, y), new Vector2(63, 46), 24,
                pixel: true, alignment: HorizontalAlignment.Center,
                weight: UiFontWeight.Bold);
            number.AddThemeColorOverride("font_color", PixelUiSkin.Burgundy);
            page.AddChild(number);
            page.AddChild(PixelUiSkin.Label("Rule" + i, "",
                new Vector2(319, y), new Vector2(880, 46), 23,
                pixel: true, weight: UiFontWeight.Medium));
        }

        page.AddChild(PixelUiSkin.Label("Summary", "",
            new Vector2(272, 714), new Vector2(896, 38), 19,
            pixel: true, light: true, alignment: HorizontalAlignment.Center,
            weight: UiFontWeight.Medium));

        // Reuse the same gold button assets and icon from the main menu.
        page.AddChild(PixelUiSkin.Button("Back", "VOLVER",
            new Vector2(230, 768), new Vector2(340, 62), onBack,
            icon: "back", fontSize: 27, variant: "medium"));
        page.AddChild(PixelUiSkin.Button("Continue", "ENTENDIDO - CONTINUAR",
            new Vector2(666, 768), new Vector2(538, 62), onContinue,
            icon: "play", fontSize: 28, variant: "large"));
    }

    public static void Configure(Control page, GameMode mode)
    {
        if (!RuleCards.TryGetValue(mode, out var rules))
            throw new ArgumentOutOfRangeException(nameof(mode));
        var logo = page.GetNode<TextureRect>("ModeLogo");
        logo.Texture = PixelUiSkin.Texture(LogoRoot + rules.Logo, 3);
        page.GetNode<Label>("Objective").Text = rules.Objective;
        page.GetNode<Label>("Summary").Text = rules.Summary;
        for (int i = 0; i < rules.Steps.Length; i++)
            page.GetNode<Label>("Rule" + i).Text = rules.Steps[i];
    }
}
