using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// Starts when a graded quiz attempt is overridden, regraded or voided.
/// </summary>
public sealed class AttemptScoreChangedEvent : AttemptEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptScoreChangedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AttemptScoreChangedEvent(IStringLocalizer<AttemptScoreChangedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AttemptScoreChangedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Quiz Score Changed"];
}
