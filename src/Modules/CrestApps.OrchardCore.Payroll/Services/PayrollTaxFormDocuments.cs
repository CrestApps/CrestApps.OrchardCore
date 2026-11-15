using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Writes a person's tax form of a filing as a PDF, in the reader's language, for the filing's page and My Pay.
/// </summary>
public sealed class PayrollTaxFormDocuments
{
    private readonly IEmployerManager _employers;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    public PayrollTaxFormDocuments(IEmployerManager employers, IServiceProvider services, IStringLocalizer<PayrollTaxFormDocuments> stringLocalizer)
    {
        _employers = employers;
        _services = services;
        S = stringLocalizer;
    }

    /// <summary>
    /// Writes the person's form.
    /// </summary>
    /// <param name="filing">The filing.</param>
    /// <param name="person">The person's statement in it.</param>
    /// <returns>The PDF.</returns>
    public async Task<byte[]> WriteAsync(PayrollFiling filing, PayrollFilingPerson person)
    {
        ArgumentNullException.ThrowIfNull(filing);
        ArgumentNullException.ThrowIfNull(person);

        var employer = await _employers.FindByIdAsync(filing.EmployerId);
        IReadOnlyList<string> address = employer?.Address is not null && _services.GetService<IAddressFormatter>() is { } formatter
            ? await formatter.GetLinesAsync(employer.Address)
            : [];

        return FormW2Document.Write(filing, person, employer, address, new FormW2Texts
        {
            Title = S["Form W-2 Wage and Tax Statement"],
            Copy = S["Copy B, to be filed with the employee's federal tax return; Copies C and 2 for the employee's records"],
            TaxId = S["Employee's social security number"],
            EmployerTaxId = S["Employer identification number (EIN)"],
            Boxes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["1"] = S["1 Wages, tips, other compensation"],
                ["2"] = S["2 Federal income tax withheld"],
                ["3"] = S["3 Social security wages"],
                ["4"] = S["4 Social security tax withheld"],
                ["5"] = S["5 Medicare wages and tips"],
                ["6"] = S["6 Medicare tax withheld"],
                ["7"] = S["7 Social security tips"],
            },
        });
    }

    /// <summary>
    /// Gets the file name of a person's form.
    /// </summary>
    /// <param name="filing">The filing.</param>
    /// <returns>The file name.</returns>
    public static string FileName(PayrollFiling filing)
        => $"W-2-{filing?.Year}.pdf";
}
