using System.Globalization;

namespace CrestApps.OrchardCore.KnowledgeBase.Migrations;

/// <summary>
/// The positions of the Knowledge Article type's parts. Orchard Core's content type editor reads a part's position as a
/// whole number, so each is one.
/// </summary>
internal static class KnowledgeArticlePartPositions
{
    /// <summary>
    /// The part names in the order the editor shows them: the title, who may read it and the summary, the category field
    /// (on the type's own part), the body and the permalink.
    /// </summary>
    public static readonly string[] Order =
    [
        "TitlePart",
        KnowledgeBaseConstants.PartName,
        KnowledgeBaseConstants.ArticleType,
        "MarkdownBodyPart",
        "AutoroutePart",
    ];

    /// <summary>
    /// Gets the position a new Knowledge Article type gives a part.
    /// </summary>
    /// <param name="partName">The part's name.</param>
    /// <returns>Its position.</returns>
    public static string Of(string partName)
        => (Array.IndexOf(Order, partName) + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Gives the parts whole, distinct positions in the order they are shown, where one isn't a whole number (such as
    /// the "2.5" an earlier version gave the type's own part) or two share one; parts in the same place keep the type's
    /// usual order. Positions that are already fine are left as they are.
    /// </summary>
    /// <param name="parts">Each part's name and position.</param>
    /// <returns>The new position of each part, or nothing when there is nothing to change.</returns>
    public static IReadOnlyDictionary<string, string> Renumber(IReadOnlyCollection<(string Name, string Position)> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var whole = parts.Select(part => int.TryParse(part.Position, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : (int?)null).ToList();

        if (whole.All(value => value is not null) && whole.Distinct().Count() == whole.Count)
        {
            return new Dictionary<string, string>();
        }

        return parts
            .Select((part, index) => (part.Name, Place: decimal.TryParse(part.Position, NumberStyles.Number, CultureInfo.InvariantCulture, out var place) ? place : decimal.MaxValue, Usual: UsualIndex(part.Name), index))
            .OrderBy(part => part.Place)
            .ThenBy(part => part.Usual)
            .ThenBy(part => part.index)
            .Select((part, index) => (part.Name, Position: (index + 1).ToString(CultureInfo.InvariantCulture)))
            .ToDictionary(part => part.Name, part => part.Position, StringComparer.Ordinal);
    }

    private static int UsualIndex(string partName)
    {
        var index = Array.IndexOf(Order, partName);

        return index < 0 ? Order.Length : index;
    }
}
