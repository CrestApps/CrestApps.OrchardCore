using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// A question type handler with defaults for every method: the answer is the text values posted under the question's
/// prefix, conditions compare those values, and the export has one column. A simple control overrides two or three
/// methods.
/// </summary>
/// <typeparam name="TSettings">The type of the control's settings.</typeparam>
public abstract class SurveyControlHandlerBase<TSettings> : ISurveyControlHandler
    where TSettings : class, new()
{
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(100);

    // The most time the pattern rules of one answer may take together, whatever the number of values it has, when the
    // caller gives no budget shared by the request.
    private static readonly TimeSpan _regexBudget = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyControlHandlerBase{TSettings}"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer of the handler's messages.</param>
    protected SurveyControlHandlerBase(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public abstract string ControlType { get; }

    /// <inheritdoc />
    public virtual void Initialize(SurveyQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        question.SetSettings(new TSettings());
    }

    /// <inheritdoc />
    public virtual Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var rule in question.Validation ?? [])
        {
            if (rule?.Kind == SurveyValidationKind.Regex && !IsValidPattern(rule.Value))
            {
                context.AddError("invalid-regex", S["The pattern of question {0} is not a valid regular expression.", question.Name], question.Id);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual void PreparePresentation(SurveyQuestion question, SurveyPresentationContext context)
    {
    }

    /// <inheritdoc />
    public virtual JsonObject GetRespondentSettings(SurveyQuestion question)
        => SurveyQuestionSettingsExtensions.WithoutKey(question.GetSettings<TSettings>());

    /// <inheritdoc />
    public virtual Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var values = ReadValues(context, context.Prefix);

        context.Answer = values.Count == 0 ? null : new SurveyAnswer { Values = values };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsEmpty(context.Question, context.Answer))
        {
            if (context.IsRequired)
            {
                context.Errors.Add(S["This question requires an answer."]);
            }

            return Task.CompletedTask;
        }

        ValidateRules(context);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual bool IsEmpty(SurveyQuestion question, SurveyAnswer answer)
        => answer is null
            || (!(answer.Values?.Any(value => !string.IsNullOrWhiteSpace(value)) ?? false)
                && answer.Number is null
                && answer.Date is null
                && !(answer.Rows?.Values.Any(row => row?.Any(value => !string.IsNullOrWhiteSpace(value)) ?? false) ?? false)
                && !(answer.AttachmentIds?.Count > 0)
                && string.IsNullOrWhiteSpace(answer.OtherText));

    /// <inheritdoc />
    public virtual ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
    {
        if (answer is null)
        {
            return ConditionValue.Empty;
        }

        if (!string.IsNullOrEmpty(rowId))
        {
            return answer.Rows is not null && answer.Rows.TryGetValue(rowId, out var row)
                ? new ConditionValue(row ?? [])
                : ConditionValue.Empty;
        }

        if (answer.Number is { } number)
        {
            return ConditionValue.Of(number.ToString(CultureInfo.InvariantCulture));
        }

        if (answer.Date is { } date)
        {
            return ConditionValue.Of(date.ToString("o", CultureInfo.InvariantCulture));
        }

        return new ConditionValue(answer.Values ?? []);
    }

    /// <inheritdoc />
    public virtual object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
    {
        if (answer is null)
        {
            return null;
        }

        if (answer.Number is { } number)
        {
            return number;
        }

        if (answer.Date is { } date)
        {
            return date;
        }

        var values = !string.IsNullOrEmpty(rowId)
            ? answer.Rows is not null && answer.Rows.TryGetValue(rowId, out var row) ? row : null
            : answer.Values;

        return values is { Count: > 0 } ? string.Join(", ", values) : null;
    }

    /// <inheritdoc />
    public virtual DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Text;

    /// <inheritdoc />
    public virtual string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        if (answer is null)
        {
            return string.Empty;
        }

        var culture = context?.UseValues == true ? CultureInfo.InvariantCulture : context?.Culture ?? CultureInfo.InvariantCulture;

        if (answer.Number is { } number)
        {
            return number.ToString(culture);
        }

        if (answer.Date is { } date)
        {
            return context?.UseValues == true
                ? date.ToString("o", CultureInfo.InvariantCulture)
                : date.ToString("g", culture);
        }

        return string.Join(", ", (answer.Values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    /// <inheritdoc />
    public virtual IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        yield return new SurveyExportColumn
        {
            Key = question.Name,
            Title = question.Title,
            QuestionId = question.Id,
            Type = GetFormulaType(question, null),
        };
    }

    /// <inheritdoc />
    public virtual void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(row);

        row[question.Name] = GetFormulaType(question, null) == DataFieldType.Text
            ? FormatAnswer(question, answer, context)
            : GetFormulaValue(question, answer, null);
    }

    /// <inheritdoc />
    public virtual SurveyAnswer RemovePersonalData(SurveyQuestion question, SurveyAnswer answer)
        => answer;

    /// <inheritdoc />
    public virtual IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        if (answer is null)
        {
            yield break;
        }

        if (answer.Number is not null || answer.Date is not null)
        {
            yield return new SurveyAnswerIndexRow { NumberValue = answer.Number, DateValue = answer.Date };

            yield break;
        }

        foreach (var value in answer.Values ?? [])
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return new SurveyAnswerIndexRow { TextValue = Cut(value, 255) };
            }
        }
    }

    /// <summary>
    /// Reads the non-blank values posted under a name, from the form or the JSON body.
    /// </summary>
    /// <param name="context">The read context.</param>
    /// <param name="name">The input name.</param>
    /// <returns>The trimmed values.</returns>
    protected static List<string> ReadValues(SurveyAnswerReadContext context, string name)
    {
        ArgumentNullException.ThrowIfNull(context);

        var values = new List<string>();

        if (context.Form is not null)
        {
            if (context.Form.TryGetValue(name, out var posted))
            {
                foreach (var value in posted)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value.Trim());
                    }
                }
            }

            return values;
        }

        var node = name == context.Prefix
            ? context.Json
            : context.Json is JsonObject json && name.StartsWith(context.Prefix + ".", StringComparison.Ordinal)
                ? json[name.Substring(context.Prefix.Length + 1)]
                : null;

        AddJsonValues(node, values);

        return values;
    }

    /// <summary>
    /// Reads one non-blank value posted under a name, or <see langword="null"/>.
    /// </summary>
    /// <param name="context">The read context.</param>
    /// <param name="name">The input name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    protected static string ReadValue(SurveyAnswerReadContext context, string name)
        => ReadValues(context, name).FirstOrDefault();

    /// <summary>
    /// Reads a number posted under a name, in the invariant culture first and then the current culture.
    /// </summary>
    /// <param name="context">The read context.</param>
    /// <param name="name">The input name.</param>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when a number was posted.</returns>
    protected static bool TryReadNumber(SurveyAnswerReadContext context, string name, out decimal number)
        => TryParseNumber(ReadValue(context, name), out number);

    /// <summary>
    /// Parses a number in the invariant culture first and then the current culture.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when the text is a number.</returns>
    protected static bool TryParseNumber(string value, out decimal number)
    {
        number = 0;

        return !string.IsNullOrWhiteSpace(value)
            && (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
                || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out number));
    }

    /// <summary>
    /// Applies the question's validation rules to the answer's text values and number.
    /// </summary>
    /// <param name="context">The validation context.</param>
    protected void ValidateRules(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var answer = context.Answer;
        var texts = (answer?.Values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var patternBudget = context.PatternBudget ?? new SurveyPatternBudget(_regexBudget);

        foreach (var rule in context.Question?.Validation ?? [])
        {
            if (rule is null)
            {
                continue;
            }

            var failed = rule.Kind switch
            {
                SurveyValidationKind.Regex => texts.Any(text => !Matches(text, rule.Value, patternBudget)),
                SurveyValidationKind.MinLength => int.TryParse(rule.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var min) && texts.Any(text => text.Length < min),
                SurveyValidationKind.MaxLength => int.TryParse(rule.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) && texts.Any(text => text.Length > max),
                SurveyValidationKind.Min => TryParseNumber(rule.Value, out var low) && NumbersOf(answer).Any(number => number < low),
                SurveyValidationKind.Max => TryParseNumber(rule.Value, out var high) && NumbersOf(answer).Any(number => number > high),
                SurveyValidationKind.Email => texts.Any(text => !IsEmail(text)),
                SurveyValidationKind.Url => texts.Any(text => !Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)),
                _ => false,
            };

            if (failed)
            {
                context.Errors.Add(!string.IsNullOrWhiteSpace(rule.Message) ? rule.Message : DefaultMessage(rule));
            }
        }
    }

    /// <summary>
    /// Tells whether a text is an e-mail address.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns><see langword="true"/> when it is one.</returns>
    protected static bool IsEmail(string value)
        => !string.IsNullOrWhiteSpace(value)
            && MailAddress.TryCreate(value.Trim(), out var address)
            && string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cuts a text to a length.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="length">The length.</param>
    /// <returns>The text, cut.</returns>
    protected static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;

    private static IEnumerable<decimal> NumbersOf(SurveyAnswer answer)
    {
        if (answer?.Number is { } number)
        {
            yield return number;

            yield break;
        }

        foreach (var value in answer?.Values ?? [])
        {
            if (TryParseNumber(value, out var parsed))
            {
                yield return parsed;
            }
        }
    }

    private string DefaultMessage(SurveyValidationRule rule)
        => rule.Kind switch
        {
            SurveyValidationKind.MinLength => S["The answer must have at least {0} characters.", rule.Value],
            SurveyValidationKind.MaxLength => S["The answer must have at most {0} characters.", rule.Value],
            SurveyValidationKind.Min => S["The answer must be at least {0}.", rule.Value],
            SurveyValidationKind.Max => S["The answer must be at most {0}.", rule.Value],
            SurveyValidationKind.Email => S["Enter a valid e-mail address."],
            SurveyValidationKind.Url => S["Enter a valid web address."],
            _ => S["The answer is not in the expected format."],
        };

    // A pattern runs without backtracking, in time linear in the text, when it can; a pattern that needs backtracking
    // (back references, lookarounds, atomic groups) runs with a timeout per match and within a budget for the whole
    // request (or the answer, without a shared budget), and fails when the time is up.
    private static bool Matches(string text, string pattern, SurveyPatternBudget budget)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return true;
        }

        var remaining = budget.Remaining;

        if (remaining <= TimeSpan.Zero)
        {
            return false;
        }

        var timeout = remaining < _regexTimeout ? remaining : _regexTimeout;
        var started = Stopwatch.GetTimestamp();

        try
        {
            try
            {
                return Regex.IsMatch(text, pattern, RegexOptions.NonBacktracking, timeout);
            }
            catch (NotSupportedException)
            {
                return Regex.IsMatch(text, pattern, RegexOptions.None, timeout);
            }
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        finally
        {
            budget.SpendSince(started);
        }
    }

    private static bool IsValidPattern(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.None, _regexTimeout);

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void AddJsonValues(JsonNode node, List<string> values)
    {
        switch (node)
        {
            case null:
                return;
            case JsonArray array:
                foreach (var item in array)
                {
                    AddJsonValues(item, values);
                }

                return;
            case JsonValue value:
                var text = value.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text.Trim());
                }

                return;
        }
    }
}
