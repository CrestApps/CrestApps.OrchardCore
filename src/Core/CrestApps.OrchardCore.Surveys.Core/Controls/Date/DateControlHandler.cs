using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Date;

/// <summary>
/// A date, a time or both. The answer keeps the wall-clock value the respondent entered, as a pure date does, so it is
/// never shifted to another time zone.
/// </summary>
public sealed class DateControlHandler : SurveyControlHandlerBase<DateSettings>
{
    private static readonly string[] _dateFormats = ["yyyy-MM-dd"];
    private static readonly string[] _timeFormats = ["HH:mm", "HH:mm:ss"];
    private static readonly string[] _dateTimeFormats = ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "o"];

    /// <summary>
    /// Initializes a new instance of the <see cref="DateControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public DateControlHandler(IStringLocalizer<DateControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Date;

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var text = ReadValue(context, context.Prefix);
        var mode = context.Question.GetSettings<DateSettings>().Mode;

        if (text is null)
        {
            context.Answer = null;
        }
        else if (TryParse(text, mode, out var value))
        {
            context.Answer = new SurveyAnswer { Date = value };
        }
        else
        {
            context.Answer = null;
            context.Errors.Add(mode == DateMode.Time ? S["Enter a valid time."] : S["Enter a valid date."]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        await base.ValidateAnswerAsync(context);

        if (context.Answer?.Date is not { } value)
        {
            return;
        }

        var settings = context.Question.GetSettings<DateSettings>();

        if (settings.Min is { } min && value < min)
        {
            context.Errors.Add(S["Enter a value on or after {0}.", Format(min, settings.Mode, CultureInfo.CurrentCulture)]);
        }

        if (settings.Max is { } max && value > max)
        {
            context.Errors.Add(S["Enter a value on or before {0}.", Format(max, settings.Mode, CultureInfo.CurrentCulture)]);
        }
    }

    /// <inheritdoc />
    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => question.GetSettings<DateSettings>().Mode == DateMode.Date ? DataFieldType.Date : DataFieldType.DateTime;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        if (answer?.Date is not { } value)
        {
            return string.Empty;
        }

        var mode = question.GetSettings<DateSettings>().Mode;

        if (context?.UseValues == true)
        {
            return InputValue(value, mode);
        }

        return Format(value, mode, context?.Culture ?? CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a value as an HTML input expects it.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The text.</returns>
    public static string InputValue(DateTime value, DateMode mode)
        => mode switch
        {
            DateMode.Time => value.ToString("HH:mm", CultureInfo.InvariantCulture),
            DateMode.DateTime => value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            _ => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

    private static string Format(DateTime value, DateMode mode, CultureInfo culture)
        => mode switch
        {
            DateMode.Time => value.ToString("t", culture),
            DateMode.DateTime => value.ToString("g", culture),
            _ => value.ToString("d", culture),
        };

    private static bool TryParse(string text, DateMode mode, out DateTime value)
    {
        var formats = mode switch
        {
            DateMode.Time => _timeFormats,
            DateMode.DateTime => _dateTimeFormats,
            _ => _dateFormats,
        };

        if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            if (mode == DateMode.Time)
            {
                value = new DateTime(2000, 1, 1, value.Hour, value.Minute, value.Second, DateTimeKind.Unspecified);
            }

            value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

            return true;
        }

        return false;
    }
}
