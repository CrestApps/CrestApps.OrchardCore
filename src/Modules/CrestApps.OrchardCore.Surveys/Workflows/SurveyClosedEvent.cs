using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a survey closes, by hand, at its close date or when it has all its responses.
/// </summary>
public sealed class SurveyClosedEvent : SurveyEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyClosedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyClosedEvent(IStringLocalizer<SurveyClosedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyClosedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Closed"];
}
