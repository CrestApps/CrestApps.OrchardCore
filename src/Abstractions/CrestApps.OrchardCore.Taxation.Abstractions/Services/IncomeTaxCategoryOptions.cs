using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// The tax categories income is sorted into: the ones every country has (<see cref="IncomeTaxCategories"/>) and those a
/// country pack registers.
/// </summary>
public sealed class IncomeTaxCategoryOptions
{
    /// <summary>
    /// Gets the categories, by stored name, in the order they were added.
    /// </summary>
    public List<IncomeTaxCategory> Categories { get; } = [];
}

/// <summary>
/// A tax category.
/// </summary>
/// <param name="Name">The stored name.</param>
/// <param name="DisplayName">What people see.</param>
public sealed record IncomeTaxCategory(string Name, [property: JsonIgnore] LocalizationSource DisplayName);

/// <summary>
/// The fields of a tax election for jurisdictions, such as a country pack's withholding form (Form W-4, TD1).
/// </summary>
public sealed class ElectionFieldSet
{
    /// <summary>
    /// Gets or sets the jurisdictions it is for: an exact jurisdiction such as US, a prefix ending in a star such as
    /// US-*, or a star alone for any.
    /// </summary>
    public string Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the set's name, such as the form it follows.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets the fields.
    /// </summary>
    public IList<ElectionField> Fields { get; } = [];

    /// <summary>
    /// Whether the set is for a jurisdiction.
    /// </summary>
    /// <param name="jurisdiction">The jurisdiction.</param>
    /// <returns>Whether it is.</returns>
    public bool Matches(string jurisdiction)
        => Jurisdiction == "*"
            || string.Equals(Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase)
            || (Jurisdiction?.EndsWith('*') == true && jurisdiction?.StartsWith(Jurisdiction[..^1], StringComparison.OrdinalIgnoreCase) == true);
}

/// <summary>
/// A field of a tax election.
/// </summary>
/// <param name="Name">The stored name.</param>
/// <param name="Label">The label.</param>
/// <param name="Kind">The kind of value.</param>
public sealed record ElectionField(string Name, [property: JsonIgnore] LocalizationSource Label, ElectionFieldKind Kind)
{
    /// <summary>
    /// Gets the choices of a choice field, by stored value.
    /// </summary>
    public IReadOnlyList<(string Value, LocalizationSource Label)> Choices { get; init; } = [];
}

/// <summary>
/// The kind of value of an election field.
/// </summary>
public enum ElectionFieldKind
{
    /// <summary>
    /// Text.
    /// </summary>
    Text,

    /// <summary>
    /// A whole number.
    /// </summary>
    Integer,

    /// <summary>
    /// An amount, with a dot for decimals.
    /// </summary>
    Amount,

    /// <summary>
    /// Yes or no.
    /// </summary>
    Boolean,

    /// <summary>
    /// One of the choices.
    /// </summary>
    Choice,
}

/// <summary>
/// The election field sets modules register; the most specific one for a jurisdiction is used.
/// </summary>
public sealed class IncomeTaxElectionOptions
{
    /// <summary>
    /// Gets the field sets.
    /// </summary>
    public IList<ElectionFieldSet> FieldSets { get; } = [];

    /// <summary>
    /// Gets the field set of a jurisdiction: an exact one, then the longest prefix, then the one for any.
    /// </summary>
    /// <param name="jurisdiction">The jurisdiction.</param>
    /// <returns>The field set, or <see langword="null"/>.</returns>
    public ElectionFieldSet For(string jurisdiction)
        => FieldSets.Where(set => set.Matches(jurisdiction))
            .OrderByDescending(set => set.Jurisdiction == "*" ? -1 : set.Jurisdiction.EndsWith('*') ? set.Jurisdiction.Length - 1 : 1000)
            .FirstOrDefault();
}
