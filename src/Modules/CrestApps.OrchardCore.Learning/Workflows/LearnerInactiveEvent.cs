using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Starts once when a learner has not worked on a course for a while.
/// </summary>
public sealed class LearnerInactiveEvent : LearningEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LearnerInactiveEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearnerInactiveEvent(IStringLocalizer<LearnerInactiveEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(LearnerInactiveEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Learner Inactive"];
}
