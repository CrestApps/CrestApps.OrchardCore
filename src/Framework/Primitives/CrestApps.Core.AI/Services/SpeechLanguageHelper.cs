using System.Globalization;

namespace CrestApps.Core.AI.Services;

public static class SpeechLanguageHelper
{
    public static string NormalizeOrDefault(string language, string fallbackLanguage = "en-US")
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return fallbackLanguage;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(language);

            return culture.IsNeutralCulture
                ? CultureInfo.CreateSpecificCulture(culture.Name).Name
                : culture.Name;
        }
        catch (CultureNotFoundException)
        {
            return fallbackLanguage;
        }
    }
}
