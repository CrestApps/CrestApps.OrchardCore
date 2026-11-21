using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a survey version is published.
/// </summary>
public sealed class SurveyPublishedEvent : SurveyEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPublishedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyPublishedEvent(IStringLocalizer<SurveyPublishedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyPublishedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Published"];
}
