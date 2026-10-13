using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// The styles a listing filter can be drawn in. The listing editor offers each registered style for the filter
/// kinds it supports, and a filter drawn in a style gets the <c>ProductFacet__{Style}</c> alternate, so a module
/// or theme adds a style by registering it here and providing a <c>ProductFacet-{Style}</c> template.
/// </summary>
/// <example>
/// <code>
/// services.Configure&lt;ProductFacetStyleOptions&gt;(options =&gt;
///     options.AddStyle("Stars", style =&gt;
///     {
///         style.DisplayName = S["Star rating"];
///         style.Kinds.Add(ProductFacetKind.Checkbox);
///         style.Kinds.Add(ProductFacetKind.Radio);
///     }));
/// </code>
/// </example>
public sealed class ProductFacetStyleOptions
{
    private readonly List<ProductFacetStyle> _styles = [];

    /// <summary>
    /// Gets the registered styles, in the order they were added.
    /// </summary>
    public IReadOnlyList<ProductFacetStyle> Styles
        => _styles;

    /// <summary>
    /// Adds a style, or updates the one already registered under the same name.
    /// </summary>
    /// <param name="name">The style's name, stored on the filter and used in its alternate.</param>
    /// <param name="configure">An optional configuration action.</param>
    public void AddStyle(string name, Action<ProductFacetStyle> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var style = Find(name);

        if (style is null)
        {
            style = new ProductFacetStyle(name);
            _styles.Add(style);
        }

        configure?.Invoke(style);

        style.DisplayName ??= new LocalizedString(name, name);
    }

    /// <summary>
    /// Gets the style registered under a name, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The style's name.</param>
    public ProductFacetStyle Find(string name)
        => string.IsNullOrEmpty(name)
            ? null
            : _styles.FirstOrDefault(style => string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// One way of drawing a listing filter.
/// </summary>
public sealed class ProductFacetStyle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductFacetStyle"/> class.
    /// </summary>
    /// <param name="name">The style's name.</param>
    public ProductFacetStyle(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
    }

    /// <summary>
    /// Gets the style's name, stored on the filter and used in its <c>ProductFacet__{Style}</c> alternate.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the name the listing editor shows.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets the filter kinds the style can draw. It can draw every kind when empty.
    /// </summary>
    public ISet<ProductFacetKind> Kinds { get; } = new HashSet<ProductFacetKind>();

    /// <summary>
    /// Gets whether the style can draw a filter of the given kind.
    /// </summary>
    /// <param name="kind">The filter's kind.</param>
    public bool Supports(ProductFacetKind kind)
        => Kinds.Count == 0 || Kinds.Contains(kind);
}
