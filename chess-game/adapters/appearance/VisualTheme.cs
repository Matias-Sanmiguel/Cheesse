
namespace Cheesse.Game.Adapters.Appearance;
/// Configuración visual de una partida.
/// No modifica las reglas de ajedrez.
public sealed record VisualTheme(
    string? BoardSkin = null,
    string? WhitePack = null,
    string? BlackPack = null)
{
    public static VisualTheme Classic { get; } = new();
}
