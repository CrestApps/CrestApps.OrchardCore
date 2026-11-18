using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Starts the first time a transaction is found unpaid after its due date.
/// </summary>
public sealed class TransactionOverdueEvent : TransactionEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionOverdueEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionOverdueEvent(IStringLocalizer<TransactionOverdueEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(TransactionOverdueEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Transaction Overdue"];
}
