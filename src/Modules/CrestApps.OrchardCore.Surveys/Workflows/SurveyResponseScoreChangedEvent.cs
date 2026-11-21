using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when the score of a graded survey response changes: a regrade or a change by staff.
/// </summary>
public sealed class SurveyResponseScoreChangedEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseScoreChangedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponseScoreChangedEvent(IStringLocalizer<SurveyResponseScoreChangedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponseScoreChangedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Score Changed"];
}
