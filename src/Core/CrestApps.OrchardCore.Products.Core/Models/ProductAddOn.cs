namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// A personal touch a buyer can add to a product, such as an engraving (a text) or gift wrap (a tick box), at an
/// extra price per unit.
/// </summary>
public sealed class ProductAddOn
{
    /// <summary>
    /// The longest text a buyer can enter.
    /// </summary>
    public const int MaxTextLength = 200;

    /// <summary>
    /// Gets or sets the add-on's stable identifier, which carts and orders keep.
    /// </summary>
    public string AddOnId { get; set; }

    /// <summary>
    /// Gets or sets what the buyer sees, such as "Engraving".
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets whether the buyer types a text or ticks a box.
    /// </summary>
    public ProductAddOnKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the extra price per unit when the buyer chooses it, or zero. With
    /// <see cref="ProductAddOnPriceBasis.PerPerson"/> it is per person a unit is for, and with
    /// <see cref="ProductAddOnPriceBasis.PercentOfPrice"/> it is a percent of the unit's price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets what <see cref="Price"/> is charged on.
    /// </summary>
    public ProductAddOnPriceBasis PriceBasis { get; set; }

    /// <summary>
    /// Gets or sets whether the buyer has to fill it in, such as the name to print on a mug.
    /// </summary>
    public bool Required { get; set; }
}

/// <summary>
/// How a buyer chooses an add-on.
/// </summary>
public enum ProductAddOnKind
{
    Text,
    Checkbox,
}

/// <summary>
/// What an add-on's price is charged on.
/// </summary>
public enum ProductAddOnPriceBasis
{
    /// <summary>
    /// Its price on every unit.
    /// </summary>
    PerUnit,

    /// <summary>
    /// Its price for every person a unit is for, such as travel insurance on a double room.
    /// </summary>
    PerPerson,

    /// <summary>
    /// A percent of the unit's price.
    /// </summary>
    PercentOfPrice,
}

/// <summary>
/// An add-on a buyer chose, as it reads on a line: its label, the text typed (or <see langword="null"/> for a ticked
/// box) and the extra price per unit.
/// </summary>
public sealed record ProductAddOnChoice(string AddOnId, string Label, string Value, decimal Price);
