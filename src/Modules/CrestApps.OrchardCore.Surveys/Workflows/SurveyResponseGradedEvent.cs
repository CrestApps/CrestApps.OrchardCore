using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when every answer of a survey response has a score, by a scorer or a grader.
/// </summary>
public sealed class SurveyResponseGradedEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseGradedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponseGradedEvent(IStringLocalizer<SurveyResponseGradedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponseGradedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Graded"];
}
