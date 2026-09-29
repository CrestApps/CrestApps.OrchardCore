using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Models;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Reads the profile values a profile template file's front matter carries beyond the ones the shared template
/// parser knows.
/// </summary>
/// <remarks>
/// The template parser leaves every front-matter key it does not know in
/// <see cref="TemplateMetadata.AdditionalProperties"/>, which is where these keys are found.
/// </remarks>
internal static class ProfileTemplateDefaultsReader
{
    /// <summary>
    /// Stores the profile values found in <paramref name="metadata"/> on <paramref name="template"/> as a
    /// <see cref="ProfileTemplateDefaultsMetadata"/>.
    /// </summary>
    /// <param name="template">The template parsed from the file.</param>
    /// <param name="metadata">The front matter parsed from the same file.</param>
    /// <remarks>
    /// Nothing is stored when the file carries none of the keys, so an ordinary template stays unchanged.
    /// </remarks>
    internal static void Apply(AIProfileTemplate template, TemplateMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(template);

        var properties = metadata?.AdditionalProperties;

        if (properties is null || properties.Count == 0)
        {
            return;
        }

        if (properties.TryGetValue(nameof(ProfileTemplateDefaultsMetadata.InitialPrompt), out var initialPrompt) &&
            !string.IsNullOrWhiteSpace(initialPrompt))
        {
            template.Put(new ProfileTemplateDefaultsMetadata
            {
                InitialPrompt = initialPrompt.Trim(),
            });
        }
    }
}
