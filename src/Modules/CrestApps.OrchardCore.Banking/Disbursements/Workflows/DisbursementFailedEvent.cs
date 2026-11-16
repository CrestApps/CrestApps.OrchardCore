using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a disbursement fails or is cancelled, such as when its check is voided.
/// </summary>
public sealed class DisbursementFailedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementFailedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementFailedEvent(IStringLocalizer<DisbursementFailedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementFailedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Failed"];
}
