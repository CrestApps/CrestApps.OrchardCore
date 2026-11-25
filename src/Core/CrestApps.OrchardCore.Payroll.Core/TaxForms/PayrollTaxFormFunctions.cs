using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Payroll.Core.TaxForms;

/// <summary>
/// What a formula of a payroll tax form reads: the pay statements of a person (a recipient field) or of every reported
/// person (a filing field), and the period's pay codes, deposits, income tax rules and employer.
/// </summary>
public sealed record PayrollTaxFormData
{
    /// <summary>Gets the statements.</summary>
    public required IReadOnlyList<PayStatement> Statements { get; init; }

    /// <summary>Gets the pay codes, by id.</summary>
    public required IReadOnlyDictionary<string, PayCode> Codes { get; init; }

    /// <summary>Gets the deposits of the period.</summary>
    public required IReadOnlyList<TaxDeposit> Deposits { get; init; }

    /// <summary>Gets the income tax rules that apply on the period's last day.</summary>
    public required IReadOnlyList<IncomeTaxRule> Rules { get; init; }

    /// <summary>Gets the employer.</summary>
    public required Employer Employer { get; init; }

    /// <summary>Gets the last day of the period.</summary>
    public required DateOnly To { get; init; }
}

/// <summary>
/// The functions Payroll's tax forms call to read pay statements, on top of every tax form's (VALUE, TOTAL, RECIPIENTS,
/// ISACCOUNT...) and the formula language's own.
/// </summary>
public sealed class PayrollTaxFormFunctions : ITaxFormFunctionProvider
{
    /// <summary>The category the functions are listed under.</summary>
    public const string Category = "Payroll";

    /// <inheritdoc />
    public string Source => PayrollConstants.TaxFormSource;

    /// <inheritdoc />
    public IEnumerable<ExpressionFunction> GetFunctions()
    {
        yield return Number("WAGES", "WAGES(category)", "The wages of a tax category, such as 'IncomeTax', 'SocialInsurance' or 'Unemployment', from the pay code lines: pre-tax deductions taken off as each pay code's tax treatment says, a voided statement taken off.", 1, 1,
            (args, data) => PayrollFilingSums.Wages(data.Statements, data.Codes, TaxFormFunctions.Text(args[0])));
        yield return Number("TAX", "TAX(taxCode, ...)", "The amount of the taxes with these codes; * matches any characters, such as 'CA-*-PIT'.", 1, 50,
            (args, data) => PayrollFilingSums.Tax(data.Statements, Texts(args)));
        yield return Number("TAXABLE", "TAXABLE(taxCode, ...)", "The wages the taxes with these codes were worked out on; * matches any characters.", 1, 50,
            (args, data) => PayrollFilingSums.Taxable(data.Statements, Texts(args)));
        yield return Boolean("HASTAX", "HASTAX(taxCode, ...)", "True when the statements have a line of a tax with one of these codes.", 1, 50,
            (args, data) => PayrollFilingSums.HasTax(data.Statements, Texts(args)));
        yield return Number("PAYKIND", "PAYKIND(kind, ...)", "The earnings and imputed income of pay codes of these kinds, such as 'Tips'.", 1, 50,
            (args, data) => PayrollFilingSums.OfKind(data.Statements, data.Codes, Texts(args)));
        yield return Number("PAYCODEFIELD", "PAYCODEFIELD(name)", "The amounts of the pay codes that report to a tax form field, such as 'W2:12D'.", 1, 1,
            (args, data) => PayrollFilingSums.ReportedTo(data.Statements, data.Codes, TaxFormFunctions.Text(args[0])));
        yield return Number("REGIONWAGES", "REGIONWAGES()", "The wages of the employee's income taxes of the regions below the country, such as the states (a W-3's box 16).", 0, 0,
            (_, data) => PayrollFilingSums.BelowCountry(data.Statements, local: false, tax: false));
        yield return Number("REGIONTAX", "REGIONTAX()", "The employee's income taxes of the regions below the country, such as the states (a W-3's box 17).", 0, 0,
            (_, data) => PayrollFilingSums.BelowCountry(data.Statements, local: false, tax: true));
        yield return Number("LOCALWAGES", "LOCALWAGES()", "The wages of the employee's local income taxes, such as a county's or a city's (a W-3's box 18).", 0, 0,
            (_, data) => PayrollFilingSums.BelowCountry(data.Statements, local: true, tax: false));
        yield return Number("LOCALTAX", "LOCALTAX()", "The employee's local income taxes, such as a county's or a city's (a W-3's box 19).", 0, 0,
            (_, data) => PayrollFilingSums.BelowCountry(data.Statements, local: true, tax: true));
        yield return Number("GROSS", "GROSS()", "The gross pay of the statements.", 0, 0,
            (_, data) => data.Statements.Sum(statement => statement.Gross));
        yield return Number("HEADCOUNT", "HEADCOUNT(day)", "The number of people paid for a pay period that includes that day of the last month of the period, such as 12.", 1, 1,
            (args, data) => PayrollFilingSums.Headcount(data.Statements, data.To, (int)(TaxFormFunctions.Decimal(args[0]) ?? 12)));
        yield return Number("DEPOSITS", "DEPOSITS(jurisdiction, [group])", "The deposits of the period to a jurisdiction, of a group of taxes ('Withholding', 'Unemployment', 'NonPayrollWithholding') or of all.", 1, 2,
            (args, data) => PayrollFilingSums.Deposited(data.Deposits, TaxFormFunctions.Text(args[0]), args.Length > 1 ? TaxFormFunctions.Text(args[1]) : null));
        yield return new ExpressionFunction
        {
            Name = "RULE",
            Category = Category,
            Signature = "RULE(taxCode, field)",
            Description = "A figure of the income tax rule with this code that applies on the period's last day: 'Rate', 'WageBase', 'WageThreshold', 'YearlyMaximum', 'YearlyExemption', 'FixedAmount' or one of its parameters; nothing when there is none.",
            MinArguments = 2,
            MaxArguments = 2,
            ReturnType = _ => DataFieldType.Decimal,
            Invoke = (args, expression) => TaxFormFormulaContext.DataOf<PayrollTaxFormData>(expression) is { } data
                ? RuleField(data.Rules.FirstOrDefault(rule => string.Equals(rule.TaxCode, TaxFormFunctions.Text(args[0]), StringComparison.OrdinalIgnoreCase)), TaxFormFunctions.Text(args[1]))
                : null,
        };
        yield return new ExpressionFunction
        {
            Name = "ACCOUNT",
            Category = Category,
            Signature = "ACCOUNT(jurisdiction)",
            Description = "The account number of the employer's registration in a jurisdiction, such as ACCOUNT('CA').",
            MinArguments = 1,
            MaxArguments = 1,
            ReturnType = _ => DataFieldType.Text,
            Invoke = (args, expression) => TaxFormFormulaContext.DataOf<PayrollTaxFormData>(expression)?.Employer?.Registrations
                .FirstOrDefault(registration => string.Equals(registration.Jurisdiction, TaxFormFunctions.Text(args[0]), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(registration.AccountId))?.AccountId?.Trim(),
        };
    }

    private static string[] Texts(object[] args)
        => [.. args.Select(TaxFormFunctions.Text).Where(text => !string.IsNullOrEmpty(text))];

    private static decimal? RuleField(IncomeTaxRule rule, string field)
        => rule is null ? null : field?.ToUpperInvariant() switch
        {
            "RATE" => rule.Rate,
            "WAGEBASE" => rule.WageBase,
            "WAGETHRESHOLD" => rule.WageThreshold,
            "YEARLYMAXIMUM" => rule.YearlyMaximum,
            "YEARLYEXEMPTION" => rule.YearlyExemption,
            "FIXEDAMOUNT" => rule.FixedAmount,
            _ => rule.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Key, field, StringComparison.OrdinalIgnoreCase)) is { Key: not null } parameter ? parameter.Value : null,
        };

    private static ExpressionFunction Number(string name, string signature, string description, int min, int max, Func<object[], PayrollTaxFormData, decimal> invoke)
        => new()
        {
            Name = name,
            Category = Category,
            Signature = signature,
            Description = description,
            MinArguments = min,
            MaxArguments = max,
            ReturnType = _ => DataFieldType.Decimal,
            Invoke = (args, expression) => TaxFormFormulaContext.DataOf<PayrollTaxFormData>(expression) is { } data ? invoke(args, data) : null,
        };

    private static ExpressionFunction Boolean(string name, string signature, string description, int min, int max, Func<object[], PayrollTaxFormData, bool> invoke)
        => new()
        {
            Name = name,
            Category = Category,
            Signature = signature,
            Description = description,
            MinArguments = min,
            MaxArguments = max,
            ReturnType = _ => DataFieldType.Boolean,
            Invoke = (args, expression) => TaxFormFormulaContext.DataOf<PayrollTaxFormData>(expression) is { } data ? invoke(args, data) : null,
        };
}
