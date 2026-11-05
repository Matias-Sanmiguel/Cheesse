namespace Chess.Core.Model;

public enum GameMode
{
    Standard = 0,
    Classic = Standard,
    // Preserve the numeric mode in Nico's existing v1 Picada saves.
    ReyDeLaPicada = 1,
    Fondue = 2,
    // Keep existing serialized mode values stable.
    QuesoSuizo = 3,
}
