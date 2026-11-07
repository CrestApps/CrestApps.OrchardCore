using System.Globalization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Commerce.Emails;

/// <summary>
/// The language a store's email is written in, such as the one the buyer shopped in.
/// </summary>
public static class CommerceEmailCulture
{
    /// <summary>
    /// Returns the culture the current request is in, to keep on a record, or <see langword="null"/>.
    /// </summary>
    public static string Current()
        => string.IsNullOrEmpty(CultureInfo.CurrentUICulture.Name) ? null : CultureInfo.CurrentUICulture.Name;

    /// <summary>
    /// Writes in a culture until the result is disposed. Returns <see langword="null"/>, keeping the current culture,
    /// when there's none or it isn't one.
    /// </summary>
    /// <param name="culture">The culture's name, such as <c>fr-FR</c>.</param>
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
