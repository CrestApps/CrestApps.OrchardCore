using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when an open batch is cancelled and its disbursements go back to pending.
/// </summary>
public sealed class DisbursementBatchCanceledEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBatchCanceledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementBatchCanceledEvent(IStringLocalizer<DisbursementBatchCanceledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementBatchCanceledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Batch Canceled"];
}
