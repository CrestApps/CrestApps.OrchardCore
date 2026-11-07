using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// One line of a paid order to grant: the order, the line, and the purchased version of its product with the part that
/// makes it grant something.
/// </summary>
/// <typeparam name="TPart">The part that makes the product grant something, such as digital files.</typeparam>
public sealed class OrderLineGrantContext<TPart>
    where TPart : ContentPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderLineGrantContext{TPart}"/> class.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <param name="line">The line to grant.</param>
    /// <param name="contentItem">The purchased version of the line's product.</param>
    /// <param name="part">The part of the purchased version.</param>
    public OrderLineGrantContext(Order order, OrderLine line, ContentItem contentItem, TPart part)
    {
        Order = order;
        Line = line;
        ContentItem = contentItem;
        Part = part;
    }

    /// <summary>
    /// Gets the paid order.
    /// </summary>
    public Order Order { get; }

    /// <summary>
    /// Gets the line to grant.
    /// </summary>
    public OrderLine Line { get; }

    /// <summary>
    /// Gets the purchased version of the line's product.
    /// </summary>
    public ContentItem ContentItem { get; }

    /// <summary>
    /// Gets the part of the purchased version.
    /// </summary>
    public TPart Part { get; }
}
