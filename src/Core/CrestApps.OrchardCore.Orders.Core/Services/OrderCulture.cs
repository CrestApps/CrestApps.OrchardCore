using System.Globalization;
using OrchardCore.Localization;

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
        => string.IsNullOrEmpty(CultureInfo.CurrentUICulture.Name) ? null : CultureInfo.CurrentUICulture.Name;

    /// <summary>
    /// Writes in a culture until the result is disposed. Returns <see langword="null"/>, keeping the current culture,
    /// when there's none or it isn't one.
    /// </summary>
    public static IDisposable Use(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        try
        {
            var info = CultureInfo.GetCultureInfo(culture);

            return CultureScope.Create(info, info);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
