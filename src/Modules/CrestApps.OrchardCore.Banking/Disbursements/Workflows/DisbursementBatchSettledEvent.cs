using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a batch is settled, by hand, by the Settle Disbursement Batches task, or at release when the provider paid everything; Settled counts the disbursements paid.
/// </summary>
public sealed class DisbursementBatchSettledEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBatchSettledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementBatchSettledEvent(IStringLocalizer<DisbursementBatchSettledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementBatchSettledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Batch Settled"];
}
