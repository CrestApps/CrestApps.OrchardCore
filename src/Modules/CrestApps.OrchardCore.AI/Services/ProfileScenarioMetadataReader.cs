using System.Globalization;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Models;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Reads the scenario presentation keys from a profile template file's front matter.
/// </summary>
/// <remarks>
/// The template parser leaves every front-matter key it does not know in
/// <see cref="TemplateMetadata.AdditionalProperties"/>, which is where these keys are found.
/// </remarks>
internal static class ProfileScenarioMetadataReader
{
    /// <summary>
    /// Stores the scenario keys found in <paramref name="metadata"/> on <paramref name="template"/> as a
    /// <see cref="ProfileScenarioMetadata"/>.
    /// </summary>
    /// <param name="template">The template parsed from the file.</param>
    /// <param name="metadata">The front matter parsed from the same file.</param>
    /// <remarks>
    /// Nothing is stored when the file carries none of the keys, so an ordinary template stays unchanged. A
    /// value that does not parse is ignored rather than failing the whole template.
    /// </remarks>
    internal static void Apply(AIProfileTemplate template, TemplateMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(template);

        var properties = metadata?.AdditionalProperties;

        if (properties is null || properties.Count == 0)
        {
            return;
        }

        var scenario = new ProfileScenarioMetadata();
        var hasValue = false;

        if (properties.TryGetValue(nameof(ProfileScenarioMetadata.Featured), out var featuredValue) &&
            bool.TryParse(featuredValue, out var featured))
        {
            scenario.Featured = featured;
            hasValue = true;
        }

        if (properties.TryGetValue(nameof(ProfileScenarioMetadata.Icon), out var icon) &&
            !string.IsNullOrWhiteSpace(icon))
        {
            scenario.Icon = icon.Trim();
            hasValue = true;
        }

        if (properties.TryGetValue(nameof(ProfileScenarioMetadata.Order), out var orderValue) &&
            int.TryParse(orderValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var order))
        {
            scenario.Order = order;
            hasValue = true;
        }

        if (properties.TryGetValue(nameof(ProfileScenarioMetadata.RequiresFeatures), out var requiresFeaturesValue) &&
            !string.IsNullOrWhiteSpace(requiresFeaturesValue))
        {
            scenario.RequiresFeatures = requiresFeaturesValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            hasValue = scenario.RequiresFeatures.Length > 0 || hasValue;
        }

        if (hasValue)
        {
            template.Put(scenario);
        }
    }
}
