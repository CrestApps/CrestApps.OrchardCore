using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Starts when a payment is applied to a transaction, online or recorded by staff.
/// </summary>
public sealed class TransactionPaymentRecordedEvent : TransactionEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPaymentRecordedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionPaymentRecordedEvent(IStringLocalizer<TransactionPaymentRecordedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(TransactionPaymentRecordedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Transaction Payment Recorded"];
}
