using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.ContactInfo;

/// <summary>
/// Name, e-mail, phone, company and address. Each field's answer is kept as a row named after the field
/// (<c>name</c>, <c>email</c>...), posted as <c>q.{questionId}.{field}</c>. It writes no index rows, so contact details
/// never reach aggregates.
/// </summary>
public sealed class ContactInfoControlHandler : SurveyControlHandlerBase<ContactInfoSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactInfoControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ContactInfoControlHandler(IStringLocalizer<ContactInfoControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.ContactInfo;

    /// <summary>
    /// Gets the row key of a field.
    /// </summary>
    /// <param name="kind">The field.</param>
    /// <returns>The key.</returns>
    public static string Key(ContactInfoFieldKind kind)
        => kind.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rows = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var field in context.Question.GetSettings<ContactInfoSettings>().Fields)
        {
            var value = ReadValue(context, $"{context.Prefix}.{Key(field.Kind)}");

            if (value is not null)
            {
                rows[Key(field.Kind)] = [Cut(value, field.Kind == ContactInfoFieldKind.Address ? 1000 : 255)];
            }
        }

        context.Answer = rows.Count == 0 ? null : new SurveyAnswer { Rows = rows };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var empty = IsEmpty(context.Question, context.Answer);

        if (empty && !context.IsRequired)
        {
            return Task.CompletedTask;
        }

        foreach (var field in context.Question.GetSettings<ContactInfoSettings>().Fields)
        {
            var value = Value(context.Answer, field.Kind);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (field.Required)
                {
                    context.Errors.Add(S["Enter your {0}.", Label(field)]);
                }

                continue;
            }

            if (field.Kind == ContactInfoFieldKind.Email && !IsEmail(value))
            {
                context.Errors.Add(S["Enter a valid e-mail address."]);
            }

            if (field.Kind == ContactInfoFieldKind.Phone && value.Count(char.IsDigit) < 5)
            {
                context.Errors.Add(S["Enter a valid phone number."]);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>Contact details name the person, so the whole answer goes.</remarks>
    public override SurveyAnswer RemovePersonalData(SurveyQuestion question, SurveyAnswer answer)
        => null;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => string.Join("; ", question.GetSettings<ContactInfoSettings>().Fields
            .Select(field => (field, Value: Value(answer, field.Kind)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => context?.UseValues == true ? entry.Value : $"{Label(entry.field)}: {entry.Value}"));

    /// <inheritdoc />
    public override IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        foreach (var field in question.GetSettings<ContactInfoSettings>().Fields)
        {
            yield return new SurveyExportColumn
            {
                Key = $"{question.Name}.{Key(field.Kind)}",
                Title = $"{question.Title}: {Label(field)}",
                QuestionId = question.Id,
                RowId = Key(field.Kind),
            };
        }
    }

    /// <inheritdoc />
    public override void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(row);

        foreach (var field in question.GetSettings<ContactInfoSettings>().Fields)
        {
            row[$"{question.Name}.{Key(field.Kind)}"] = Value(answer, field.Kind) ?? string.Empty;
        }
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
        => [];

    /// <summary>
    /// Gets the label of a field.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The label.</returns>
    public string Label(ContactInfoField field)
        => !string.IsNullOrWhiteSpace(field?.Label)
            ? field.Label
            : field?.Kind switch
            {
                ContactInfoFieldKind.Email => S["E-mail"],
                ContactInfoFieldKind.Phone => S["Phone"],
                ContactInfoFieldKind.Company => S["Company"],
                ContactInfoFieldKind.Address => S["Address"],
                _ => S["Name"],
            };

    private static string Value(SurveyAnswer answer, ContactInfoFieldKind kind)
        => answer?.Rows is not null && answer.Rows.TryGetValue(Key(kind), out var values) && values is { Count: > 0 } ? values[0] : null;
}
