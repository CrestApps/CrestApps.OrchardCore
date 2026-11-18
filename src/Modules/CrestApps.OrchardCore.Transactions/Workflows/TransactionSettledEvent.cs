using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Starts when nothing more is owed on a transaction.
/// </summary>
public sealed class TransactionSettledEvent : TransactionEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSettledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionSettledEvent(IStringLocalizer<TransactionSettledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(TransactionSettledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Transaction Settled"];
}
