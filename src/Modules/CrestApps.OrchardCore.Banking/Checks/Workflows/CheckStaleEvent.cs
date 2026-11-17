using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts when an outstanding check becomes too old to pay.
/// </summary>
public sealed class CheckStaleEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckStaleEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckStaleEvent(IStringLocalizer<CheckStaleEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckStaleEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Stale"];
}
