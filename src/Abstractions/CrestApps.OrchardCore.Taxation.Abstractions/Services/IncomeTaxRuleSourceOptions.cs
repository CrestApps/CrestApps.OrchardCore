using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// The kinds of income tax rule that can be added: Taxation's own (<see cref="IncomeTaxRuleSources"/>) and those a
/// country pack registers with <c>services.AddIncomeTaxRuleSource(...)</c>, listed only while the feature that adds
/// them is on. A rule's source is chosen when it is added and decides which fields its editor shows; each source's
/// fields come from a display driver of <see cref="IncomeTaxRule"/> that handles only its rules.
/// </summary>
public sealed class IncomeTaxRuleSourceOptions
{
    /// <summary>
    /// Gets the sources by name.
    /// </summary>
    public Dictionary<string, IncomeTaxRuleSource> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A kind of income tax rule.
/// </summary>
public sealed class IncomeTaxRuleSource
{
    /// <summary>
    /// Gets or sets the stored name, kept in <see cref="IncomeTaxRule.Source"/>; it never changes.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what people see.
    /// </summary>
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what the kind of rule is for, with examples.
    /// </summary>
    public LocalizationSource Description { get; set; }

    /// <summary>
    /// Gets or sets the method a rule of this source always uses (<see cref="IncomeTaxRule.Method"/>), such as
    /// Percentage or a withholding method's name; <see langword="null"/> when the rule's editor chooses it.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets where the source is listed among the others; lower first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the amount fields a rule of this source holds, by <see cref="IncomeTaxRule"/> property name. The
    /// handler clears the others when a rule is saved or imported, because the calculation reads every field whatever
    /// the source; a share of another tax outside <see cref="IncomeTaxRuleSources.ShareOfTax"/> is refused.
    /// </summary>
    public IReadOnlyList<string> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether a source with a withholding <see cref="Method"/> gets Taxation's
    /// withholding editor (the method's tables and amounts). A pack whose own driver edits its source sets it to
    /// <see langword="false"/>, so the two cards never render or save together.
    /// </summary>
    public bool UsesWithholdingEditor { get; set; } = true;
}
