using CrestApps.Core.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// How a party is collected from, kept once for the party: whether it is sent payment reminders and charged late fees.
/// It shows in the party's billing profile and is read when the reminder and late-fee sweeps run.
/// </summary>
public sealed class PartyCollectionSettings : CatalogItem
{
    public PartyRef Party { get; set; }

    /// <summary>
    /// Gets or sets whether the party is sent no payment reminders, for any debt it owes or pays.
    /// </summary>
    public bool RemindersPaused { get; set; }

    /// <summary>
    /// Gets or sets whether the party is charged no late fees, on any invoice billed to it.
    /// </summary>
    public bool LateFeesExempt { get; set; }

    public DateTime ModifiedUtc { get; set; }
}
