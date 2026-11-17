using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Payroll.Core.Canada;
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

        if (filing.Form == Form1099Nec.FormName)
        {
            return Form1099NecDocument.Write(filing, person, employer, address, new Form1099NecTexts
            {
                Title = S["Form 1099-NEC Nonemployee Compensation"],
                Copy = S["Copy B, for the recipient"],
                PayerTaxId = S["Payer's TIN"],
                RecipientTaxId = S["Recipient's TIN"],
                Box1 = S["1 Nonemployee compensation"],
                Box4 = S["4 Federal income tax withheld"],
            });
        }

        if (filing.Form == FormT4.FormName)
        {
            return FormW2Document.Write(filing, person, employer, address, new FormW2Texts
            {
                Title = S["T4 Statement of Remuneration Paid"],
                Copy = S["Copy for the employee: keep it for your records and use it to file your income tax return"],
                TaxId = S["Social insurance number"],
                TaxIdFormat = "XXX XX{0}",
                EmployerTaxId = S["Employer's account number"],
                EmployerTaxIdValue = CanadaFilingSums.PayrollAccount(employer),
                Boxes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["14"] = S["14 Employment income"],
                    ["22"] = S["22 Income tax deducted"],
                    ["16"] = S["16 Employee's CPP contributions"],
                    ["16A"] = S["16A Employee's second CPP contributions"],
                    ["17"] = S["17 Employee's QPP contributions"],
                    ["17A"] = S["17A Employee's second QPP contributions"],
                    ["18"] = S["18 Employee's EI premiums"],
                    ["24"] = S["24 EI insurable earnings"],
                    ["26"] = S["26 CPP/QPP pensionable earnings"],
                    ["55"] = S["55 Employee's PPIP premiums"],
                    ["56"] = S["56 PPIP insurable earnings"],
                },
            });
        }

        if (filing.Form == FormRl1.FormName)
        {
            return FormW2Document.Write(filing, person, employer, address, new FormW2Texts
            {
                Title = S["RL-1 Employment and Other Income"],
                Copy = S["Copy for the employee: keep it for your records and use it to file your Quebec income tax return"],
                TaxId = S["Social insurance number"],
                TaxIdFormat = "XXX XX{0}",
                EmployerTaxId = S["Identification number"],
                EmployerTaxIdValue = CanadaFilingSums.QuebecAccount(employer),
                Boxes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["A"] = S["A Employment income"],
                    ["E"] = S["E Québec income tax withheld"],
                    ["B.A"] = S["B.A QPP contribution"],
                    ["B.B"] = S["B.B Additional QPP contribution"],
                    ["C"] = S["C Employment insurance premium"],
                    ["G"] = S["G Pensionable salary or wages under the QPP"],
                    ["H"] = S["H QPIP premium"],
                    ["I"] = S["I Eligible salary or wages under the QPIP"],
                },
            });
        }

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
        => filing?.Form switch
        {
            Form1099Nec.FormName => $"1099-NEC-{filing.Year}.pdf",
            FormT4.FormName => $"T4-{filing.Year}.pdf",
            FormRl1.FormName => $"RL-1-{filing.Year}.pdf",
            _ => $"W-2-{filing?.Year}.pdf",
        };
}
