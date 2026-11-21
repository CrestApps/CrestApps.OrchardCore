using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;

/// <summary>
/// Summarizes a file upload question: how many responses uploaded files.
/// </summary>
public sealed class FileUploadAnalyzer : SurveyAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public FileUploadAnalyzer(IStringLocalizer<FileUploadAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.FileUpload;

    /// <inheritdoc />
    protected override Task FillAsync(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc />
    protected override IEnumerable<ReportSection> BuildSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context)
    {
        yield return ReportSection.ForMetrics(title, [AnsweredMetric(summary)]);
    }
}
