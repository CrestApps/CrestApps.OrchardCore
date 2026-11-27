using System.Collections.Concurrent;
using System.Globalization;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Taxation.Core.TaxForms;

/// <summary>
/// Offers functions to the formulas of tax forms, such as Payroll's functions that read pay statements. Register one
/// with <c>services.AddTaxFormFunctions&lt;T&gt;()</c>; a function reads what it works on with
/// <see cref="TaxFormFormulaContext.Current(ExpressionContext)"/>. The functions are only for tax forms: data pipelines
/// and reports never see them.
/// </summary>
public interface ITaxFormFunctionProvider : IExpressionFunctionProvider
{
    /// <summary>
    /// Gets the tax form source whose forms can call the functions, such as Payroll, or <see langword="null"/> for every
    /// form.
    /// </summary>
    string Source { get; }
}

/// <summary>
/// What a formula of a tax form is worked out on: the form and the filing's period, and the records of the recipient or
/// of the filing, which the form's source puts in <see cref="Data"/>.
/// </summary>
public sealed class TaxFormFormulaContext
{
    /// <summary>Gets the form.</summary>
    public required TaxForm Form { get; init; }

    /// <summary>Gets the year of the filing.</summary>
    public required int Year { get; init; }

    /// <summary>Gets the quarter of the filing, 1 to 4, or 0 for a yearly form.</summary>
    public int Quarter { get; init; }

    /// <summary>Gets the first day of the period.</summary>
    public required DateOnly From { get; init; }

    /// <summary>Gets the last day of the period.</summary>
    public required DateOnly To { get; init; }

    /// <summary>Gets the services.</summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Gets or sets what the source gives the formula to read: a recipient's records for a recipient field and the
    /// condition, every reported recipient's for a filing field and a check.
    /// </summary>
    public object Data { get; set; }

    /// <summary>Gets the recipients reported so far, with their fields.</summary>
    public IList<IReadOnlyDictionary<string, decimal>> Recipients { get; } = [];

    /// <summary>
    /// Gets the context of a formula being worked out.
    /// </summary>
    /// <param name="context">The expression context.</param>
    /// <returns>The context, or <see langword="null"/> when the formula is only being checked.</returns>
    public static TaxFormFormulaContext Current(ExpressionContext context)
        => context?.Row is { Length: > 0 } row ? row[0] as TaxFormFormulaContext : null;

    /// <summary>
    /// Gets what the source gave the formula, as its own type.
    /// </summary>
    /// <typeparam name="TData">The source's type.</typeparam>
    /// <param name="context">The expression context.</param>
    /// <returns>The data, or <see langword="null"/>.</returns>
    public static TData DataOf<TData>(ExpressionContext context)
        where TData : class
        => Current(context)?.Data as TData;

    /// <summary>
    /// Gets the value of a form's yearly value for the filing's year: the latest year at or before it.
    /// </summary>
    /// <param name="name">The value's name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public decimal? Value(string name)
        => ValueFor(Form, name, Year);

    /// <summary>
    /// Gets the value of a form's yearly value for a year: the latest year at or before it.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="name">The value's name.</param>
    /// <param name="year">The year.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public static decimal? ValueFor(TaxForm form, string name, int year)
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
/// The functions every tax form's formulas can call, whatever its source, on top of the formula language's own (IF,
/// ROUND, GREATEST...): yearly values, recipient totals, account shapes and the filing's period.
/// </summary>
public sealed class TaxFormFunctions : ITaxFormFunctionProvider
{
    /// <summary>The category the functions are listed under.</summary>
    public const string Category = "Tax form";

    /// <inheritdoc />
    public string Source => null;

    /// <inheritdoc />
    public IEnumerable<ExpressionFunction> GetFunctions()
    {
        yield return new ExpressionFunction
        {
            Name = "VALUE",
            Category = Category,
            Signature = "VALUE(name, [fallback])",
            Description = "A yearly value of the form for the filing's year, such as VALUE('Threshold'); the fallback, or nothing, when it has none.",
            MinArguments = 1,
            MaxArguments = 2,
            ReturnType = _ => DataFieldType.Decimal,
            Invoke = (args, expression) => TaxFormFormulaContext.Current(expression) is { } context
                ? context.Value(Text(args[0])) ?? (args.Length > 1 ? Decimal(args[1]) : null)
                : null,
        };
        yield return new ExpressionFunction
        {
            Name = "RECIPIENTS",
            Category = Category,
            Signature = "RECIPIENTS()",
            Description = "The number of recipients reported (statements, for a form with a statement per jurisdiction).",
            MinArguments = 0,
            MaxArguments = 0,
            ReturnType = _ => DataFieldType.Decimal,
            Invoke = (_, expression) => TaxFormFormulaContext.Current(expression) is { } context ? (decimal)context.Recipients.Count : null,
        };
        yield return new ExpressionFunction
        {
            Name = "TOTAL",
            Category = Category,
            Signature = "TOTAL(number)",
            Description = "The total of a recipient field over the recipients reported, such as TOTAL('1').",
            MinArguments = 1,
            MaxArguments = 1,
            ReturnType = _ => DataFieldType.Decimal,
            Invoke = (args, expression) => TaxFormFormulaContext.Current(expression) is { } context
                ? context.Recipients.Sum(recipient => recipient.GetValueOrDefault(Text(args[0]) ?? string.Empty))
                : null,
        };
        yield return ExpressionFunction.CreateScalar(
            "ISACCOUNT",
            Category,
            "ISACCOUNT(number, shape)",
            "True when a number has a shape: # a digit, @ a letter, any other character itself; spaces and dashes are left out of both. ISACCOUNT('123456789RP0001', '#########RP####').",
            2,
            2,
            _ => DataFieldType.Boolean,
            args => HasShape(Text(args[0]), Text(args[1])));
        yield return new ExpressionFunction
        {
            Name = "FILINGYEAR",
            Category = Category,
            Signature = "FILINGYEAR()",
            Description = "The year of the filing.",
            MinArguments = 0,
            MaxArguments = 0,
            ReturnType = _ => DataFieldType.Integer,
            Invoke = (_, expression) => TaxFormFormulaContext.Current(expression) is { } context ? (long)context.Year : null,
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
            Invoke = (_, expression) => TaxFormFormulaContext.Current(expression) is { } context ? (long)context.Quarter : null,
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

    /// <summary>
    /// Gets whether an account number has a shape: # is a digit, @ a letter, any other character itself (ignoring case);
    /// spaces and dashes are left out of both first. 123456789RP0001 has the shape #########RP####, and 12-3456789 the
    /// shape ##-#######.
    /// </summary>
    /// <param name="account">The account number.</param>
    /// <param name="mask">The shape.</param>
    /// <returns>Whether it has.</returns>
    public static bool HasShape(string account, string mask)
    {
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(mask))
        {
            return false;
        }

        static string Squeeze(string text)
            => new([.. text.Where(character => !char.IsWhiteSpace(character) && character != '-')]);

        var value = Squeeze(account);

        mask = Squeeze(mask);

        if (value.Length != mask.Length)
        {
            return false;
        }

        for (var i = 0; i < mask.Length; i++)
        {
            var ok = mask[i] switch
            {
                '#' => char.IsAsciiDigit(value[i]),
                '@' => char.IsAsciiLetter(value[i]),
                _ => char.ToUpperInvariant(value[i]) == char.ToUpperInvariant(mask[i]),
            };

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// The functions the formulas of a tax form can call: the formula language's own, those for every form and those of
/// the form's source.
/// </summary>
public sealed class TaxFormFunctionCatalog
{
    private readonly IReadOnlyList<ITaxFormFunctionProvider> _providers;
    private readonly ConcurrentDictionary<string, IExpressionFunctionCatalog> _catalogs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxFormFunctionCatalog"/> class.
    /// </summary>
    /// <param name="providers">The providers.</param>
    public TaxFormFunctionCatalog(IEnumerable<ITaxFormFunctionProvider> providers)
    {
        _providers = [.. providers ?? []];
    }

    /// <summary>
    /// Gets the functions the forms of a source can call.
    /// </summary>
    /// <param name="source">The source, such as Payroll.</param>
    /// <returns>The catalog.</returns>
    public IExpressionFunctionCatalog For(string source)
        => _catalogs.GetOrAdd(source ?? string.Empty, key => new ExpressionFunctionCatalog(
        [

            // The formula language's own functions first (a catalog made from providers has only theirs), then those
            // for every form, then the source's, which replace one of the same name.
            new BuiltInExpressionFunctionProvider(),
            .. _providers.Where(provider => provider.Source is null),
            .. _providers.Where(provider => provider.Source is not null && string.Equals(provider.Source, key, StringComparison.OrdinalIgnoreCase)),
        ]));
}

/// <summary>
/// Compiles and checks the formulas of a tax form, and orders its fields so each comes after the fields it reads.
/// </summary>
public sealed class TaxFormFormulas
{
    private readonly Dictionary<string, int> _recipientSlots;
    private readonly Dictionary<string, int> _filingSlots;

    private TaxFormFormulas(TaxForm form)
    {
        Form = form;
        RecipientFields = [.. form.Fields.Where(box => box.Level == TaxFormFieldLevel.Recipient)];
        FilingFields = [.. form.Fields.Where(box => box.Level == TaxFormFieldLevel.Filing)];
        TotaledFields = [.. RecipientFields.Where(box => box.AddToFiling)];

        // Slot 0 of a row holds the formula context; the fields follow.
        _recipientSlots = Slots(RecipientFields.Select(box => box.Number));
        _filingSlots = Slots(TotaledFields.Select(box => box.Number).Concat(FilingFields.Select(box => box.Number)));
    }

    /// <summary>Gets the form.</summary>
    public TaxForm Form { get; }

    /// <summary>Gets the recipient fields, in the order they are worked out.</summary>
    public IReadOnlyList<TaxFormField> RecipientFields { get; private set; }

    /// <summary>Gets the filing fields, in the order they are worked out.</summary>
    public IReadOnlyList<TaxFormField> FilingFields { get; private set; }

    /// <summary>Gets the recipient fields added up on the filing.</summary>
    public IReadOnlyList<TaxFormField> TotaledFields { get; }

    /// <summary>Gets the compiled formula of each field; a field without a formula has none.</summary>
    public Dictionary<TaxFormField, CompiledExpression> Compiled { get; } = [];

    /// <summary>Gets the compiled condition a recipient is reported on, or <see langword="null"/>.</summary>
    public CompiledExpression RecipientCondition { get; private set; }

    /// <summary>Gets the compiled checks.</summary>
    public List<(TaxFormCheck Check, CompiledExpression Condition)> Checks { get; } = [];

    /// <summary>
    /// Compiles a form's formulas.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="functions">The functions.</param>
    /// <param name="localizer">The localizer of the messages.</param>
    /// <param name="errors">What is wrong, by what it is about (a field's number for a field).</param>
    /// <returns>The compiled formulas, or <see langword="null"/> when any is wrong.</returns>
    public static TaxFormFormulas Compile(TaxForm form, IExpressionFunctionCatalog functions, IStringLocalizer localizer, out IList<(string Field, string Message)> errors)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(localizer);

        var S = localizer;
        var list = new List<(string Field, string Message)>();

        errors = list;

        foreach (var (level, boxes) in new[] { ("Recipient", form.Fields.Where(box => box.Level == TaxFormFieldLevel.Recipient)), ("Filing", form.Fields.Where(box => box.Level == TaxFormFieldLevel.Filing)) })
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var box in boxes)
            {
                if (string.IsNullOrWhiteSpace(box.Number))
                {
                    list.Add((nameof(TaxForm.Fields), S["Every field needs its box or line number."]));
                }
                else if (box.Number.Contains('[', StringComparison.Ordinal) || box.Number.Contains(']', StringComparison.Ordinal) || box.Number.Contains(':', StringComparison.Ordinal))
                {
                    list.Add((box.Number, S["Field {0}: a number has no brackets or colons.", box.Number]));
                }
                else if (!seen.Add(box.Number))
                {
                    list.Add((box.Number, level == "Recipient" ? S["Two recipient fields are numbered {0}.", box.Number] : S["Two filing fields are numbered {0}.", box.Number]));
                }
            }
        }

        foreach (var box in form.Fields.Where(box => box.Level == TaxFormFieldLevel.Filing && !string.IsNullOrWhiteSpace(box.Number)))
        {
            if (form.Fields.Any(other => other.Level == TaxFormFieldLevel.Recipient && other.AddToFiling && other.Number == box.Number))
            {
                list.Add((box.Number, S["Field {0} is a filing field and a recipient field added up on the filing: give one of them another number.", box.Number]));
            }
        }

        if (list.Count > 0)
        {
            return null;
        }

        var formulas = new TaxFormFormulas(form);
        var recipientScope = new Scope(formulas._recipientSlots);
        var filingScope = new Scope(formulas._filingSlots);

        foreach (var box in form.Fields)
        {
            if (string.IsNullOrWhiteSpace(box.Formula))
            {
                continue;
            }

            var compiled = TryCompile(box.Formula, box.Level == TaxFormFieldLevel.Recipient ? recipientScope : filingScope, functions, S, box.Number, list);

            if (compiled is not null)
            {
                formulas.Compiled[box] = compiled;
            }
        }

        if (!string.IsNullOrWhiteSpace(form.RecipientCondition))
        {
            formulas.RecipientCondition = TryCompile(form.RecipientCondition, recipientScope, functions, S, nameof(TaxForm.RecipientCondition), list);
        }

        foreach (var check in form.Checks)
        {
            if (string.IsNullOrWhiteSpace(check.Condition))
            {
                list.Add((nameof(TaxForm.Checks), S["Every check needs its condition."]));

                continue;
            }

            if (TryCompile(check.Condition, filingScope, functions, S, nameof(TaxForm.Checks), list) is { } condition)
            {
                formulas.Checks.Add((check, condition));
            }
        }

        if (list.Count > 0)
        {
            return null;
        }

        formulas.RecipientFields = formulas.Order(formulas.RecipientFields, S, list);
        formulas.FilingFields = formulas.Order(formulas.FilingFields, S, list);

        return list.Count > 0 ? null : formulas;
    }

    /// <summary>
    /// Makes a row of a recipient's fields for the formulas.
    /// </summary>
    /// <param name="context">The formula context.</param>
    /// <returns>The row.</returns>
    public object[] RecipientRow(TaxFormFormulaContext context)
        => NewRow(_recipientSlots, context);

    /// <summary>
    /// Makes a row of the filing's fields for the formulas.
    /// </summary>
    /// <param name="context">The formula context.</param>
    /// <returns>The row.</returns>
    public object[] FilingRow(TaxFormFormulaContext context)
        => NewRow(_filingSlots, context);

    /// <summary>Gets the slot of a recipient field in a recipient row.</summary>
    /// <param name="key">The box's number.</param>
    /// <returns>The slot.</returns>
    public int RecipientSlot(string key)
        => _recipientSlots[key];

    /// <summary>Gets the slot of a filing field (or a totaled recipient field) in a filing row.</summary>
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
        => TaxFormFunctions.Decimal(value) ?? 0m;

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
                errors.Add((field, S["{0}: a tax form's formula can't use SUM, AVG, COUNT or another aggregate; use TOTAL to add up a recipient field.", field]));

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

    // Orders fields so each comes after the fields of its level it reads; a field that reads itself through others is wrong.
    private List<TaxFormField> Order(IReadOnlyList<TaxFormField> boxes, IStringLocalizer S, List<(string Field, string Message)> errors)
    {
        var byKey = boxes.ToDictionary(box => box.Number, StringComparer.Ordinal);
        var ordered = new List<TaxFormField>();
        var state = new Dictionary<TaxFormField, int>();

        bool Visit(TaxFormField box)
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

    private static object[] NewRow(Dictionary<string, int> slots, TaxFormFormulaContext context)
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
/// A recipient a source hands to <see cref="TaxFormEvaluator"/>: what its formulas read, and anything the source wants
/// back with the result.
/// </summary>
public sealed class TaxFormRecipientInput
{
    /// <summary>Gets what the recipient's formulas read, such as a person's pay statements.</summary>
    public required object Data { get; init; }

    /// <summary>Gets what the source keeps with the recipient, such as the person and the jurisdiction of the statement.</summary>
    public object Tag { get; init; }
}

/// <summary>
/// A recipient that was reported, with its fields.
/// </summary>
/// <param name="Input">The recipient as the source handed it.</param>
/// <param name="Fields">The fields kept on the recipient's statement: each with a formula, and a field with none only when the source reported an amount to it.</param>
public sealed record TaxFormRecipientResult(TaxFormRecipientInput Input, IReadOnlyDictionary<string, decimal> Fields);

/// <summary>
/// What a tax form works out to.
/// </summary>
public sealed class TaxFormEvaluationResult
{
    /// <summary>Gets the recipients reported, in the order they were handed.</summary>
    public IList<TaxFormRecipientResult> Recipients { get; } = [];

    /// <summary>Gets the filing's fields: the totals of the recipient fields added up on it, and the filing fields.</summary>
    public IDictionary<string, decimal> Fields { get; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>Gets the notes of the checks that failed, in order.</summary>
    public IList<string> FailedChecks { get; } = [];
}

/// <summary>
/// What a source hands <see cref="TaxFormEvaluator"/> to work a form out.
/// </summary>
public sealed class TaxFormEvaluation
{
    /// <summary>Gets the form's compiled formulas.</summary>
    public required TaxFormFormulas Formulas { get; init; }

    /// <summary>Gets the formula context: the period and the services.</summary>
    public required TaxFormFormulaContext Context { get; init; }

    /// <summary>Gets the recipients, in the order they are reported.</summary>
    public required IEnumerable<TaxFormRecipientInput> Recipients { get; init; }

    /// <summary>Gets what the filing fields and the checks read, such as every reported pay statement.</summary>
    public required object FilingData { get; init; }

    /// <summary>
    /// Gets the amount the source's records report to a field, given what the field is worked out on; added to the
    /// field's formula when the field accepts reported amounts. <see langword="null"/> when the source has none.
    /// </summary>
    public Func<TaxFormField, object, decimal> ReportedAmount { get; init; }
}

/// <summary>
/// Works a tax form out for any source: each recipient's fields in the order their formulas need, whether the recipient
/// is reported, the totals of the recipient fields added up on the filing, the filing fields and the checks.
/// </summary>
public static class TaxFormEvaluator
{
    /// <summary>
    /// Works a form out.
    /// </summary>
    /// <param name="evaluation">The form, the recipients and what the filing reads.</param>
    /// <returns>The recipients reported, the filing's fields and the failed checks.</returns>
    public static TaxFormEvaluationResult Evaluate(TaxFormEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var formulas = evaluation.Formulas;
        var context = evaluation.Context;
        var result = new TaxFormEvaluationResult();
        var totals = formulas.TotaledFields.ToDictionary(field => field.Number, _ => 0m, StringComparer.Ordinal);

        if (formulas.RecipientFields.Count > 0)
        {
            foreach (var recipient in evaluation.Recipients)
            {
                var (row, fields) = WorkRecipient(formulas, context, recipient.Data, evaluation.ReportedAmount);
                var include = formulas.RecipientCondition is { } condition
                    ? TaxFormFormulas.IsTrue(condition.Evaluate(new ExpressionContext { Row = row }))
                    : fields.Values.Any(value => value != 0);

                if (!include)
                {
                    continue;
                }

                foreach (var field in formulas.TotaledFields)
                {
                    totals[field.Number] += (decimal)row[formulas.RecipientSlot(field.Number)];
                }

                context.Recipients.Add(fields);
                result.Recipients.Add(new TaxFormRecipientResult(recipient, fields));
            }
        }

        // The filing fields, after the totals of the recipient fields.
        context.Data = evaluation.FilingData;

        var filingRow = formulas.FilingRow(context);

        foreach (var field in formulas.TotaledFields)
        {
            filingRow[formulas.FilingSlot(field.Number)] = totals[field.Number];

            if (!string.IsNullOrWhiteSpace(field.Formula) || totals[field.Number] != 0)
            {
                result.Fields[field.Number] = totals[field.Number];
            }
        }

        foreach (var field in formulas.FilingFields)
        {
            var value = Work(formulas, field, filingRow, evaluation.FilingData, evaluation.ReportedAmount);

            filingRow[formulas.FilingSlot(field.Number)] = value;

            if (!string.IsNullOrWhiteSpace(field.Formula) || value != 0)
            {
                result.Fields[field.Number] = value;
            }
        }

        foreach (var (check, condition) in formulas.Checks)
        {
            if (!TaxFormFormulas.IsTrue(condition.Evaluate(new ExpressionContext { Row = filingRow })))
            {
                result.FailedChecks.Add(check.Note ?? check.Condition);
            }
        }

        return result;
    }

    /// <summary>
    /// Works out one recipient's fields, as a filing would, whether the recipient would be reported or not.
    /// </summary>
    /// <param name="formulas">The form's compiled formulas.</param>
    /// <param name="context">The formula context.</param>
    /// <param name="data">What the recipient's formulas read.</param>
    /// <param name="reportedAmount">The amount the source's records report to a field, or <see langword="null"/>.</param>
    /// <returns>Every recipient field, zero or not.</returns>
    public static IReadOnlyDictionary<string, decimal> RecipientFields(TaxFormFormulas formulas, TaxFormFormulaContext context, object data, Func<TaxFormField, object, decimal> reportedAmount = null)
    {
        ArgumentNullException.ThrowIfNull(formulas);
        ArgumentNullException.ThrowIfNull(context);

        var (row, _) = WorkRecipient(formulas, context, data, reportedAmount);

        return formulas.RecipientFields.ToDictionary(field => field.Number, field => (decimal)row[formulas.RecipientSlot(field.Number)], StringComparer.Ordinal);
    }

    private static (object[] Row, Dictionary<string, decimal> Fields) WorkRecipient(TaxFormFormulas formulas, TaxFormFormulaContext context, object data, Func<TaxFormField, object, decimal> reportedAmount)
    {
        context.Data = data;

        var row = formulas.RecipientRow(context);
        var fields = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var field in formulas.RecipientFields)
        {
            var value = Work(formulas, field, row, data, reportedAmount);

            row[formulas.RecipientSlot(field.Number)] = value;

            if (!string.IsNullOrWhiteSpace(field.Formula) || value != 0)
            {
                fields[field.Number] = value;
            }
        }

        return (row, fields);
    }

    private static decimal Work(TaxFormFormulas formulas, TaxFormField field, object[] row, object data, Func<TaxFormField, object, decimal> reportedAmount)
    {
        var value = formulas.Compiled.TryGetValue(field, out var compiled)
            ? TaxFormFormulas.Amount(compiled.Evaluate(new ExpressionContext { Row = row }))
            : 0m;

        if (field.AcceptsReportedAmounts && reportedAmount is not null)
        {
            value += reportedAmount(field, data);
        }

        return value;
    }
}

/// <summary>
/// Registers Tax Forms' services.
/// </summary>
public static class TaxFormsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tax form store, manager, formula functions and recipe seeder. The handler that checks a form is registered by the
    /// Tax Forms feature, with its recipe step.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTaxFormsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TaxFormStore>();
        services.TryAddScoped<CrestApps.OrchardCore.Taxation.Services.ITaxFormManager, TaxFormManager>();
        services.TryAddScoped<CrestApps.Core.Services.ICatalogManager<TaxForm>>(serviceProvider => serviceProvider.GetRequiredService<CrestApps.OrchardCore.Taxation.Services.ITaxFormManager>());
        services.TryAddSingleton<TaxFormFunctionCatalog>();
        services.TryAddScoped<TaxFormRecipeSeeder>();
        services.AddTaxFormFunctions<TaxFormFunctions>();

        return services;
    }

    /// <summary>
    /// Adds functions the formulas of tax forms can call: every form's, or one source's.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTaxFormFunctions<TProvider>(this IServiceCollection services)
        where TProvider : class, ITaxFormFunctionProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITaxFormFunctionProvider, TProvider>());

        return services;
    }
}
