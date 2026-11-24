using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Starts once when the access of an enrollment ends soon.
/// </summary>
public sealed class AccessExpiringEvent : LearningEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccessExpiringEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AccessExpiringEvent(IStringLocalizer<AccessExpiringEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AccessExpiringEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Access Expiring"];
}
