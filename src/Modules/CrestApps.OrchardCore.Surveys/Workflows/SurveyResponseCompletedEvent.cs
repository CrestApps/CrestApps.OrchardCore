using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a survey response is completed.
/// </summary>
public sealed class SurveyResponseCompletedEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseCompletedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponseCompletedEvent(IStringLocalizer<SurveyResponseCompletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponseCompletedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Completed"];
}
