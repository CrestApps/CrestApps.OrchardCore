using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Registers the built-in tax calculation methods as selectable rule sources so that the "Add tax rule"
/// dialog and the per-source editors are driven by configuration rather than a hard-coded dropdown.
/// </summary>
internal sealed class TaxCalculationMethodOptionsConfiguration : IConfigureOptions<TaxCalculationMethodOptions>
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxCalculationMethodOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxCalculationMethodOptionsConfiguration(IStringLocalizer<TaxCalculationMethodOptionsConfiguration> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public void Configure(TaxCalculationMethodOptions options)
    {
        options.AddMethod(TaxCalculationMethodNames.Percentage, entry =>
        {
            entry.DisplayName = S["Percentage"];
            entry.Description = S["Applies a percentage rate to the taxable base. Use this for sales tax, VAT, and GST."];
        });

        options.AddMethod(TaxCalculationMethodNames.FixedAmount, entry =>
        {
            entry.DisplayName = S["Fixed amount"];
            entry.Description = S["Charges a fixed amount per taxable line, independent of quantity."];
        });

        options.AddMethod(TaxCalculationMethodNames.PerUnit, entry =>
        {
            entry.DisplayName = S["Per unit"];
            entry.Description = S["Charges a fixed amount for every unit of quantity, such as an excise duty."];
        });

        options.AddMethod(TaxCalculationMethodNames.PerWeight, entry =>
        {
            entry.DisplayName = S["Per weight"];
            entry.Description = S["Charges a fixed amount for every unit of weight."];
        });

        options.AddMethod(TaxCalculationMethodNames.PerVolume, entry =>
        {
            entry.DisplayName = S["Per volume"];
            entry.Description = S["Charges a fixed amount for every unit of volume."];
        });

        options.AddMethod(TaxCalculationMethodNames.Progressive, entry =>
        {
            entry.DisplayName = S["Progressive"];
            entry.Description = S["Applies a progressive, tiered calculation driven by a tax table."];
        });

        options.AddMethod(TaxCalculationMethodNames.Threshold, entry =>
        {
            entry.DisplayName = S["Threshold"];
            entry.Description = S["Applies tax only once a taxable threshold is reached, using a tax table."];
        });

        options.AddMethod(TaxCalculationMethodNames.TaxTable, entry =>
        {
            entry.DisplayName = S["Tax table lookup"];
            entry.Description = S["Resolves the rate or amount from a tax table row."];
        });
    }
}
