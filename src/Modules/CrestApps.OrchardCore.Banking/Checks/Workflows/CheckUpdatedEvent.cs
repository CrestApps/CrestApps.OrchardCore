using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts when a check waiting to print is edited: its payee, address, amount, date or memo changed.
/// </summary>
public sealed class CheckUpdatedEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckUpdatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckUpdatedEvent(IStringLocalizer<CheckUpdatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckUpdatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Edited"];
}
