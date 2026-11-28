using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Localization.Data;

namespace CrestApps.OrchardCore.AI.Agent.DataLocalization;

/// <summary>
/// What the dynamic translation tools share: the translatable strings every enabled feature declares, the cultures of
/// the site, and the translation a culture has for a string.
/// </summary>
internal static class DynamicTranslationToolSupport
{
    /// <summary>
    /// Finds a culture among the site's supported cultures, ignoring letter case, as the culture is stored under the
    /// name the site uses.
    /// </summary>
    /// <param name="supportedCultures">The supported cultures of the site.</param>
    /// <param name="culture">The culture the caller gave.</param>
    /// <returns>The name the site uses, or <see langword="null"/> when the culture is not supported.</returns>
    public static string FindCulture(IEnumerable<string> supportedCultures, string culture)
        => string.IsNullOrWhiteSpace(culture)
            ? null
            : supportedCultures.FirstOrDefault(name => string.Equals(name, culture.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the supported cultures of the site.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The culture names.</returns>
    public static async Task<string[]> GetSupportedCulturesAsync(IServiceProvider services)
        => await services.GetRequiredService<ILocalizationService>().GetSupportedCulturesAsync() ?? [];

    /// <summary>
    /// Gets the display name of a supported culture, as the Dynamic Translations page shows it.
    /// </summary>
    /// <param name="culture">The culture name.</param>
    /// <returns>The display name.</returns>
    public static string GetDisplayName(string culture)
    {
        var cultureInfo = CultureInfo.GetCultureInfo(culture);

        return !string.IsNullOrEmpty(cultureInfo.DisplayName)
            ? cultureInfo.DisplayName
            : cultureInfo.NativeName;
    }

    /// <summary>
    /// Gets the translatable strings every enabled feature declares, as the Dynamic Translations page lists them.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The strings, with their context and name.</returns>
    public static async Task<List<DataLocalizedString>> GetDescriptorsAsync(IServiceProvider services)
    {
        var descriptors = new List<DataLocalizedString>();

        foreach (var provider in services.GetServices<ILocalizationDataProvider>())
        {
            descriptors.AddRange(await provider.GetDescriptorsAsync());
        }

        return descriptors;
    }

    /// <summary>
    /// Gets the translation one culture has for a string, without falling back to a parent culture, as the Dynamic
    /// Translations page shows it.
    /// </summary>
    /// <param name="resourceManager">The resource manager of the dynamic translations.</param>
    /// <param name="descriptor">The string.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The translation, or <see langword="null"/> when the culture has none.</returns>
    public static string GetTranslation(DataResourceManager resourceManager, DataLocalizedString descriptor, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(descriptor.Name) || string.IsNullOrEmpty(descriptor.Context))
        {
            return null;
        }

        var translation = resourceManager.GetString(descriptor.Name, descriptor.Context, culture);

        return string.IsNullOrWhiteSpace(translation)
            ? null
            : translation;
    }
}
