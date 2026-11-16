namespace CrestApps.OrchardCore.ContentFields.Conditions;

/// <summary>
/// Describes a source a field's rule can read: what the settings editor offers and what the browser reads.
/// </summary>
public sealed class ContentConditionSourceDescriptor
{
    /// <summary>
    /// Gets or sets the source's key, as a condition names it: <c>{TypePartName}.{FieldName}</c> for a field, or the
    /// name a module gives its value (no dot).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown in the settings editor.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what kind of value it is: a field's type name (<c>BooleanField</c>, <c>TaxonomyField</c>...), so the
    /// browser knows how to read its editor, or a module's own kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the name the editor posts the value under, relative to the editor's HTML prefix, such as
    /// <c>Ticket.Outage</c> or <c>TicketPart.CategoryId</c>; <see langword="null"/> when the browser can't read it.
    /// </summary>
    public string InputName { get; set; }

    /// <summary>
    /// Gets or sets whether its values sit in a tree, so <c>IsOrUnder</c> applies.
    /// </summary>
    public bool IsHierarchical { get; set; }

    /// <summary>
    /// Gets or sets the values to choose from in the settings editor, or none when any text is accepted.
    /// </summary>
    public IList<ContentConditionOption> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets, for a hierarchical source, the values above each value (parent first), so the browser can apply
    /// <c>IsOrUnder</c> as the server does.
    /// </summary>
    public IDictionary<string, string[]> Ancestors { get; set; } = new Dictionary<string, string[]>(StringComparer.Ordinal);
}

/// <summary>
/// A value a condition can compare with, as the settings editor offers it.
/// </summary>
/// <param name="Value">The stored value, in the invariant culture.</param>
/// <param name="Text">What it reads as.</param>
/// <param name="Level">Its depth in a tree, 0 at the top.</param>
public sealed record ContentConditionOption(string Value, string Text, int Level = 0);
