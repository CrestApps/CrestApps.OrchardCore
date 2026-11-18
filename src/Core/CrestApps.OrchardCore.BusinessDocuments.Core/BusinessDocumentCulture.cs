using System.Globalization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.BusinessDocuments.Core;

/// <summary>
/// Renders a business document in the culture it names.
/// </summary>
public static class BusinessDocumentCulture
{
    /// <summary>
    /// Switches the current culture and UI culture to a document's culture until the result is disposed.
    /// </summary>
    /// <param name="culture">The name of the culture, such as <c>fr-FR</c>.</param>
    /// <returns>The scope to dispose, or <see langword="null"/>, keeping the current culture, when the name is empty or
    /// is not a culture.</returns>
    public static IDisposable Use(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        try
        {
            var info = CultureInfo.GetCultureInfo(culture.Trim());

            return CultureScope.Create(info, info);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
