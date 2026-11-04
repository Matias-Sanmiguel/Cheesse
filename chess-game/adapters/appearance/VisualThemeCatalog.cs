
namespace Cheesse.Game.Adapters.Appearance;

public sealed record SkinOption(string Label, string Folder);

public static class VisualThemeCatalog
{
    public static readonly SkinOption[] Boards =
    [
        new("Tabla de Picada", "01_TablaDePicada"),
        new("Roble Gourmet", "02_RobleGourmet"),
        new("Pizarra Gourmet", "03_PizarraGourmet"),
        new("Mármol Bistro", "04_MarmolBistro"),
        new("Mantel Picnic", "05_MantelPicnic"),
        new("Azulejo Rústico", "06_AzulejoRustico"),
        new("Bambú Zen", "07_BambuZen"),
        new("Corcho Bodega", "08_CorchoBodega")
    ];

    public static readonly SkinOption[] Cheeses =
    [
        new("Brie", "01_Brie_vs_Salame"),
        new("Cheddar", "02_Cheddar_vs_Chorizo"),
        new("Emmental", "03_Emmental_vs_Pepperoni"),
        new("Roquefort", "04_Roquefort_vs_Pastrami"),
        new("Parmesano", "05_Parmesan_vs_Soppressata"),
        new("Cabra con hierbas", "06_CabraHierbas_vs_Morcilla")
    ];

    public static readonly SkinOption[] Charcuterie =
    [
        new("Salame", "01_Brie_vs_Salame"),
        new("Chorizo", "02_Cheddar_vs_Chorizo"),
        new("Pepperoni", "03_Emmental_vs_Pepperoni"),
        new("Pastrami", "04_Roquefort_vs_Pastrami"),
        new("Soppressata", "05_Parmesan_vs_Soppressata"),
        new("Morcilla", "06_CabraHierbas_vs_Morcilla")
    ];

    // Tablero: índice 0 a 7.
    // Equipos: 0 = Aleatorio; 1 a 6 = selección manual.
    public static VisualTheme Create(
        int boardIndex,
        int cheeseIndex,
        int meatIndex,
        Random? random = null)
    {
        random ??= Random.Shared;

        return new VisualTheme(
            Boards[Math.Clamp(
                boardIndex, 0, Boards.Length - 1)].Folder,

            SelectTeam(Cheeses, cheeseIndex, random),

            SelectTeam(Charcuterie, meatIndex, random)
        );
    }

    // Validamos también los valores recuperados
    // de partidas guardadas.
    public static VisualTheme Normalize(VisualTheme? theme)
    {
        if (theme is null)
            return VisualTheme.Classic;

        return new VisualTheme(
            Allowed(Boards, theme.BoardSkin)
                ? theme.BoardSkin : null,

            Allowed(Cheeses, theme.WhitePack)
                ? theme.WhitePack : null,

            Allowed(Charcuterie, theme.BlackPack)
                ? theme.BlackPack : null
        );
    }

    private static bool Allowed(
        SkinOption[] options,
        string? folder)
    {
        return folder is not null &&
            options.Any(option => option.Folder == folder);
    }

    private static string SelectTeam(
        SkinOption[] options,
        int selection,
        Random random)
    {
        int index = selection > 0 && selection <= options.Length
            ? selection - 1
            : random.Next(options.Length);

        return options[index].Folder;
    }
}
