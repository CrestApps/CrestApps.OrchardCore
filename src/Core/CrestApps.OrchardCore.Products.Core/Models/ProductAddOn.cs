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
    /// Gets or sets the extra price per unit when the buyer chooses it, or zero.
    /// </summary>
    public decimal Price { get; set; }

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
/// An add-on a buyer chose, as it reads on a line: its label, the text typed (or <see langword="null"/> for a ticked
/// box) and the extra price per unit.
/// </summary>
public sealed record ProductAddOnChoice(string AddOnId, string Label, string Value, decimal Price);
