using System.Globalization;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Exports a person's store credit (each balance with its activity) and, on erasure, scrubs the activity reasons, which
/// carry the titles of the transactions credit was kept or spent for, and the actors. Amounts and balances stay.
/// </summary>
public sealed class StoreCreditPersonalDataContributor : ICommercePersonalDataContributor
{
    /// <summary>
    /// The kind of the exported records.
    /// </summary>
    public const string RecordKind = "StoreCredit";

    private readonly ICustomerStore _customers;
    private readonly IStoreCreditStore _store;
    private readonly IBalanceLedgerArchive _archive;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreCreditPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="customers">The customer records, to find a person's.</param>
    /// <param name="store">The store credit accounts.</param>
    /// <param name="archive">The older changes moved out of the accounts.</param>
    public StoreCreditPersonalDataContributor(ICustomerStore customers, IStoreCreditStore store, IBalanceLedgerArchive archive)
    {
        _customers = customers;
        _store = store;
        _archive = archive;
    }

    /// <inheritdoc/>
    public bool TakesPartInRetentionExpiry => false;

    /// <inheritdoc/>
    public Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var account in await FindAsync(context.Subject, cancellationToken))
        {
            var record = new CommercePersonalDataRecord
            {
                Kind = RecordKind,
                Id = account.ItemId,
                CreatedUtc = account.CreatedUtc,
                Values =
                {
                    [nameof(StoreCreditAccount.Currency)] = account.Currency,
                    [nameof(StoreCreditAccount.Balance)] = account.Balance.ToString(CultureInfo.InvariantCulture),
                },
            };

            var index = 0;

            // The moved-out older changes first, then the ones the account keeps: the whole history, oldest first.
            foreach (var archived in await _archive.ListAsync(StoreCreditStore.Ledger, account.ItemId, cancellationToken))
            {
                index++;
                record.Values[$"Activity{index}"] = Describe(archived.CreatedUtc, archived.Amount, archived.Reason);
            }

            foreach (var entry in account.Activity ?? [])
            {
                index++;
                record.Values[$"Activity{index}"] = Describe(entry.CreatedUtc, entry.Amount, entry.Reason);
            }

            context.Export.Records.Add(record);
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var account in await FindAsync(context.Subject, cancellationToken))
        {
            foreach (var entry in account.Activity ?? [])
            {
                entry.Reason = context.Scrub(entry.Reason);
                entry.Actor = context.ReplaceName(entry.Actor);
            }

            foreach (var archived in await _archive.ListAsync(StoreCreditStore.Ledger, account.ItemId, cancellationToken))
            {
                archived.Reason = context.Scrub(archived.Reason);
                archived.Actor = context.ReplaceName(archived.Actor);
                await _archive.SaveAsync(archived);
            }

            await _store.UpdateAsync(account, cancellationToken);
            context.Result.Count(RecordKind);
        }
    }

    private async Task<IReadOnlyList<StoreCreditAccount>> FindAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, StoreCreditAccount>(StringComparer.Ordinal);

        foreach (var userId in subject.UserIds ?? [])
        {
            if (await _customers.FindByUserIdAsync(userId, cancellationToken) is not { } customer)
            {
                continue;
            }

            foreach (var account in await _store.ListByCustomerAsync(customer.ItemId, cancellationToken))
            {
                found[account.ItemId] = account;
            }
        }

        return [.. found.Values];
    }

    private static string Describe(DateTime createdUtc, decimal amount, string reason)
        => string.Join(" | ", createdUtc.ToString("O", CultureInfo.InvariantCulture), amount.ToString(CultureInfo.InvariantCulture), reason);
}
