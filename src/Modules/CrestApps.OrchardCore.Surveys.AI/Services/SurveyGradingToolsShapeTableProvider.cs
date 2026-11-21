using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Shows the scoring help button in the tools of each answer on the Surveys grading page, through the
/// <c>SurveyGradingQuestionTools__AI</c> alternate.
/// </summary>
internal sealed class SurveyGradingToolsShapeTableProvider : IShapeTableProvider
{
    /// <inheritdoc />
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("SurveyGradingQuestionTools")
            .OnDisplaying(displaying => displaying.Shape.Metadata.Alternates.Add("SurveyGradingQuestionTools__AI"));

        return ValueTask.CompletedTask;
    }
}
