using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a completed survey response has answers a person must grade.
/// </summary>
public sealed class SurveyResponseNeedsGradingEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseNeedsGradingEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponseNeedsGradingEvent(IStringLocalizer<SurveyResponseNeedsGradingEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponseNeedsGradingEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Needs Grading"];
}
