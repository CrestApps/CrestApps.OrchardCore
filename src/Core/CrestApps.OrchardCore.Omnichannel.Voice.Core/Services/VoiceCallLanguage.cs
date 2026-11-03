using System.Globalization;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The language a call's written record is kept in: the language of the AI persona the call's profile speaks as.
/// </summary>
/// <remarks>
/// The call's review and the briefing for the agent it is handed to are asked for in English, and an English prompt
/// is answered in English whatever language the call was held in: live, a call held in Greek was summarized in
/// English for an office that works in Greek. Named in the prompt, the language holds.
/// </remarks>
internal static class VoiceCallLanguage
{
    /// <summary>
    /// The English name of the language the profile's persona speaks (<c>Greek</c> for <c>el-GR</c>).
    /// </summary>
    /// <returns>
    /// The name, or <see langword="null"/> when the profile has no persona, its persona names no language, or the AI
    /// Personas feature is off.
    /// </returns>
    /// <param name="services">The tenant's services.</param>
    /// <param name="profile">The AI profile the call spoke for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<string> GetNameAsync(IServiceProvider services, AIProfile profile, CancellationToken cancellationToken = default)
    {
        if (services is null || profile is null)
        {
            return null;
        }

        var persona = await services.ResolvePersonaAsync(profile, cancellationToken);

        return GetName(persona?.Language);
    }

    /// <summary>
    /// The English name of a BCP-47 language (<c>Greek</c> for <c>el-GR</c>), the tag itself when .NET does not know
    /// it, or <see langword="null"/> for none.
    /// </summary>
    /// <param name="language">The language tag.</param>
    internal static string GetName(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        language = language.Trim();

        try
        {
            var culture = CultureInfo.GetCultureInfo(language);
            var neutral = culture.IsNeutralCulture ? culture : culture.Parent;
            var name = string.IsNullOrWhiteSpace(neutral?.EnglishName) ? culture.EnglishName : neutral.EnglishName;

            return string.IsNullOrWhiteSpace(name) ? language : name;
        }
        catch (CultureNotFoundException)
        {
            return language;
        }
    }
}
