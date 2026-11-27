using CrestApps.Core;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// What Tax Returns keeps on a tax form of the sales tax source, in the form's properties, from the Sales tax return
/// card of the tax form editor: which field is the amount the return says is due.
/// </summary>
public sealed class SalesTaxFormSettings
{
    /// <summary>The field a return's amount due is read from when the form names none.</summary>
    public const string DefaultAmountDueField = "AmountDue";

    /// <summary>
    /// Gets or sets the number of the filing field that is the amount due, after credits, discounts, penalty and
    /// interest, such as 21 or AmountDue. Empty reads <see cref="DefaultAmountDueField"/>.
    /// </summary>
    public string AmountDueField { get; set; }
}

/// <summary>
/// Reads Tax Returns' settings of a tax form.
/// </summary>
public static class SalesTaxFormSettingsExtensions
{
    /// <summary>
    /// Gets Tax Returns' settings of a form: the stored ones, else the defaults.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The settings.</returns>
    public static SalesTaxFormSettings GetSalesTaxSettings(this TaxForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        return form.TryGet<SalesTaxFormSettings>(out var stored) && stored is not null ? stored : new SalesTaxFormSettings();
    }

    /// <summary>
    /// Gets the number of the field a form's amount due is read from: its own, else <c>AmountDue</c>.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The number.</returns>
    public static string AmountDueField(this TaxForm form)
    {
        var field = form.GetSalesTaxSettings().AmountDueField;

        return string.IsNullOrWhiteSpace(field) ? SalesTaxFormSettings.DefaultAmountDueField : field.Trim();
    }
}
