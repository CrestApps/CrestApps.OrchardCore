using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// Starts when a quiz attempt is submitted, by the learner or when the time is up.
/// </summary>
public sealed class AttemptSubmittedEvent : AttemptEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptSubmittedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AttemptSubmittedEvent(IStringLocalizer<AttemptSubmittedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AttemptSubmittedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Quiz Attempt Submitted"];
}
