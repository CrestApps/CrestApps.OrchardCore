using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a graded survey response reaches the pass mark.
/// </summary>
public sealed class SurveyResponsePassedEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponsePassedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponsePassedEvent(IStringLocalizer<SurveyResponsePassedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponsePassedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Passed"];
}
