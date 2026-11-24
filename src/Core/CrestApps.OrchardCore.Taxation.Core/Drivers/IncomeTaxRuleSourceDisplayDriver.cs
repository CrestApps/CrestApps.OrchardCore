using System.Globalization;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Taxation.Core.Drivers;

/// <summary>
/// The base of the editor of how one source's income tax rules are worked out: it builds and updates only rules of
/// <see cref="Source"/>, so each kind of rule shows only its own fields. A country pack derives its own source's
/// editor from it, registered as a display driver of <see cref="IncomeTaxRule"/>, and registers its source with
/// <c>usesWithholdingEditor: false</c> so Taxation's withholding editor doesn't render beside it.
/// </summary>
public abstract class IncomeTaxRuleSourceDisplayDriver : DisplayDriver<IncomeTaxRule>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IncomeTaxRuleSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer of the derived driver.</param>
    protected IncomeTaxRuleSourceDisplayDriver(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <summary>
    /// Gets the source whose rules the driver edits.
    /// </summary>
    protected abstract string Source { get; }

    /// <inheritdoc />
    public override bool CanHandleModel(IncomeTaxRule rule)
        => string.Equals(rule?.Source, Source, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a rate or an amount typed as text with a dot, so a decimal comma never changes it; the handler checks
    /// ranges, as for a recipe.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="field">The field, for the error.</param>
    /// <param name="context">The editor context.</param>
    /// <returns>The number, or <see langword="null"/> when empty or not a number.</returns>
    protected decimal? Number(string text, string field, UpdateEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        context.Updater.ModelState.AddModelError(Prefix, field, S["Type a number of zero or more, with a dot for decimals."]);

        return null;
    }

    /// <summary>
    /// Writes a number as the editor shows it.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    protected static string Text(decimal? value)
        => value?.ToString("0.##########", CultureInfo.InvariantCulture);

    /// <summary>
    /// Clears every amount of a rule, before the driver sets its own, so a rule never carries a value its kind doesn't
    /// show and the calculation would still read.
    /// </summary>
    /// <param name="rule">The rule.</param>
    protected static void ClearAmounts(IncomeTaxRule rule)
        => IncomeTaxRuleSources.ClearNotOwned(rule, []);
}
