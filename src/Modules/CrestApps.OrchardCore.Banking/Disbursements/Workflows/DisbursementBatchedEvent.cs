using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a disbursement is put in a batch; BatchId is the batch.
/// </summary>
public sealed class DisbursementBatchedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBatchedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementBatchedEvent(IStringLocalizer<DisbursementBatchedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementBatchedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Added to Batch"];
}
