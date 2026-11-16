using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts when a batch is released to its provider: its bank file is written or its checks issued. Settled counts what the provider paid at once.
/// </summary>
public sealed class DisbursementBatchReleasedEvent : DisbursementEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBatchReleasedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementBatchReleasedEvent(IStringLocalizer<DisbursementBatchReleasedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(DisbursementBatchReleasedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Disbursement Batch Released"];
}
