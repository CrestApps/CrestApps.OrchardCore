using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// Starts when a learner starts a quiz attempt.
/// </summary>
public sealed class AttemptStartedEvent : AttemptEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptStartedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AttemptStartedEvent(IStringLocalizer<AttemptStartedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AttemptStartedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Quiz Attempt Started"];
}
