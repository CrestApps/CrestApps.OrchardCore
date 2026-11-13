using CrestApps.OrchardCore.Banking.Models;

namespace CrestApps.OrchardCore.Banking.Core.Services;

/// <summary>
/// Holds, for one request, the changes <see cref="BankAccountService"/> made to accounts that are not saved yet, so the
/// save can tell the observers what changed. The number in clear text is never kept here.
/// </summary>
public sealed class BankAccountChangeTracker
{
    private readonly Dictionary<BankAccount, PendingChange> _pending = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Records a change to an account.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="kind">What changed.</param>
    /// <param name="previousStatus">The status before a status change, or <see langword="null"/>.</param>
    /// <param name="reason">Why, or <see langword="null"/>.</param>
    public void Record(BankAccount account, BankAccountChangeKind kind, BankAccountStatus? previousStatus = null, string reason = null)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!_pending.TryGetValue(account, out var change))
        {
            change = new PendingChange();
            _pending[account] = change;
        }

        if (!change.Kinds.Contains(kind))
        {
            change.Kinds.Add(kind);
        }

        change.PreviousStatus ??= previousStatus;
        change.Reason = reason ?? change.Reason;
    }

    /// <summary>
    /// Returns the changes recorded for an account and forgets them.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The kinds of change, the status before the first status change, and the last reason given.</returns>
    public (IReadOnlyList<BankAccountChangeKind> Kinds, BankAccountStatus? PreviousStatus, string Reason) Take(BankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!_pending.Remove(account, out var change))
        {
            return ([], null, null);
        }

        return (change.Kinds, change.PreviousStatus, change.Reason);
    }

    private sealed class PendingChange
    {
        public List<BankAccountChangeKind> Kinds { get; } = [];

        public BankAccountStatus? PreviousStatus { get; set; }

        public string Reason { get; set; }
    }
}
