using System;
using System.Collections.Generic;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Carries everything required to deterministically calculate tax for a transaction.
/// </summary>
public sealed class TaxCalculationContext
{
    /// <summary>
    /// Gets or sets the taxable items participating in the transaction.
    /// </summary>
    public IList<ITaxableItem> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the customer tax profile, when a customer is known.
    /// </summary>
    public CustomerTaxProfile Customer { get; set; }

    /// <summary>
    /// Gets or sets the origin (ship-from / merchant) address.
    /// </summary>
    public Address Origin { get; set; }

    /// <summary>
    /// Gets or sets the destination (ship-to) address.
    /// </summary>
    public Address Destination { get; set; }

    /// <summary>
    /// Gets or sets the location where a service is performed.
    /// </summary>
    public Address ServiceLocation { get; set; }

    /// <summary>
    /// Gets or sets the location where an event takes place.
    /// </summary>
    public Address EventLocation { get; set; }

    /// <summary>
    /// Gets or sets the transaction date, in UTC, used to select effective rules and tables.
    /// </summary>
    public DateTime TransactionDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the currency the transaction is expressed in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether amounts, by default, already include tax.
    /// Individual items may override this through <see cref="ITaxableItem.PriceIncludesTax"/>.
    /// </summary>
    public TaxPriceType DefaultPriceType { get; set; } = TaxPriceType.Exclusive;

    /// <summary>
    /// Gets or sets an optional override for the rounding level of the calculation.
    /// </summary>
    public TaxRoundingLevel? RoundingLevel { get; set; }

    /// <summary>
    /// Gets or sets optional metadata for the transaction that rules may evaluate.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
