using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts when a check is replaced by a new one; ReplacedById is the new check. Check Voided starts too.
/// </summary>
public sealed class CheckReissuedEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckReissuedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckReissuedEvent(IStringLocalizer<CheckReissuedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckReissuedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Reissued"];
}
