using System.Collections.Generic;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents anything that can be taxed. Implementations are provided by taxable-item providers so
/// that products, subscriptions, bookings, services, content items, and custom objects can all
/// participate in taxation through a single abstraction.
/// </summary>
public interface ITaxableItem
{
    /// <summary>
    /// Gets the stable identifier of the taxable item within the transaction.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the kind of taxable item.
    /// </summary>
    TaxableItemKind Kind { get; }

    /// <summary>
    /// Gets the quantity being taxed.
    /// </summary>
    decimal Quantity { get; }

    /// <summary>
    /// Gets the price of a single unit, before any discount.
    /// </summary>
    decimal UnitPrice { get; }

    /// <summary>
    /// Gets the discount applied to the line, expressed in the same currency as <see cref="UnitPrice"/>.
    /// </summary>
    decimal DiscountAmount { get; }

    /// <summary>
    /// Gets the currency code the amounts are expressed in.
    /// </summary>
    string Currency { get; }

    /// <summary>
    /// Gets the tax category code (for example <c>Electronics</c>) used to classify the item.
    /// </summary>
    string TaxCategoryCode { get; }

    /// <summary>
    /// Gets the tax classification code (for example <c>Television</c>) that refines the category.
    /// </summary>
    string TaxClassificationCode { get; }

    /// <summary>
    /// Gets an optional external or provider-specific tax code.
    /// </summary>
    string ExternalTaxCode { get; }

    /// <summary>
    /// Gets a value indicating whether the amounts already include tax.
    /// </summary>
    bool? PriceIncludesTax { get; }

    /// <summary>
    /// Gets the total weight of the line, used by per-weight calculation methods.
    /// </summary>
    decimal? Weight { get; }

    /// <summary>
    /// Gets the total volume of the line, used by per-volume calculation methods.
    /// </summary>
    decimal? Volume { get; }

    /// <summary>
    /// Gets the origin (ship-from) address of the item, when it differs from the transaction origin.
    /// </summary>
    Address Origin { get; }

    /// <summary>
    /// Gets optional metadata that providers can attach for rule evaluation.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}
