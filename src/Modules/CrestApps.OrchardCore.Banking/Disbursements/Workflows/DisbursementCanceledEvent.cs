using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a disbursement is cancelled, such as when its check is voided or stopped.
/// </summary>
public sealed class DisbursementCanceledEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementCanceledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementCanceledEvent(IStringLocalizer<DisbursementCanceledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementCanceledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Canceled"];
}
