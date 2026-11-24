using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Payroll.Core.TaxForms;
using CrestApps.OrchardCore.Payroll.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Writes a person's statement of a filing as a PDF, for the filing's page and My Pay, as its tax form says: the title,
/// the copy line, the labels and the boxes it prints.
/// </summary>
public sealed class PayrollTaxFormDocuments
{
    private readonly IEmployerManager _employers;
    private readonly IPayrollTaxFormManager _forms;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    public PayrollTaxFormDocuments(IEmployerManager employers, IPayrollTaxFormManager forms, IServiceProvider services, IStringLocalizer<PayrollTaxFormDocuments> stringLocalizer)
    {
        _employers = employers;
        _forms = forms;
        _services = services;
        S = stringLocalizer;
    }

    /// <summary>
    /// Writes the person's statement.
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
        var form = await _forms.FindByNameAsync(filing.Form);
        var texts = form is null
            ? new PayrollTaxStatementTexts
            {
                // A form that is gone, or one a module adds in code: every box, by its number.
                Title = filing.Form,
                EmployerTaxId = employer?.TaxId,
                Boxes = [.. person.Boxes.Select(pair => (pair.Key, pair.Value))],
            }
            : PayrollTaxStatementDocument.TextsFor(form, person, employer);

        texts.TaxIdLabel ??= S["Tax id"];
        texts.EmployerTaxIdLabel ??= S["Employer's number"];
        texts.StateColumns =
        [
            S["15 State"],
            S["Employer's state ID number"],
            S["16 State wages, tips, etc."],
            S["17 State income tax"],
            S["18 Local wages, tips, etc."],
            S["19 Local income tax"],
            S["20 Locality name"],
        ];

        return PayrollTaxStatementDocument.Write(filing, person, employer, address, texts);
    }

    /// <summary>
    /// Gets the file name of a person's statement, such as W-2-2026.pdf.
    /// </summary>
    /// <param name="filing">The filing.</param>
    /// <returns>The file name.</returns>
    public async Task<string> FileNameAsync(PayrollFiling filing)
    {
        ArgumentNullException.ThrowIfNull(filing);

        return PayrollTaxStatementDocument.FileName(await _forms.FindByNameAsync(filing.Form), filing);
    }
}
