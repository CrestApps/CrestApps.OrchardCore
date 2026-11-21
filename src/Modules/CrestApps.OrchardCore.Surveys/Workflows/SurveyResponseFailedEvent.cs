using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a graded survey response stays under the pass mark.
/// </summary>
public sealed class SurveyResponseFailedEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseFailedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponseFailedEvent(IStringLocalizer<SurveyResponseFailedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponseFailedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Failed"];
}
