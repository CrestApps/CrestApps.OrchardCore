using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts when the bank is asked not to pay a check. Check Voided starts too.
/// </summary>
public sealed class CheckStopPaymentEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckStopPaymentEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckStopPaymentEvent(IStringLocalizer<CheckStopPaymentEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckStopPaymentEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Stop Payment"];
}
