using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Checks.Workflows;

/// <summary>
/// Starts when a check is voided, stopped or replaced by a reissued check.
/// </summary>
public sealed class CheckVoidedEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckVoidedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckVoidedEvent(IStringLocalizer<CheckVoidedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckVoidedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Voided"];
}
