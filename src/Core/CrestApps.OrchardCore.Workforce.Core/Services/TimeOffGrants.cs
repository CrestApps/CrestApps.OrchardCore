using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Works out what is left of each grant from a ledger. Every addition is a lot; time taken, payouts and other
/// reductions use the lots that can still be used on their day, the grant that expires soonest first, then the rest,
/// oldest first. Given-back time goes to the grants it can still be used under, then becomes a lot of its own.
/// </summary>
internal static class TimeOffGrants
{
    /// <summary>
    /// Allocates a ledger, in the order its entries count.
    /// </summary>
    public static List<Lot> Allocate(IEnumerable<TimeOffLedgerEntry> entries)
    {
        var lots = new List<Lot>();

        foreach (var entry in entries.OrderBy(entry => entry.EffectiveDate).ThenBy(entry => entry.CreatedUtc))
        {
            var date = entry.EffectiveDate;
            var usable = lots.Where(lot => lot.ExpiresOn is not { } last || last >= date)
                .OrderBy(lot => lot.ExpiresOn ?? DateOnly.MaxValue)
                .ThenBy(lot => lot.Entry.EffectiveDate)
                .ToList();

            // A grant's own expiry takes what is left of it.
            if (entry.Kind == TimeOffLedgerKind.Expiry && !string.IsNullOrEmpty(entry.SourceRef) && lots.FirstOrDefault(lot => lot.Entry.ItemId == entry.SourceRef) is { } expired)
            {
                expired.Left = Math.Max(0, expired.Left + entry.Amount);

                continue;
            }

            if (entry.Amount > 0 && entry.Kind == TimeOffLedgerKind.Reversal)
            {
                var back = entry.Amount;

                foreach (var lot in usable.Where(lot => lot.Used > 0))
                {
                    var amount = Math.Min(back, lot.Used);

                    lot.Used -= amount;
                    lot.Left += amount;
                    back -= amount;

                    if (back == 0)
                    {
                        break;
                    }
                }

                if (back > 0)
                {
                    lots.Add(new Lot(entry, back, null));
                }

                continue;
            }

            if (entry.Amount > 0)
            {
                lots.Add(new Lot(entry, entry.Amount, entry.Kind == TimeOffLedgerKind.Grant ? entry.ExpiresOn : null));

                continue;
            }

            var need = -entry.Amount;

            foreach (var lot in usable.Where(lot => lot.Left > 0))
            {
                var amount = Math.Min(need, lot.Left);

                lot.Left -= amount;
                lot.Used += amount;
                need -= amount;

                if (need == 0)
                {
                    break;
                }
            }
        }

        return lots;
    }

    /// <summary>
    /// A share of the balance and what is left of it.
    /// </summary>
    internal sealed class Lot
    {
        public Lot(TimeOffLedgerEntry entry, decimal left, DateOnly? expiresOn)
        {
            Entry = entry;
            Left = left;
            ExpiresOn = expiresOn;
        }

        public TimeOffLedgerEntry Entry { get; }

        public DateOnly? ExpiresOn { get; }

        public decimal Left { get; set; }

        public decimal Used { get; set; }

        public bool IsGrant => ExpiresOn is not null;
    }
}
