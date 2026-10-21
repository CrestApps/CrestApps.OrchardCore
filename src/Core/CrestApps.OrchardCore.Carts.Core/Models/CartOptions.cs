namespace CrestApps.OrchardCore.Carts.Core.Models;

/// <summary>
/// Limits and lifetimes of carts.
/// </summary>
public sealed class CartOptions
{
    /// <summary>
    /// Gets or sets how long a guest's cart is kept after it was last changed.
    /// </summary>
    public TimeSpan GuestCartLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets how long a signed-in user's cart is kept after it was last changed.
    /// </summary>
    public TimeSpan UserCartLifetime { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Gets or sets the most lines a cart may hold.
    /// </summary>
    public int MaxLines { get; set; } = 50;

    /// <summary>
    /// Gets or sets the most units of one line, when the product's price sets no lower limit.
    /// </summary>
    public int MaxQuantity { get; set; } = 99;

    /// <summary>
    /// Gets the content parts whose products are bought through their own flow, never through a cart. A
    /// subscription plan is signed up for on its own: its first payment starts an agreement that bills again.
    /// </summary>
    public ISet<string> ExcludedParts { get; } = new HashSet<string>(StringComparer.Ordinal) { "SubscriptionPart" };

    /// <summary>
    /// Gets the content parts whose products need something chosen on their own page before they're added, such as a
    /// booking's date: a product card links to the page instead of adding it.
    /// </summary>
    public ISet<string> ChooseOnProductPageParts { get; } = new HashSet<string>(StringComparer.Ordinal);
}
