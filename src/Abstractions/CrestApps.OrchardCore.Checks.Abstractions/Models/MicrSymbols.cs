namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// The four symbols of the E-13B check line, as the Unicode characters that stand for them. A check line font draws
/// them on other characters (<see cref="CheckLayout.MicrSymbolCharacters"/>), which <see cref="ToFont"/> maps.
/// </summary>
public static class MicrSymbols
{
    /// <summary>
    /// The transit symbol, around the routing number.
    /// </summary>
    public const char Transit = '⑆';

    /// <summary>
    /// The amount symbol, around the amount the bank encodes.
    /// </summary>
    public const char Amount = '⑇';

    /// <summary>
    /// The on-us symbol, after the account number and around the auxiliary on-us field.
    /// </summary>
    public const char OnUs = '⑈';

    /// <summary>
    /// The dash symbol.
    /// </summary>
    public const char Dash = '⑉';

    /// <summary>
    /// Writes a check line with the characters a font draws the symbols on.
    /// </summary>
    /// <param name="line">The check line, with the Unicode symbols.</param>
    /// <param name="symbolCharacters">The font's characters for transit, amount, on-us and dash, in that order.</param>
    /// <returns>The text to write with the font.</returns>
    public static string ToFont(string line, string symbolCharacters)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (string.IsNullOrEmpty(symbolCharacters) || symbolCharacters.Length < 4)
        {
            symbolCharacters = "ABCD";
        }

        return string.Create(line.Length, (line, symbolCharacters), static (span, state) =>
        {
            for (var i = 0; i < state.line.Length; i++)
            {
                span[i] = state.line[i] switch
                {
                    Transit => state.symbolCharacters[0],
                    Amount => state.symbolCharacters[1],
                    OnUs => state.symbolCharacters[2],
                    Dash => state.symbolCharacters[3],
                    var other => other,
                };
            }
        });
    }
}
