using CrestApps.Core.Infrastructure.Indexing;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Converts between the comma-separated knowledge kinds an editor carries and the array the metadata
/// stores, and names the kinds a reader may choose from.
/// </summary>
/// <remarks>
/// An ingested file is stored as separate typed pieces -- its text, its figures, its charts and its
/// tables -- and this restricts which of those a search may return.
/// </remarks>
public static class KnowledgeObjectTypeEditor
{
    /// <summary>
    /// The kinds a data source may be restricted to.
    /// </summary>
    public static readonly string[] SupportedObjectTypes =
    [
        KnowledgeObjectTypes.Document,
        KnowledgeObjectTypes.Article,
        KnowledgeObjectTypes.Text,
        KnowledgeObjectTypes.Figure,
        KnowledgeObjectTypes.Chart,
        KnowledgeObjectTypes.Table,
    ];

    /// <summary>
    /// Turns the comma-separated kinds an editor carries into the array the metadata stores.
    /// </summary>
    /// <param name="value">The value as typed.</param>
    /// <returns>The kinds, or <see langword="null"/> when nothing was named.</returns>
    /// <remarks>
    /// Naming nothing is not a restriction to nothing: an empty box means every kind, so it returns null
    /// rather than an empty array a reader could mistake for "none of them".
    /// </remarks>
    public static string[] Split(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var objectTypes = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(objectType => objectType.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return objectTypes.Length == 0 ? null : objectTypes;
    }

    /// <summary>
    /// Turns the stored kinds back into the comma-separated value an editor shows.
    /// </summary>
    /// <param name="objectTypes">The stored kinds.</param>
    /// <returns>The kinds as a single value, or <see langword="null"/> when none are stored.</returns>
    public static string Join(string[] objectTypes)
        => objectTypes is { Length: > 0 } ? string.Join(", ", objectTypes) : null;

    /// <summary>
    /// Names the kinds in <paramref name="value"/> that are not kinds anything is stored as.
    /// </summary>
    /// <param name="value">The value as typed.</param>
    /// <returns>The unrecognized kinds, in the order they were typed.</returns>
    /// <remarks>
    /// A misspelled kind matches nothing, so a search restricted to it returns nothing at all. That reads
    /// as an empty knowledge base rather than as a typo, which is why it is worth refusing at the editor.
    /// </remarks>
    public static IReadOnlyList<string> FindUnknown(string value)
    {
        var objectTypes = Split(value);

        if (objectTypes is null)
        {
            return [];
        }

        return objectTypes
            .Where(objectType => !SupportedObjectTypes.Contains(objectType, StringComparer.Ordinal))
            .ToArray();
    }
}
