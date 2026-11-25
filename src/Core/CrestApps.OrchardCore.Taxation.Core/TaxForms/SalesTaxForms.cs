using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Core.TaxForms;

/// <summary>
/// What a formula of a sales tax form reads: the return (its lines, its period, its payments), the registration it is
/// filed under, and the day it is worked out as filed on.
/// </summary>
public sealed record SalesTaxFormData
{
    /// <summary>Gets the return, with the lines the data providers gave.</summary>
    public required TaxReturn Return { get; init; }

    /// <summary>Gets the registration the return is filed under.</summary>
    public required MerchantTaxRegistration Registration { get; init; }

    /// <summary>
    /// Gets the day the return is worked out as filed on: the day it was filed, or today for a draft past its due date;
    /// <see langword="null"/> for a draft that is not late.
    /// </summary>
    public DateOnly? FiledOn { get; init; }

    /// <summary>Gets the days the return is late: zero when it is on time.</summary>
    [JsonIgnore]
    public int DaysLate
        => FiledOn is { } filed && filed > Return.DueDate ? filed.DayNumber - Return.DueDate.DayNumber : 0;

    /// <summary>Gets the months the return is late, a month begun counting as a whole one: zero when it is on time.</summary>
    [JsonIgnore]
    public int MonthsLate
    {
        get
        {
            if (DaysLate == 0)
            {
                return 0;
            }

            var filed = FiledOn.Value;
            var months = ((filed.Year - Return.DueDate.Year) * 12) + filed.Month - Return.DueDate.Month;

            // A part of a month counts: 21 October to 21 November is one month, to 22 November two.
            return Return.DueDate.AddMonths(months) < filed ? months + 1 : months;
        }
    }
}

/// <summary>
/// The functions sales tax forms call to read a return, on top of every tax form's (VALUE, ISACCOUNT, FILINGYEAR...)
/// and the formula language's own: the return's lines by kind, jurisdiction, tax type, category and form line, the
/// registration, how late the return is (for a penalty, interest or a discount for filing on time) and its payments.
/// </summary>
public sealed class SalesTaxFormFunctions : ITaxFormFunctionProvider
{
    /// <summary>The category the functions are listed under.</summary>
    public const string Category = "Sales tax";

    private const string LineFilters = "Leave a filter out, or give '' or '*', to match any; * matches any characters, such as 'us-*'. A line with no jurisdiction is the return's own.";

    /// <inheritdoc />
    public string Source => TaxationConstants.SalesTaxFormSource;

    /// <inheritdoc />
    public IEnumerable<ExpressionFunction> GetFunctions()
    {
        yield return Number("AMOUNT", "AMOUNT(kind, [jurisdiction], [taxType], [category])", "The amounts (sales, purchases, wages) of the lines of a kind, such as AMOUNT('GrossSales') or AMOUNT('ExemptSales', '', '', 'Resale'). " + LineFilters, 1, 4,
            (args, data) => Lines(data, args).Sum(line => line.Amount));
        yield return Number("TAX", "TAX(kind, [jurisdiction], [taxType], [category])", "The tax of the lines of a kind, such as TAX('TaxableSales') or TAX('TaxableSales', 'us-mi-*'). " + LineFilters, 1, 4,
            (args, data) => Lines(data, args).Sum(line => line.TaxAmount));
        yield return Boolean("HASLINES", "HASLINES(kind, [jurisdiction], [taxType], [category])", "True when the return has a line of a kind, such as HASLINES('TaxableSales'). " + LineFilters, 1, 4,
            (args, data) => Lines(data, args).Any());
        yield return Number("LINEAMOUNT", "LINEAMOUNT(formLine)", "The amounts of the lines the data providers gave for a line of the form, such as LINEAMOUNT('5g').", 1, 1,
            (args, data) => FormLines(data, args).Sum(line => line.Amount));
        yield return Number("LINETAX", "LINETAX(formLine)", "The tax of the lines the data providers gave for a line of the form, such as LINETAX('5g').", 1, 1,
            (args, data) => FormLines(data, args).Sum(line => line.TaxAmount));
        yield return new ExpressionFunction
        {
            Name = "REGISTRATION",
            Category = Category,
            Signature = "REGISTRATION(field)",
            Description = "A figure of the registration the return is filed under: 'Number' (as it was when the return was prepared), 'Jurisdiction', 'TaxType' or 'Name'.",
            MinArguments = 1,
            MaxArguments = 1,
            ReturnType = _ => DataFieldType.Text,
            Invoke = (args, expression) => TaxFormFormulaContext.DataOf<SalesTaxFormData>(expression) is { } data
                ? TaxFormFunctions.Text(args[0])?.ToUpperInvariant() switch
                {
                    "NUMBER" => data.Return.RegistrationNumber ?? data.Registration.RegistrationNumber,
                    "JURISDICTION" => data.Return.JurisdictionId ?? data.Registration.JurisdictionId,
                    "TAXTYPE" => data.Return.TaxType ?? data.Registration.TaxType,
                    "NAME" => data.Registration.Name,
                    _ => null,
                }
                : null,
        };
        yield return Number("DAYSLATE", "DAYSLATE()", "The days the return is filed after its due date, for a penalty or interest; zero when it is on time. A draft past its due date counts as filed today.", 0, 0,
            (_, data) => data.DaysLate);
        yield return Number("MONTHSLATE", "MONTHSLATE()", "The months the return is filed after its due date, a month begun counting as a whole one, such as a penalty of 5% a month or part of a month; zero when it is on time.", 0, 0,
            (_, data) => data.MonthsLate);
        yield return Boolean("ONTIME", "ONTIME()", "True when the return is filed on or before its due date (a draft that is not late is), such as for a discount for filing on time.", 0, 0,
            (_, data) => data.DaysLate == 0);
        yield return Number("PAID", "PAID()", "What was paid on the return, with what was paid with the returns it corrects. The amount due is before payments: the return's balance takes them off.", 0, 0,
            (_, data) => data.Return.AmountPaid);
        yield return Number("PAIDEARLIER", "PAIDEARLIER()", "What was paid with the returns an amendment corrects, such as an amended return's line for the tax paid with the original.", 0, 0,
            (_, data) => data.Return.PaidWithEarlier);
        yield return Number("AMENDMENT", "AMENDMENT()", "The amendment number: 0 for the original return, 1 for its first amendment.", 0, 0,
            (_, data) => data.Return.Amendment);
        yield return Boolean("ISRECONCILIATION", "ISRECONCILIATION()", "True for the yearly reconciliation return filed besides the periodic returns.", 0, 0,
            (_, data) => data.Return.IsAnnualReconciliation);
        yield return Number("PERIODMONTHS", "PERIODMONTHS()", "The months the return covers: 1, 3 or 12, such as to spread a yearly cap over the periods.", 0, 0,
            (_, data) => TaxFilingFrequencies.IsKnown(data.Return.Frequency) ? TaxFilingFrequencies.MonthsOf(data.Return.Frequency) : 0);
    }

    /// <summary>
    /// Gets the lines of a return that match a kind and the optional jurisdiction, tax type and category, as AMOUNT and
    /// TAX read them.
    /// </summary>
    /// <param name="taxReturn">The return.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="jurisdiction">The jurisdiction, or <see langword="null"/>, '' or '*' for any.</param>
    /// <param name="taxType">The tax type, or <see langword="null"/>, '' or '*' for any.</param>
    /// <param name="category">The category, or <see langword="null"/>, '' or '*' for any.</param>
    /// <returns>The lines.</returns>
    public static IEnumerable<TaxReturnLine> Match(TaxReturn taxReturn, string kind, string jurisdiction = null, string taxType = null, string category = null)
    {
        ArgumentNullException.ThrowIfNull(taxReturn);

        return taxReturn.Lines.Where(line => Matches(line.Kind, kind)
            && Matches(line.JurisdictionId ?? taxReturn.JurisdictionId, jurisdiction)
            && Matches(line.TaxType, taxType)
            && Matches(line.Category, category));
    }

    private static IEnumerable<TaxReturnLine> Lines(SalesTaxFormData data, object[] args)
        => Match(data.Return, TaxFormFunctions.Text(args[0]), Arg(args, 1), Arg(args, 2), Arg(args, 3));

    private static IEnumerable<TaxReturnLine> FormLines(SalesTaxFormData data, object[] args)
    {
        var formLine = TaxFormFunctions.Text(args[0]);

        return string.IsNullOrEmpty(formLine)
            ? []
            : data.Return.Lines.Where(line => string.Equals(line.FormLine, formLine, StringComparison.OrdinalIgnoreCase));
    }

    private static string Arg(object[] args, int index)
        => args.Length > index ? TaxFormFunctions.Text(args[index]) : null;

    // Empty or * matches anything; otherwise the value, ignoring case, with * for any characters.
    private static bool Matches(string value, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "*")
        {
            return true;
        }

        if (value is null)
        {
            return false;
        }

        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);
        }

        return Regex.IsMatch(value, "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    private static ExpressionFunction Number(string name, string signature, string description, int min, int max, Func<object[], SalesTaxFormData, decimal> invoke)
        => new()
        {
            Name = name,
            Category = Category,
            Signature = signature,
            Description = description,
            MinArguments = min,
            MaxArguments = max,
            ReturnType = _ => DataFieldType.Decimal,
            Invoke = (args, expression) => TaxFormFormulaContext.DataOf<SalesTaxFormData>(expression) is { } data ? invoke(args, data) : null,
        };

    private static ExpressionFunction Boolean(string name, string signature, string description, int min, int max, Func<object[], SalesTaxFormData, bool> invoke)
        => new()
        {
            Name = name,
            Category = Category,
            Signature = signature,
            Description = description,
            MinArguments = min,
            MaxArguments = max,
            ReturnType = _ => DataFieldType.Boolean,
            Invoke = (args, expression) => TaxFormFormulaContext.DataOf<SalesTaxFormData>(expression) is { } data ? invoke(args, data) : null,
        };
}

/// <summary>
/// A Taxation tax form whose source is sales tax, as a return form: its filing fields are the return's boxes, worked out
/// from the return's lines; the field its settings name is the amount due; a failed check is a note on the return. It
/// has no recipients.
/// </summary>
public sealed class TaxFormReturnForm : ITaxReturnForm
{
    private readonly TaxFormFunctionCatalog _functions;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxFormReturnForm"/> class.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="functions">The functions of the formulas.</param>
    /// <param name="localizer">The localizer of the messages.</param>
    public TaxFormReturnForm(TaxForm form, TaxFormFunctionCatalog functions, IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(localizer);

        Form = form;
        _functions = functions;
        S = localizer;

        // The title is what the site's administrator typed, so there is no template to extract.
#pragma warning disable OCLOC103 // The value of the localization source is not a compile-time constant
        DisplayName = LocalizationSource.Create<TaxFormReturnForm>(string.IsNullOrWhiteSpace(form.DisplayName) ? form.Name : form.DisplayName);
#pragma warning restore OCLOC103
    }

    /// <summary>Gets the form record.</summary>
    [JsonIgnore]
    public TaxForm Form { get; }

    /// <inheritdoc />
    public string Name => Form.Name;

    /// <inheritdoc />
    [JsonIgnore]
    public LocalizationSource DisplayName { get; }

    /// <inheritdoc />
    public Task ComputeAsync(TaxReturnFormContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var formulas = TaxFormFormulas.Compile(Form, _functions.For(Form.Source), S, out var errors);

        if (formulas is null)
        {
            context.Return.Notes.Add(S["The formulas of {0} don't work, so the return has no boxes: {1}", Form.Name, string.Join(" ", errors.Select(error => error.Message))]);

            return Task.CompletedTask;
        }

        Compute(formulas, context);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Works out a return's boxes and amount due from a form's compiled formulas.
    /// </summary>
    /// <param name="formulas">The form's compiled formulas.</param>
    /// <param name="context">The return, its registration and the day it is worked out as filed on.</param>
    public static void Compute(TaxFormFormulas formulas, TaxReturnFormContext context)
    {
        ArgumentNullException.ThrowIfNull(formulas);
        ArgumentNullException.ThrowIfNull(context);

        var form = formulas.Form;
        var taxReturn = context.Return;
        var annual = taxReturn.Frequency == TaxFilingFrequencies.Annual;
        var data = new SalesTaxFormData
        {
            Return = taxReturn,
            Registration = context.Registration,
            FiledOn = context.FiledOn,
        };

        var result = TaxFormEvaluator.Evaluate(new TaxFormEvaluation
        {
            Formulas = formulas,
            Context = new TaxFormFormulaContext
            {
                Form = form,
                Year = taxReturn.PeriodEnd.Year,
                Quarter = annual || taxReturn.PeriodEnd == default ? 0 : ((taxReturn.PeriodEnd.Month - 1) / 3) + 1,
                From = taxReturn.PeriodStart,
                To = taxReturn.PeriodEnd,
                Services = context.Services,
            },

            // A return has no recipients: its fields are the filing's.
            Recipients = [],
            FilingData = data,
        });

        foreach (var (number, value) in result.Fields)
        {
            taxReturn.Boxes[number] = value;
        }

        foreach (var note in result.FailedChecks)
        {
            taxReturn.Notes.Add(note);
        }

        taxReturn.AmountDue = taxReturn.Boxes.GetValueOrDefault(form.AmountDueField());
    }
}
