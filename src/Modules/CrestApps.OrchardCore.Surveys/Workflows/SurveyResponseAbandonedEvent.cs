using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when an unfinished survey response is abandoned.
/// </summary>
public sealed class SurveyResponseAbandonedEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseAbandonedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponseAbandonedEvent(IStringLocalizer<SurveyResponseAbandonedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponseAbandonedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Abandoned"];
}
