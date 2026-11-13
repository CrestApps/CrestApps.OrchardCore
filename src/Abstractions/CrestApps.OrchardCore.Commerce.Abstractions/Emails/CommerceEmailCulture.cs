using CrestApps.OrchardCore.Emails;

namespace CrestApps.OrchardCore.Commerce.Emails;

/// <summary>
/// The language a store's email is written in, such as the one the buyer shopped in. The same as the site's
/// <see cref="TemplatedEmailCulture"/>.
/// </summary>
public static class CommerceEmailCulture
{
    /// <summary>
    /// Returns the culture the current request is in, to keep on a record, or <see langword="null"/>.
    /// </summary>
    public static string Current()
        => TemplatedEmailCulture.Current();

    /// <summary>
    /// Writes in a culture until the result is disposed. Returns <see langword="null"/>, keeping the current culture,
    /// when there's none or it isn't one.
    /// </summary>
    /// <param name="culture">The culture's name, such as <c>fr-FR</c>.</param>
    public static IDisposable Use(string culture)
        => TemplatedEmailCulture.Use(culture);
}
