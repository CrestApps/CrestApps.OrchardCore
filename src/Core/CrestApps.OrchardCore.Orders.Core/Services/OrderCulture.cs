using CrestApps.OrchardCore.Commerce.Emails;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// The language an order's emails are written in: the one the buyer shopped in.
/// </summary>
public static class OrderCulture
{
    /// <summary>
    /// Returns the culture the current request is in, to keep on an order, or <see langword="null"/>.
    /// </summary>
    public static string Current()
        => CommerceEmailCulture.Current();

    /// <summary>
    /// Writes in a culture until the result is disposed. Returns <see langword="null"/>, keeping the current culture,
    /// when there's none or it isn't one.
    /// </summary>
    public static IDisposable Use(string culture)
        => CommerceEmailCulture.Use(culture);
}
