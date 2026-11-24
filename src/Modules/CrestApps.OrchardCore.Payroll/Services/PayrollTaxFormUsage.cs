using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Tells Taxation's tax form page that a form has payroll filings: they keep its name, so it keeps it and is turned off
/// rather than deleted.
/// </summary>
internal sealed class PayrollTaxFormUsage : ITaxFormUsage
{
    private readonly IPayrollFilingManager _filings;

    internal readonly IStringLocalizer S;

    public PayrollTaxFormUsage(IPayrollFilingManager filings, IStringLocalizer<PayrollTaxFormUsage> stringLocalizer)
    {
        _filings = filings;
        S = stringLocalizer;
    }

    public async Task<IReadOnlyList<string>> GetUsagesAsync(TaxForm form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (string.IsNullOrEmpty(form.Name))
        {
            return [];
        }

        var count = (await _filings.GetAllAsync(cancellationToken)).Count(filing => string.Equals(filing.Form, form.Name, StringComparison.OrdinalIgnoreCase));

        return count == 0 ? [] : [S.Plural(count, "1 payroll filing", "{0} payroll filings").Value];
    }
}
