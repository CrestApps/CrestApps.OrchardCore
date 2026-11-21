using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Slider;

/// <summary>
/// A number picked on a slider. A slider always has a value, so the respondent confirms it with a check box
/// (<c>q.{questionId}.set</c>) for the answer to count; without script the box is checked by default.
/// </summary>
public sealed class SliderControlHandler : SurveyControlHandlerBase<SliderSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SliderControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SliderControlHandler(IStringLocalizer<SliderControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Slider;

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        var settings = question.GetSettings<SliderSettings>();

        if (settings.Min >= settings.Max || settings.Step <= 0)
        {
            context.AddError("slider-range", S["The slider of question {0} needs a lowest value below its highest value and a step above 0.", question.Name], question.Id);
        }
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.Question.GetSettings<SliderSettings>();
        var set = context.Form is null || ReadValue(context, context.Prefix + ".set") is not null;

        context.Answer = set && TryReadNumber(context, context.Prefix, out var number)
            ? new SurveyAnswer { Number = Math.Clamp(number, settings.Min, settings.Max) }
            : null;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Decimal;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => answer?.Number is { } number
            ? number.ToString(context?.UseValues == true ? CultureInfo.InvariantCulture : context?.Culture ?? CultureInfo.InvariantCulture)
            : string.Empty;
}
