using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Starts when a payment recorded by hand or an adjustment is taken back with a reversing entry, so it is owed again.
/// </summary>
public sealed class TransactionEntryReversedEvent : TransactionEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionEntryReversedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionEntryReversedEvent(IStringLocalizer<TransactionEntryReversedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(TransactionEntryReversedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Transaction Entry Taken Back"];
}
