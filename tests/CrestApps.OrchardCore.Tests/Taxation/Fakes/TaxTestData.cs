using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// Helper factory methods that seed common taxation test fixtures.
/// </summary>
public static class TaxTestData
{
    /// <summary>
    /// The fixed transaction date used by most taxation tests.
    /// </summary>
    public static readonly DateTime TransactionDate = new(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Creates a destination address in California, United States.
    /// </summary>
    /// <returns>The address.</returns>
    public static Address California()
        => new() { Country = "US", Region = "CA", City = "Los Angeles", PostalCode = "90001" };

    /// <summary>
    /// Creates and stores a state-level jurisdiction, returning its generated identifier.
    /// </summary>
    /// <param name="harness">The test harness.</param>
    /// <param name="name">The jurisdiction name.</param>
    /// <param name="country">The country component.</param>
    /// <param name="region">The region component.</param>
    /// <returns>The generated jurisdiction identifier.</returns>
    public static async Task<string> AddJurisdictionAsync(
        TaxTestHarness harness,
        string name,
        string country,
        string region)
    {
        var jurisdiction = new TaxJurisdiction
        {
            Name = name,
            Code = name,
            Level = JurisdictionLevel.State,
            Country = country,
            Region = region,
        };

        await harness.Jurisdictions.CreateAsync(jurisdiction);

        return jurisdiction.ItemId;
    }

    /// <summary>
    /// Creates and stores a tax rule, returning its generated identifier.
    /// </summary>
    /// <param name="harness">The test harness.</param>
    /// <param name="rule">The rule to store.</param>
    /// <returns>The generated rule identifier.</returns>
    public static async Task<string> AddRuleAsync(TaxTestHarness harness, TaxRule rule)
    {
        await harness.Rules.CreateAsync(rule);

        return rule.ItemId;
    }

    /// <summary>
    /// Builds a single-item calculation context with a physical taxable item.
    /// </summary>
    /// <param name="unitPrice">The unit price.</param>
    /// <param name="quantity">The quantity.</param>
    /// <param name="destination">The destination address.</param>
    /// <param name="customer">The optional customer profile.</param>
    /// <param name="categoryCode">The optional tax category code.</param>
    /// <returns>The calculation context.</returns>
    public static TaxCalculationContext Context(
        decimal unitPrice,
        decimal quantity = 1m,
        Address destination = null,
        CustomerTaxProfile customer = null,
        string categoryCode = null)
    {
        return new TaxCalculationContext
        {
            Currency = "USD",
            TransactionDateUtc = TransactionDate,
            Destination = destination ?? California(),
            Customer = customer,
            Items =
            [
                new TaxableItem
                {
                    Id = "item-1",
                    Kind = TaxableItemKind.Physical,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    Currency = "USD",
                    TaxCategoryCode = categoryCode,
                },
            ],
        };
    }
}
