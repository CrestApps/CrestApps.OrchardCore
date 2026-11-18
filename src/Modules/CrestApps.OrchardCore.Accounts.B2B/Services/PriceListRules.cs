using System.Globalization;
using CrestApps.OrchardCore.Accounts.B2B.Models;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// The rules of a price list, shared by the Price lists admin pages and the AI tools: a name, a percentage from 0 to 100,
/// prices by SKU and at most <see cref="MaxEntries"/> of them; and deleting a list puts its accounts back on the store's
/// prices. Each caller words the problems itself.
/// </summary>
internal static class PriceListRules
{
    /// <summary>
    /// The most prices by SKU a list holds.
    /// </summary>
    public const int MaxEntries = 5000;

    /// <summary>
    /// The longest SKU a price list keeps.
    /// </summary>
    public const int MaxSkuLength = 100;

    /// <summary>
    /// Adds a price by SKU, replacing an earlier price for the same SKU, ignoring case.
    /// </summary>
    /// <param name="entries">The prices so far.</param>
    /// <param name="sku">The SKU, already trimmed.</param>
    /// <param name="unitPrice">The price of one unit.</param>
    /// <returns><see langword="false"/> when the SKU is blank or too long, or the price is negative.</returns>
    public static bool TryAddEntry(List<PriceListEntry> entries, string sku, decimal unitPrice)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (string.IsNullOrEmpty(sku) || sku.Length > MaxSkuLength || unitPrice < 0m)
        {
            return false;
        }

        entries.RemoveAll(entry => string.Equals(entry.Sku, sku, StringComparison.OrdinalIgnoreCase));
        entries.Add(new PriceListEntry { Sku = sku, UnitPrice = unitPrice });

        return true;
    }

    /// <summary>
    /// Checks a price list's name, percentage off and number of prices.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="percentOff">The percentage off, as text with a decimal point, or blank for none.</param>
    /// <param name="entryCount">How many prices by SKU it has.</param>
    /// <param name="value">The percentage off to keep, or none for blank or zero.</param>
    /// <returns>The problems, or an empty list.</returns>
    public static IReadOnlyList<PriceListProblem> Check(string name, string percentOff, int entryCount, out decimal? value)
    {
        var problems = new List<PriceListProblem>();

        value = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            problems.Add(PriceListProblem.NameRequired);
        }

        if (!string.IsNullOrWhiteSpace(percentOff))
        {
            if (!decimal.TryParse(percentOff, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent) || percent is < 0m or > 100m)
            {
                problems.Add(PriceListProblem.PercentOffOutOfRange);
            }
            else if (percent > 0m)
            {
                value = percent;
            }
        }

        if (entryCount > MaxEntries)
        {
            problems.Add(PriceListProblem.TooManyEntries);
        }

        return problems;
    }

    /// <summary>
    /// Copies the checked values onto a price list. Its prices are in the store's currency.
    /// </summary>
    /// <param name="list">The price list.</param>
    /// <param name="name">The name.</param>
    /// <param name="percentOff">The percentage off, or none.</param>
    /// <param name="entries">The prices by SKU.</param>
    /// <param name="storeCurrency">The store's currency.</param>
    public static void Apply(PriceList list, string name, decimal? percentOff, List<PriceListEntry> entries, string storeCurrency)
    {
        ArgumentNullException.ThrowIfNull(list);

        list.Name = name.Trim();
        list.PercentOff = percentOff;
        list.Entries = entries ?? [];
        list.Currency = storeCurrency;
    }

    /// <summary>
    /// Deletes a price list. The accounts on it go back to the store's prices.
    /// </summary>
    /// <param name="priceLists">The price list store.</param>
    /// <param name="accounts">The account store.</param>
    /// <param name="list">The price list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accounts that were on it.</returns>
    public static async Task<IReadOnlyList<B2BAccount>> DeleteAsync(IPriceListStore priceLists, IB2BAccountStore accounts, PriceList list, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(priceLists);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(list);

        var onList = await accounts.ListByPriceListAsync(list.ItemId, cancellationToken);

        foreach (var account in onList)
        {
            account.Part.PriceListId = null;
            await accounts.SaveAsync(account);
        }

        await priceLists.DeleteAsync(list, cancellationToken);

        return onList;
    }
}

/// <summary>
/// What can be wrong with a price list.
/// </summary>
internal enum PriceListProblem
{
    NameRequired,
    PercentOffOutOfRange,
    TooManyEntries,
}
