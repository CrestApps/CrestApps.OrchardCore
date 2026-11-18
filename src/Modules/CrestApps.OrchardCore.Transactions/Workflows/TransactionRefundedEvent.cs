using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Starts when money is given back on a transaction.
/// </summary>
public sealed class TransactionRefundedEvent : TransactionEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionRefundedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionRefundedEvent(IStringLocalizer<TransactionRefundedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(TransactionRefundedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Transaction Refunded"];
}
