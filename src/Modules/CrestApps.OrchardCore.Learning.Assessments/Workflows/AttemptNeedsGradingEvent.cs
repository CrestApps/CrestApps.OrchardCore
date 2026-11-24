using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// Starts when a submitted quiz attempt has answers a person must grade.
/// </summary>
public sealed class AttemptNeedsGradingEvent : AttemptEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptNeedsGradingEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AttemptNeedsGradingEvent(IStringLocalizer<AttemptNeedsGradingEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AttemptNeedsGradingEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Quiz Attempt Needs Grading"];
}
