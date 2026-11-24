using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Keeps whether a party is sent payment reminders and charged late fees.
/// </summary>
public interface IPartyCollectionSettingsService
{
    /// <summary>
    /// Gets a party's settings, or <see langword="null"/> when nothing is kept for it (reminders and fees as usual).
    /// </summary>
    Task<PartyCollectionSettings> GetAsync(PartyRef party, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a party's settings.
    /// </summary>
    Task SaveAsync(PartyRef party, bool remindersPaused, bool lateFeesExempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the parties kept out of reminders or late fees, for a sweep to check each debt or invoice against.
    /// </summary>
    Task<PartyCollectionExclusions> GetExclusionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The parties kept out of payment reminders and late fees, as a sweep reads them once.
/// </summary>
public sealed class PartyCollectionExclusions
{
    private readonly HashSet<PartyRef> _remindersPaused;
    private readonly HashSet<PartyRef> _lateFeesExempt;

    public PartyCollectionExclusions(IEnumerable<PartyCollectionSettings> settings)
    {
        var list = (settings ?? []).Where(item => item is not null && !item.Party.IsEmpty).ToList();

        _remindersPaused = [.. list.Where(item => item.RemindersPaused).Select(item => item.Party)];
        _lateFeesExempt = [.. list.Where(item => item.LateFeesExempt).Select(item => item.Party)];
    }

    /// <summary>
    /// Whether a debt's reminders are paused for the party that pays it or the user who owns it.
    /// </summary>
    public bool AreRemindersPaused(Transaction transaction)
    {
        if (transaction is null || _remindersPaused.Count == 0)
        {
            return false;
        }

        var payer = transaction.GetPayer();

        return !payer.IsEmpty && _remindersPaused.Contains(payer) ||
            !string.IsNullOrEmpty(transaction.OwnerId) && _remindersPaused.Contains(new PartyRef(PartyKinds.User, transaction.OwnerId));
    }

    /// <summary>
    /// Whether a party is charged no late fees.
    /// </summary>
    public bool IsLateFeeExempt(PartyRef party)
        => !party.IsEmpty && _lateFeesExempt.Contains(party);
}
