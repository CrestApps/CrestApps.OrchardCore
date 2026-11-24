using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// Starts when every answer of a quiz attempt has a score.
/// </summary>
public sealed class AttemptGradedEvent : AttemptEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptGradedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AttemptGradedEvent(IStringLocalizer<AttemptGradedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AttemptGradedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Quiz Attempt Graded"];
}
