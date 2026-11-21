using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Rating;

/// <summary>
/// A rating on a scale of whole numbers. Net Promoter Score and customer satisfaction are presets of it.
/// </summary>
public sealed class RatingControlHandler : SurveyControlHandlerBase<RatingSettings>
{
    /// <summary>
    /// The longest scale allowed.
    /// </summary>
    public const int MaxScale = 11;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public RatingControlHandler(IStringLocalizer<RatingControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Rating;

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        var settings = question.GetSettings<RatingSettings>();

        if (settings.Min >= settings.Max || settings.Max - settings.Min + 1 > MaxScale || settings.Min < 0)
        {
            context.AddError("rating-scale", S["The scale of question {0} must run from 0 or more up to a larger value, with at most {1} values.", question.Name, MaxScale], question.Id);
        }
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.Question.GetSettings<RatingSettings>();

        context.Answer = TryReadNumber(context, context.Prefix, out var number)
            && number == Math.Floor(number)
            && number >= settings.Min
            && number <= settings.Max
                ? new SurveyAnswer { Number = number }
                : null;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Integer;

    /// <inheritdoc />
    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => answer?.Number is { } number ? (long)number : null;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => answer?.Number is { } number ? ((long)number).ToString(CultureInfo.InvariantCulture) : string.Empty;
}
