using System.Globalization;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Words a sentence the platform speaks to a caller in the language it will be spoken in.
/// </summary>
/// <remarks>
/// A spoken prompt is built from a webhook or a background pass, where the current culture belongs to nobody in
/// particular, and it is read out by a text-to-speech voice set up for one language. Looking the sentence up in the
/// voice's language keeps a translated tenant from hearing English read in, say, a Spanish voice.
/// </remarks>
public static class SpokenPromptCulture
{
    /// <summary>
    /// Builds a sentence with the current culture set to the given language.
    /// </summary>
    /// <param name="language">The language the sentence will be spoken in, such as <c>es-ES</c>; empty or unknown
    /// leaves the current culture as it is.</param>
    /// <param name="build">Builds the sentence, typically through a string localizer.</param>
    /// <returns>The sentence.</returns>
    public static string Localize(string language, Func<string> build)
    {
        ArgumentNullException.ThrowIfNull(build);

        var culture = Find(language);

        if (culture is null ||
            (culture.Equals(CultureInfo.CurrentUICulture) && culture.Equals(CultureInfo.CurrentCulture)))
        {
            return build();
        }

        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            return build();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    private static CultureInfo Find(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(language.Trim());
        }
        catch (CultureNotFoundException)
        {
            // A voice language the runtime does not know as a culture: the sentence is worded as it would be anyway.
            return null;
        }
    }
}
