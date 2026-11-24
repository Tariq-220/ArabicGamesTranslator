// SPDX-License-Identifier: AGPL-3.0-only
namespace GameTranslator.Translation;

public static class DialogueContext
{
    public static string ForGame(string? game) => DialogueProfile.Rdr2(game ?? "")
        ? "Dialogue in Red Dead Redemption 2, a Western set in 1899. Arthur Morgan, Dutch van der Linde and John Marston are character names. Interpret words using the current scene: physical height, river scenery, horses and period equipment. Do not assume modern phones or internet equipment."
        : game is not null && (game.Contains("Grand Theft Auto",StringComparison.OrdinalIgnoreCase)||game.Replace(" ","").Equals("GTA6",StringComparison.OrdinalIgnoreCase))
        ? "Dialogue in Grand Theft Auto VI, a contemporary crime game in Leonida. Jason and Lucia are character names. Use nearby dialogue to interpret informal speech, speaker intent and pronouns. Gameplay imperatives are instructions; menu labels and phone messages have their on-screen meaning. Preserve the meaning, not a literal word-by-word rendering."
        : string.IsNullOrWhiteSpace(game) ? "Video game dialogue." : "Video game dialogue. Game: " + game;
}
