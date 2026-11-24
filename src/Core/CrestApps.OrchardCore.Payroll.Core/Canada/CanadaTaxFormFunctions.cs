using CrestApps.OrchardCore.Payroll.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Payroll.Core.Canada;

/// <summary>
/// The functions Canada Payroll Filings adds to the formulas of tax forms: DENTALCODE(), the employer's dental benefits
/// code (T4 box 45) from its Canada card.
/// </summary>
public sealed class CanadaTaxFormFunctions : ITaxFormFunctionProvider
{
    /// <inheritdoc />
    public string Source => PayrollConstants.TaxFormSource;

    /// <inheritdoc />
    public IEnumerable<ExpressionFunction> GetFunctions()
    {
        yield return new ExpressionFunction
        {
            Name = "DENTALCODE",
            Category = PayrollTaxFormFunctions.Category,
            Signature = "DENTALCODE()",
            Description = "The employer's dental benefits code (T4 box 45, 1 to 5) from its Canada card; nothing when it has none.",
            MinArguments = 0,
            MaxArguments = 0,
            ReturnType = _ => DataFieldType.Integer,
            Invoke = (_, expression) => TaxFormFormulaContext.DataOf<PayrollTaxFormData>(expression)?.Employer?.GetCanadaSettings().DentalBenefitsCode is { } code ? (long)code : null,
        };
    }
}
