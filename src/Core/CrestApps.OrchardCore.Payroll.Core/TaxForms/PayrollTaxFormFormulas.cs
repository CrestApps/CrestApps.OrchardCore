using System.Globalization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Payroll.Core.TaxForms;

/// <summary>
/// Offers functions to the formulas of tax forms, such as one that reads an employer setting a country pack keeps.
/// Register one with <c>services.AddPayrollTaxFormFunctions&lt;T&gt;()</c>; a function reads what it works on with
/// <see cref="PayrollTaxFormFormulaContext.Current(ExpressionContext)"/>. The functions are only for tax forms: data
/// pipelines and reports never see them.
/// </summary>
public interface IPayrollTaxFormFunctionProvider : IExpressionFunctionProvider
{
}

/// <summary>
/// What a formula of a tax form is worked out on: the filing, and the statements of the person or of the filing.
/// </summary>
public sealed class PayrollTaxFormFormulaContext
{
    /// <summary>Gets the form.</summary>
    public required PayrollTaxForm Form { get; init; }

    /// <summary>Gets the employer.</summary>
    public required Employer Employer { get; init; }

    /// <summary>Gets the filing being prepared.</summary>
    public required PayrollFiling Filing { get; init; }

    /// <summary>Gets the first pay date of the period.</summary>
    public required DateOnly From { get; init; }

    /// <summary>Gets the last pay date of the period.</summary>
    public required DateOnly To { get; init; }

    /// <summary>Gets the pay codes, by id.</summary>
    public required IReadOnlyDictionary<string, PayCode> Codes { get; init; }

    /// <summary>Gets the deposits of the period.</summary>
    public required IReadOnlyList<TaxDeposit> Deposits { get; init; }

    /// <summary>Gets the income tax rules that apply on the period's last day.</summary>
    public required IReadOnlyList<IncomeTaxRule> Rules { get; init; }

    /// <summary>Gets the services.</summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Gets or sets the statements the formula is worked out on: a person's, for a person box and the condition; every
    /// reported person's, for a filing box and a check.
    /// </summary>
    public IReadOnlyList<PayStatement> Statements { get; set; } = [];

    /// <summary>Gets the people reported so far, with their boxes.</summary>
    public IList<IReadOnlyDictionary<string, decimal>> People { get; } = [];

    /// <summary>
    /// Gets the context of a formula being worked out.
    /// </summary>
    /// <param name="context">The expression context.</param>
    /// <returns>The context, or <see langword="null"/> when the formula is only being checked.</returns>
    public static PayrollTaxFormFormulaContext Current(ExpressionContext context)
        => context?.Row is { Length: > 0 } row ? row[0] as PayrollTaxFormFormulaContext : null;

    /// <summary>
    /// Gets the value of a form's yearly value for the filing's year: the latest year at or before it.
    /// </summary>
    /// <param name="name">The value's name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public decimal? Value(string name)
        => ValueFor(Form, name, Filing.Year);

    /// <summary>
    /// Gets the value of a form's yearly value for a year: the latest year at or before it.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="name">The value's name.</param>
    /// <param name="year">The year.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public static decimal? ValueFor(PayrollTaxForm form, string name, int year)
    {
        ArgumentNullException.ThrowIfNull(form);

        return form.Values
            .Where(value => string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase) && value.Year <= year)
            .OrderByDescending(value => value.Year)
            .Select(value => (decimal?)value.Amount)
            .FirstOrDefault();
    }
}

/// <summary>
/// The functions every tax form's formulas can call, on top of the formula language's own (IF, ROUND, GREATEST...).
/// </summary>
public sealed class PayrollTaxFormFunctions : IPayrollTaxFormFunctionProvider
{
    /// <summary>The category the functions are listed under.</summary>
    public const string Category = "Payroll";

    /// <inheritdoc />
    public IEnumerable<ExpressionFunction> GetFunctions()
    {
        yield return Number("WAGES", "WAGES(category)", "The wages of a tax category, such as 'IncomeTax', 'SocialInsurance' or 'Unemployment', from the pay code lines (pre-tax deductions taken off as each pay code says).", 1, 1,
            (args, context) => PayrollFilingSums.Wages(context.Statements, context.Codes, Text(args[0])));
        yield return Number("TAX", "TAX(taxCode, ...)", "The amount of the taxes with these codes; * matches any characters, such as 'CA-*-PIT'.", 1, 50,
            (args, context) => PayrollFilingSums.Tax(context.Statements, Texts(args)));
        yield return Number("TAXABLE", "TAXABLE(taxCode, ...)", "The wages the taxes with these codes were worked out on; * matches any characters.", 1, 50,
            (args, context) => PayrollFilingSums.Taxable(context.Statements, Texts(args)));
        yield return Boolean("HASTAX", "HASTAX(taxCode, ...)", "True when the statements have a line of a tax with one of these codes.", 1, 50,
            (args, context) => PayrollFilingSums.HasTax(context.Statements, Texts(args)));
        yield return Number("PAYKIND", "PAYKIND(kind, ...)", "The earnings and imputed income of pay codes of these kinds, such as 'Tips'.", 1, 50,
            (args, context) => PayrollFilingSums.OfKind(context.Statements, context.Codes, Texts(args)));
        yield return Number("PAYCODEFIELD", "PAYCODEFIELD(name)", "The amounts of the pay codes that report to a tax form field, such as 'W2:12D'.", 1, 1,
            (args, context) => PayrollFilingSums.ReportedTo(context.Statements, context.Codes, Text(args[0])));
        yield return Number("REGIONWAGES", "REGIONWAGES()", "The wages of the employee's income taxes of the regions below the country, such as the states (a W-3's box 16).", 0, 0,
            (_, context) => PayrollFilingSums.BelowCountry(context.Statements, local: false, tax: false));
        yield return Number("REGIONTAX", "REGIONTAX()", "The employee's income taxes of the regions below the country, such as the states (a W-3's box 17).", 0, 0,
            (_, context) => PayrollFilingSums.BelowCountry(context.Statements, local: false, tax: true));
        yield return Number("LOCALWAGES", "LOCALWAGES()", "The wages of the employee's local income taxes, such as a county's or a city's (a W-3's box 18).", 0, 0,
            (_, context) => PayrollFilingSums.BelowCountry(context.Statements, local: true, tax: false));
        yield return Number("LOCALTAX", "LOCALTAX()", "The employee's local income taxes, such as a county's or a city's (a W-3's box 19).", 0, 0,
            (_, context) => PayrollFilingSums.BelowCountry(context.Statements, local: true, tax: true));
        yield return Number("GROSS", "GROSS()", "The gross pay of the statements.", 0, 0,
            (_, context) => context.Statements.Sum(statement => statement.Gross));
        yield return Number("HEADCOUNT", "HEADCOUNT(day)", "The number of people paid for a pay period that includes that day of the last month of the period, such as 12.", 1, 1,
            (args, context) => PayrollFilingSums.Headcount(context.Statements, context.To, (int)(Decimal(args[0]) ?? 12)));
        yield return Number("DEPOSITS", "DEPOSITS(jurisdiction, [group])", "The deposits of the period to a jurisdiction, of a group of taxes ('Withholding', 'Unemployment', 'NonPayrollWithholding') or of all.", 1, 2,
            (args, context) => PayrollFilingSums.Deposited(context.Deposits, Text(args[0]), args.Length > 1 ? Text(args[1]) : null));
        yield return Number("PEOPLE", "PEOPLE()", "The number of people reported (statements, for a form with a statement per jurisdiction).", 0, 0,
            (_, context) => context.People.Count);
        yield return Number("TOTAL", "TOTAL(number)", "The total of a person field over the people reported, such as TOTAL('1').", 1, 1,
            (args, context) => context.People.Sum(person => person.GetValueOrDefault(Text(args[0]) ?? string.Empty)));
        yield return new ExpressionFunction
        {
            Name = "VALUE",
            Category = Category,
            Signature = "VALUE(name, [fallback])",
            Description = "A yearly value of the form for the filing's year, such as VALUE('Threshold'); the fallback, or nothing, when it has none.",
            MinArguments = 1,
            MaxArguments = 2,
            ReturnType = _ => DataFieldType.Decimal,
            Invoke = (args, expression) => PayrollTaxFormFormulaContext.Current(expression) is { } context
                ? context.Value(Text(args[0])) ?? (args.Length > 1 ? Decimal(args[1]) : null)
                : null,
        };
        yield return new ExpressionFunction
        {
            Name = "RULE",
            Category = Category,
            Signature = "RULE(taxCode, field)",
            Description = "A figure of the income tax rule with this code that applies on the period's last day: 'Rate', 'WageBase', 'WageThreshold', 'YearlyMaximum', 'YearlyExemption', 'FixedAmount' or one of its parameters; nothing when there is none.",
            MinArguments = 2,
            MaxArguments = 2,
            ReturnType = _ => DataFieldType.Decimal,
            Invoke = (args, expression) => PayrollTaxFormFormulaContext.Current(expression) is { } context
                ? RuleField(context.Rules.FirstOrDefault(rule => string.Equals(rule.TaxCode, Text(args[0]), StringComparison.OrdinalIgnoreCase)), Text(args[1]))
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
            Invoke = (args, expression) => PayrollTaxFormFormulaContext.Current(expression)?.Employer?.Registrations
                .FirstOrDefault(registration => string.Equals(registration.Jurisdiction, Text(args[0]), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(registration.AccountId))?.AccountId?.Trim(),
        };
        yield return ExpressionFunction.CreateScalar(
            "ISACCOUNT",
            Category,
            "ISACCOUNT(number, shape)",
            "True when a number has a shape: # a digit, @ a letter, any other character itself; spaces and dashes are left out. ISACCOUNT(ACCOUNT('CA'), '#########RP####').",
            2,
            2,
            _ => DataFieldType.Boolean,
            args => PayrollFilingSums.HasShape(Text(args[0]), Text(args[1])));
        yield return new ExpressionFunction
        {
            Name = "FILINGYEAR",
            Category = Category,
            Signature = "FILINGYEAR()",
            Description = "The year of the filing.",
            MinArguments = 0,
            MaxArguments = 0,
            ReturnType = _ => DataFieldType.Integer,
            Invoke = (_, expression) => PayrollTaxFormFormulaContext.Current(expression) is { } context ? (long)context.Filing.Year : null,
        };
        yield return new ExpressionFunction
        {
            Name = "FILINGQUARTER",
            Category = Category,
            Signature = "FILINGQUARTER()",
            Description = "The quarter of the filing, 1 to 4, or 0 for a yearly form.",
            MinArguments = 0,
            MaxArguments = 0,
            ReturnType = _ => DataFieldType.Integer,
            Invoke = (_, expression) => PayrollTaxFormFormulaContext.Current(expression) is { } context ? (long)context.Filing.Quarter : null,
        };
    }

    /// <summary>Gets an argument as text.</summary>
    /// <param name="value">The argument.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    public static string Text(object value)
        => value switch
        {
            null => null,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

    /// <summary>Gets an argument as a number.</summary>
    /// <param name="value">The argument.</param>
    /// <returns>The number, or <see langword="null"/>.</returns>
    public static decimal? Decimal(object value)
        => value switch
        {
            decimal number => number,
            long number => number,
            int number => number,
            double number => (decimal)number,
            bool flag => flag ? 1 : 0,
            string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => number,
            _ => null,
        };

    private static string[] Texts(object[] args)
        => [.. args.Select(Text).Where(text => !string.IsNullOrEmpty(text))];

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

    private static ExpressionFunction Number(string name, string signature, string description, int min, int max, Func<object[], PayrollTaxFormFormulaContext, decimal> invoke)
        => new()
        {
            Name = name,
            Category = Category,
            Signature = signature,
            Description = description,
            MinArguments = min,
            MaxArguments = max,
            ReturnType = _ => DataFieldType.Decimal,
            Invoke = (args, expression) => PayrollTaxFormFormulaContext.Current(expression) is { } context ? invoke(args, context) : null,
        };

    private static ExpressionFunction Boolean(string name, string signature, string description, int min, int max, Func<object[], PayrollTaxFormFormulaContext, bool> invoke)
        => new()
        {
            Name = name,
            Category = Category,
            Signature = signature,
            Description = description,
            MinArguments = min,
            MaxArguments = max,
            ReturnType = _ => DataFieldType.Boolean,
            Invoke = (args, expression) => PayrollTaxFormFormulaContext.Current(expression) is { } context ? invoke(args, context) : null,
        };
}

/// <summary>
/// The functions the formulas of tax forms can call: the formula language's own and those of
/// <see cref="IPayrollTaxFormFunctionProvider"/> implementations.
/// </summary>
public sealed class PayrollTaxFormFunctionCatalog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxFormFunctionCatalog"/> class.
    /// </summary>
    /// <param name="providers">The providers.</param>
    public PayrollTaxFormFunctionCatalog(IEnumerable<IPayrollTaxFormFunctionProvider> providers)
    {
        // The formula language's own functions first (a catalog made from providers has only theirs), then Payroll's and
        // the modules', which replace a built-in function of the same name.
        Functions = new ExpressionFunctionCatalog([new BuiltInExpressionFunctionProvider(), .. providers ?? []]);
    }

    /// <summary>Gets the catalog.</summary>
    public IExpressionFunctionCatalog Functions { get; }
}

/// <summary>
/// Compiles and checks the formulas of a tax form, and works its boxes out in an order where each box comes after the
/// boxes it reads.
/// </summary>
public sealed class PayrollTaxFormFormulas
{
    private readonly Dictionary<string, int> _personSlots;
    private readonly Dictionary<string, int> _filingSlots;

    private PayrollTaxFormFormulas(PayrollTaxForm form)
    {
        Form = form;
        PersonFields = [.. form.Fields.Where(box => box.Level == PayrollTaxFormFieldLevel.Person)];
        FilingFields = [.. form.Fields.Where(box => box.Level == PayrollTaxFormFieldLevel.Filing)];
        TotaledFields = [.. PersonFields.Where(box => box.AddToFiling)];

        // Slot 0 of a row holds the formula context; the boxes follow.
        _personSlots = Slots(PersonFields.Select(box => box.Number));
        _filingSlots = Slots(TotaledFields.Select(box => box.Number).Concat(FilingFields.Select(box => box.Number)));
    }

    /// <summary>Gets the form.</summary>
    public PayrollTaxForm Form { get; }

    /// <summary>Gets the person boxes, in the order they are worked out.</summary>
    public IReadOnlyList<PayrollTaxFormField> PersonFields { get; private set; }

    /// <summary>Gets the filing boxes, in the order they are worked out.</summary>
    public IReadOnlyList<PayrollTaxFormField> FilingFields { get; private set; }

    /// <summary>Gets the person boxes added up on the filing.</summary>
    public IReadOnlyList<PayrollTaxFormField> TotaledFields { get; }

    /// <summary>Gets the compiled formula of each box, by box; a box without a formula has none.</summary>
    public Dictionary<PayrollTaxFormField, CompiledExpression> Compiled { get; } = [];

    /// <summary>Gets the compiled condition a person is reported on, or <see langword="null"/>.</summary>
    public CompiledExpression PersonCondition { get; private set; }

    /// <summary>Gets the compiled checks.</summary>
    public List<(PayrollTaxFormCheck Check, CompiledExpression Condition)> Checks { get; } = [];

    /// <summary>
    /// Compiles a form's formulas.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="functions">The functions.</param>
    /// <param name="localizer">The localizer of the messages.</param>
    /// <param name="errors">What is wrong, by the field it is about (a box's number for a box).</param>
    /// <returns>The compiled formulas, or <see langword="null"/> when any is wrong.</returns>
    public static PayrollTaxFormFormulas Compile(PayrollTaxForm form, IExpressionFunctionCatalog functions, IStringLocalizer localizer, out IList<(string Field, string Message)> errors)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(localizer);

        var S = localizer;
        var list = new List<(string Field, string Message)>();

        errors = list;

        foreach (var (level, boxes) in new[] { ("Person", form.Fields.Where(box => box.Level == PayrollTaxFormFieldLevel.Person)), ("Filing", form.Fields.Where(box => box.Level == PayrollTaxFormFieldLevel.Filing)) })
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var box in boxes)
            {
                if (string.IsNullOrWhiteSpace(box.Number))
                {
                    list.Add((nameof(PayrollTaxForm.Fields), S["Every field needs its box or line number."]));
                }
                else if (box.Number.Contains('[', StringComparison.Ordinal) || box.Number.Contains(']', StringComparison.Ordinal) || box.Number.Contains(':', StringComparison.Ordinal))
                {
                    list.Add((box.Number, S["Field {0}: a number has no brackets or colons.", box.Number]));
                }
                else if (!seen.Add(box.Number))
                {
                    list.Add((box.Number, level == "Person" ? S["Two person fields are numbered {0}.", box.Number] : S["Two filing fields are numbered {0}.", box.Number]));
                }
            }
        }

        foreach (var box in form.Fields.Where(box => box.Level == PayrollTaxFormFieldLevel.Filing && !string.IsNullOrWhiteSpace(box.Number)))
        {
            if (form.Fields.Any(other => other.Level == PayrollTaxFormFieldLevel.Person && other.AddToFiling && other.Number == box.Number))
            {
                list.Add((box.Number, S["Field {0} is a filing field and a person field added up on the filing: give one of them another number.", box.Number]));
            }
        }

        if (list.Count > 0)
        {
            return null;
        }

        var formulas = new PayrollTaxFormFormulas(form);
        var personScope = new Scope(formulas._personSlots);
        var filingScope = new Scope(formulas._filingSlots);

        foreach (var box in form.Fields)
        {
            if (string.IsNullOrWhiteSpace(box.Formula))
            {
                continue;
            }

            var compiled = TryCompile(box.Formula, box.Level == PayrollTaxFormFieldLevel.Person ? personScope : filingScope, functions, S, box.Number, list);

            if (compiled is not null)
            {
                formulas.Compiled[box] = compiled;
            }
        }

        if (!string.IsNullOrWhiteSpace(form.PersonCondition))
        {
            formulas.PersonCondition = TryCompile(form.PersonCondition, personScope, functions, S, nameof(PayrollTaxForm.PersonCondition), list);
        }

        foreach (var check in form.Checks)
        {
            if (string.IsNullOrWhiteSpace(check.Condition))
            {
                list.Add((nameof(PayrollTaxForm.Checks), S["Every check needs its condition."]));

                continue;
            }

            if (TryCompile(check.Condition, filingScope, functions, S, nameof(PayrollTaxForm.Checks), list) is { } condition)
            {
                formulas.Checks.Add((check, condition));
            }
        }

        if (list.Count > 0)
        {
            return null;
        }

        formulas.PersonFields = formulas.Order(formulas.PersonFields, S, list);
        formulas.FilingFields = formulas.Order(formulas.FilingFields, S, list);

        return list.Count > 0 ? null : formulas;
    }

    /// <summary>
    /// Makes a row of a person's boxes for the formulas.
    /// </summary>
    /// <param name="context">The formula context.</param>
    /// <returns>The row.</returns>
    public object[] PersonRow(PayrollTaxFormFormulaContext context)
        => NewRow(_personSlots, context);

    /// <summary>
    /// Makes a row of the filing's boxes for the formulas.
    /// </summary>
    /// <param name="context">The formula context.</param>
    /// <returns>The row.</returns>
    public object[] FilingRow(PayrollTaxFormFormulaContext context)
        => NewRow(_filingSlots, context);

    /// <summary>Gets the slot of a person box in a person row.</summary>
    /// <param name="key">The box's number.</param>
    /// <returns>The slot.</returns>
    public int PersonSlot(string key)
        => _personSlots[key];

    /// <summary>Gets the slot of a filing box (or a totaled person box) in a filing row.</summary>
    /// <param name="key">The box's number.</param>
    /// <returns>The slot.</returns>
    public int FilingSlot(string key)
        => _filingSlots[key];

    /// <summary>
    /// Gets a formula's value as an amount: a missing value is zero and true is 1.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The amount.</returns>
    public static decimal Amount(object value)
        => PayrollTaxFormFunctions.Decimal(value) ?? 0m;

    /// <summary>
    /// Gets whether a formula's value is true: true, or a number that is not zero.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsTrue(object value)
        => value switch
        {
            bool flag => flag,
            null => false,
            string text => !string.IsNullOrEmpty(text) && text != "0" && !string.Equals(text, "false", StringComparison.OrdinalIgnoreCase),
            _ => Amount(value) != 0,
        };

    private static CompiledExpression TryCompile(string formula, Scope scope, IExpressionFunctionCatalog functions, IStringLocalizer S, string field, List<(string Field, string Message)> errors)
    {
        try
        {
            var compiled = ExpressionCompiler.Compile(formula, scope, functions, S);

            if (compiled.IsAggregate)
            {
                errors.Add((field, S["{0}: a tax form's formula can't use SUM, AVG, COUNT or another aggregate; use TOTAL to add up a person field.", field]));

                return null;
            }

            return compiled;
        }
        catch (ExpressionException exception)
        {
            errors.Add((field, S["{0}: {1}", field, exception.Message]));

            return null;
        }
    }

    // Orders boxes so each comes after the boxes of its level it reads; a box that reads itself through others is wrong.
    private List<PayrollTaxFormField> Order(IReadOnlyList<PayrollTaxFormField> boxes, IStringLocalizer S, List<(string Field, string Message)> errors)
    {
        var byKey = boxes.ToDictionary(box => box.Number, StringComparer.Ordinal);
        var ordered = new List<PayrollTaxFormField>();
        var state = new Dictionary<PayrollTaxFormField, int>();

        bool Visit(PayrollTaxFormField box)
        {
            if (state.TryGetValue(box, out var mark))
            {
                return mark == 2;
            }

            state[box] = 1;

            if (Compiled.TryGetValue(box, out var compiled))
            {
                foreach (var key in compiled.References)
                {
                    if (byKey.TryGetValue(key, out var read) && !Visit(read))
                    {
                        if (state[box] == 1)
                        {
                            errors.Add((box.Number, S["Field {0} reads itself through the fields it reads.", box.Number]));
                        }

                        state[box] = 2;

                        return false;
                    }
                }
            }

            state[box] = 2;
            ordered.Add(box);

            return true;
        }

        foreach (var box in boxes)
        {
            Visit(box);
        }

        return ordered;
    }

    private static Dictionary<string, int> Slots(IEnumerable<string> keys)
    {
        var slots = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            slots.TryAdd(key, slots.Count + 1);
        }

        return slots;
    }

    private static object[] NewRow(Dictionary<string, int> slots, PayrollTaxFormFormulaContext context)
    {
        var row = new object[slots.Count + 1];

        row[0] = context;

        for (var i = 1; i < row.Length; i++)
        {
            row[i] = 0m;
        }

        return row;
    }

    private sealed class Scope : IExpressionScope
    {
        private readonly Dictionary<string, int> _slots;

        public Scope(Dictionary<string, int> slots)
        {
            _slots = slots;
        }

        public bool TryResolve(string key, out ExpressionFieldBinding binding)
        {
            if (key is not null && _slots.TryGetValue(key, out var slot))
            {
                binding = new ExpressionFieldBinding
                {
                    Key = key,
                    DataType = DataFieldType.Decimal,
                    Index = slot,
                };

                return true;
            }

            binding = null;

            return false;
        }
    }
}

/// <summary>
/// Registers the functions of tax form formulas.
/// </summary>
public static class PayrollTaxFormFunctionServiceCollectionExtensions
{
    /// <summary>
    /// Adds functions the formulas of tax forms can call.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayrollTaxFormFunctions<TProvider>(this IServiceCollection services)
        where TProvider : class, IPayrollTaxFormFunctionProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPayrollTaxFormFunctionProvider, TProvider>());

        return services;
    }
}
