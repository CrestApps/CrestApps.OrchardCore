using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a survey response starts.
/// </summary>
public sealed class SurveyResponseStartedEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseStartedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponseStartedEvent(IStringLocalizer<SurveyResponseStartedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponseStartedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Started"];
}
