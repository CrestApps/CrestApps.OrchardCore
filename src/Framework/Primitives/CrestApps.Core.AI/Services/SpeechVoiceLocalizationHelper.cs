using System.Globalization;

namespace CrestApps.Core.AI.Services;

public static class SpeechVoiceLocalizationHelper
{
    public static HashSet<string> CreateAllowedCultures(
        IEnumerable<string> supportedCultures,
        CultureInfo currentCulture = null,
        CultureInfo currentUICulture = null)
    {
        var allowedCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in supportedCultures ?? [])
        {
            AddCultureHierarchy(allowedCultures, culture);
        }

        AddCultureHierarchy(allowedCultures, currentCulture?.Name);
        AddCultureHierarchy(allowedCultures, currentUICulture?.Name);

        return allowedCultures;
    }

    public static bool IsLanguageAllowed(string language, ISet<string> allowedCultures)
    {
        if (string.IsNullOrWhiteSpace(language) || allowedCultures is null || allowedCultures.Count == 0)
        {
            return true;
        }

        foreach (var allowedCulture in allowedCultures)
        {
            if (string.Equals(language, allowedCulture, StringComparison.OrdinalIgnoreCase) ||
                language.StartsWith(allowedCulture + '-', StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string GetCultureDisplayName(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(language).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return language;
        }
    }

    private static void AddCultureHierarchy(HashSet<string> allowedCultures, string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);

            while (!string.IsNullOrWhiteSpace(culture.Name))
            {
                allowedCultures.Add(culture.Name);
                culture = culture.Parent;
            }
        }
        catch (CultureNotFoundException)
        {
            allowedCultures.Add(cultureName);
        }
    }
}
