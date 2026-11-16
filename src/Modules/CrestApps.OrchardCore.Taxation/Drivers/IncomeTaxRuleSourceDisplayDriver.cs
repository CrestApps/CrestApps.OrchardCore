using System.Globalization;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// The editor of how one source's income tax rules are worked out: it builds and updates only rules of
/// <see cref="Source"/>, so each kind of rule shows only its own fields. A country pack adds its own source's editor
/// the same way, as a display driver of <see cref="IncomeTaxRule"/>.
/// </summary>
internal abstract class IncomeTaxRuleSourceDisplayDriver : DisplayDriver<IncomeTaxRule>
{
    internal readonly IStringLocalizer S;

    protected IncomeTaxRuleSourceDisplayDriver(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the source whose rules the driver edits.
    /// </summary>
    protected abstract string Source { get; }

    public override bool CanHandleModel(IncomeTaxRule rule)
        => string.Equals(rule?.Source, Source, StringComparison.OrdinalIgnoreCase);

    // Rates and amounts are typed as text with a dot, so a decimal comma never changes them; the handler checks ranges.
    protected decimal? Number(string text, string field, UpdateEditorContext context)
    {
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

    protected static string Text(decimal? value)
        => value?.ToString("0.##########", CultureInfo.InvariantCulture);

    // A rule keeps only its source's values, so one changed from an older layout carries nothing it doesn't use.
    protected static void ClearAmounts(IncomeTaxRule rule)
    {
        rule.Rate = null;
        rule.RateFromRegistration = false;
        rule.FixedAmount = null;
        rule.TaxTableId = null;
        rule.WageBase = null;
        rule.WageThreshold = null;
        rule.YearlyExemption = null;
        rule.YearlyMaximum = null;
        rule.ShareOfTaxCode = null;
        rule.ShareMultiplier = null;
        rule.Annualize = false;
        rule.UsesElections = false;
        rule.AllowanceAmount = null;
        rule.SupplementalRate = null;
        rule.SupplementalExcessRate = null;
        rule.SupplementalExcessThreshold = null;
        rule.Tables = new Dictionary<string, string>(StringComparer.Ordinal);
        rule.Parameters = new Dictionary<string, decimal>(StringComparer.Ordinal);
    }
}
