using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Starts when a transaction is recorded.
/// </summary>
public sealed class TransactionCreatedEvent : TransactionEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCreatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionCreatedEvent(IStringLocalizer<TransactionCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(TransactionCreatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Transaction Created"];
}
