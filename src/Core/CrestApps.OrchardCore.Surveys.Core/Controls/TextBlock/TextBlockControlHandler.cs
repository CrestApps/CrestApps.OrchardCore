using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.TextBlock;

/// <summary>
/// Text and media that need no answer.
/// </summary>
public sealed class TextBlockControlHandler : SurveyControlHandlerBase<TextBlockSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextBlockControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public TextBlockControlHandler(IStringLocalizer<TextBlockControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.TextBlock;

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Answer = null;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
        => Task.CompletedTask;

    /// <inheritdoc />
    public override bool IsEmpty(SurveyQuestion question, SurveyAnswer answer)
        => true;

    /// <inheritdoc />
    public override IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
        => [];

    /// <inheritdoc />
    public override void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
        => [];
}
