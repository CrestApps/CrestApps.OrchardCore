using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// Starts when the skills a person holds change: one is given, changed or taken away.
/// </summary>
public sealed class SkillsChangedEvent : SkillsEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkillsChangedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SkillsChangedEvent(IStringLocalizer<SkillsChangedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SkillsChangedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Skills Changed"];
}
