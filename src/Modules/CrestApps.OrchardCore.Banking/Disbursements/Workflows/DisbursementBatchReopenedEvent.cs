using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a batch stuck in sending is reopened to be sent again; Reason says why.
/// </summary>
public sealed class DisbursementBatchReopenedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBatchReopenedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementBatchReopenedEvent(IStringLocalizer<DisbursementBatchReopenedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementBatchReopenedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Batch Reopened"];
}
