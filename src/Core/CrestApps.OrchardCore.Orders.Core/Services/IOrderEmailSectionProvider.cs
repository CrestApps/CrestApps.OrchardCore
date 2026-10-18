using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Adds a section to an order's confirmation email, such as the links to download what was bought.
/// </summary>
public interface IOrderEmailSectionProvider
{
    /// <summary>
    /// Returns the section for the order, or <see langword="null"/> when it has nothing to add.
    /// </summary>
    Task<OrderEmailSection> GetSectionAsync(Order order, CancellationToken cancellationToken = default);
}

/// <summary>
/// A titled list of links in an order email.
/// </summary>
public sealed class OrderEmailSection
{
    public string Title { get; set; }

    public string Intro { get; set; }

    public IList<OrderEmailLink> Links { get; set; } = [];
}

/// <summary>
/// A link in an <see cref="OrderEmailSection"/>, with a note after it, such as how long it works.
/// </summary>
public sealed record OrderEmailLink(string Text, string Url, string Note = null);
