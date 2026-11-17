using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a batch is made from pending disbursements.
/// </summary>
public sealed class DisbursementBatchCreatedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBatchCreatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementBatchCreatedEvent(IStringLocalizer<DisbursementBatchCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementBatchCreatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Batch Created"];
}
