using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Starts when a transaction is canceled.
/// </summary>
public sealed class TransactionCanceledEvent : TransactionEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCanceledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionCanceledEvent(IStringLocalizer<TransactionCanceledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(TransactionCanceledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Transaction Canceled"];
}
