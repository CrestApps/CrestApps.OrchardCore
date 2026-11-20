using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tickets.Core.Handlers;

/// <summary>
/// The badge colors a status or a priority may have: a hex color (<c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c> or
/// <c>#rrggbbaa</c>) or a CSS color name, nothing that could carry more CSS into the style it is written in.
/// </summary>
public static partial class TicketColors
{
    /// <summary>
    /// Returns whether a color may be kept.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>Whether it is a hex color or a color name.</returns>
    public static bool IsValid(string color)
        => !string.IsNullOrEmpty(color) && Pattern().IsMatch(color);

    [GeneratedRegex("^(#([0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})|[a-zA-Z]{3,30})$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
