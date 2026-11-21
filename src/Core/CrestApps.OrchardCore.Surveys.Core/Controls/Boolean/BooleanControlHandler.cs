using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;

/// <summary>
/// Yes or no, true or false, or a consent box. The answer is <c>true</c> or <c>false</c>.
/// </summary>
public sealed class BooleanControlHandler : SurveyControlHandlerBase<BooleanSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public BooleanControlHandler(IStringLocalizer<BooleanControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Boolean;

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = ReadValues(context, context.Prefix).LastOrDefault();
        var settings = context.Question.GetSettings<BooleanSettings>();

        context.Answer = Parse(value) switch
        {
            true => new SurveyAnswer { Values = [BooleanValues.True] },
            false => new SurveyAnswer { Values = [BooleanValues.False] },
            null when settings.Display == BooleanDisplay.ConsentCheckbox && context.Form is not null => new SurveyAnswer { Values = [BooleanValues.False] },
            _ => null,
        };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.Question.GetSettings<BooleanSettings>();
        var value = context.Answer?.Values?.FirstOrDefault();

        if (context.IsRequired && settings.Display == BooleanDisplay.ConsentCheckbox && value != BooleanValues.True)
        {
            context.Errors.Add(S["Check the box to continue."]);
        }
        else if (context.IsRequired && value is null)
        {
            context.Errors.Add(S["This question requires an answer."]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => Parse(answer?.Values?.FirstOrDefault());

    /// <inheritdoc />
    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Boolean;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        var value = Parse(answer?.Values?.FirstOrDefault());

        if (value is null)
        {
            return string.Empty;
        }

        if (context?.UseValues == true)
        {
            return value.Value ? BooleanValues.True : BooleanValues.False;
        }

        var settings = question.GetSettings<BooleanSettings>();

        return value.Value ? TrueLabel(settings) : FalseLabel(settings);
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        if (Parse(answer?.Values?.FirstOrDefault()) is { } value)
        {
            yield return new SurveyAnswerIndexRow { ChoiceId = value ? BooleanValues.True : BooleanValues.False };
        }
    }

    /// <summary>
    /// Gets the label of yes.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The label.</returns>
    public string TrueLabel(BooleanSettings settings)
        => !string.IsNullOrWhiteSpace(settings?.TrueLabel)
            ? settings.TrueLabel
            : settings?.Display == BooleanDisplay.TrueFalse ? S["True"] : S["Yes"];

    /// <summary>
    /// Gets the label of no.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The label.</returns>
    public string FalseLabel(BooleanSettings settings)
        => !string.IsNullOrWhiteSpace(settings?.FalseLabel)
            ? settings.FalseLabel
            : settings?.Display == BooleanDisplay.TrueFalse ? S["False"] : S["No"];

    private static bool? Parse(string value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "true" or "on" or "yes" or "1" => true,
            "false" or "off" or "no" or "0" => false,
            _ => null,
        };
}
