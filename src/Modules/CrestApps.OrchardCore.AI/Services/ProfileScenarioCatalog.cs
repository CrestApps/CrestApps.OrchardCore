using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Turns profile templates into the starting points offered by the "New AI profile" picker.
/// </summary>
internal sealed class ProfileScenarioCatalog
{
    private readonly IAIProfileTemplateManager _templateManager;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileScenarioCatalog"/> class.
    /// </summary>
    /// <param name="templateManager">The AI profile template manager.</param>
    /// <param name="shellFeaturesManager">The shell features manager.</param>
    public ProfileScenarioCatalog(
        IAIProfileTemplateManager templateManager,
        IShellFeaturesManager shellFeaturesManager)
    {
        _templateManager = templateManager;
        _shellFeaturesManager = shellFeaturesManager;
    }

    /// <summary>
    /// Gets the starting points the picker lists, featured scenarios first.
    /// </summary>
    /// <returns>
    /// The listable profile templates, sorted so that grouping them by category keeps each category's
    /// scenarios in order.
    /// </returns>
    /// <remarks>
    /// A template-generated prompt is left out. It runs inside an existing chat session rather than standing
    /// on its own, so it does not answer "what do you want to build?". Such a template can still be used from
    /// the templates list or the blank profile editor.
    /// </remarks>
    public async Task<IList<ProfileScenarioCardViewModel>> GetPickerScenariosAsync()
    {
        var entries = (await _templateManager.GetAsync(AITemplateSources.Profile))
            .Where(template => template.IsListable)
            .Select(template => new Entry(
                template,
                template.GetOrCreate<ProfileScenarioMetadata>(),
                template.GetOrCreate<ProfileTemplateMetadata>().ProfileType))
            .Where(entry => entry.ProfileType != AIProfileType.TemplatePrompt)
            .ToList();

        var features = await GetFeatureLookupAsync();

        var featured = entries
            .Where(entry => entry.Scenario.Featured)
            .ToList();

        // A category comes in the position of its first scenario, so the order written in the scenario files
        // decides both the order of the categories and the order of the scenarios inside each one.
        var categoryOrder = featured
            .GroupBy(entry => entry.Template.Category ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Min(entry => entry.Scenario.Order), StringComparer.OrdinalIgnoreCase);

        var orderedFeatured = featured
            .OrderBy(entry => categoryOrder[entry.Template.Category ?? string.Empty])
            .ThenBy(entry => entry.Template.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Scenario.Order)
            .ThenBy(entry => GetTitle(entry.Template), StringComparer.OrdinalIgnoreCase);

        var orderedOthers = entries
            .Where(entry => !entry.Scenario.Featured)
            .OrderBy(entry => string.IsNullOrEmpty(entry.Template.Category))
            .ThenBy(entry => entry.Template.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => GetTitle(entry.Template), StringComparer.OrdinalIgnoreCase);

        return orderedFeatured
            .Concat(orderedOthers)
            .Select(entry => BuildCard(entry.Template, entry.Scenario, features))
            .ToList();
    }

    /// <summary>
    /// Describes a single template as a starting point, including the features it still needs.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The starting point.</returns>
    public async Task<ProfileScenarioCardViewModel> GetScenarioAsync(AIProfileTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return BuildCard(template, template.GetOrCreate<ProfileScenarioMetadata>(), await GetFeatureLookupAsync());
    }

    /// <summary>
    /// Gets the display title of a template.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The template's display text, or its name when it has none.</returns>
    public static string GetTitle(AIProfileTemplate template)
        => string.IsNullOrEmpty(template.DisplayText) ? template.Name : template.DisplayText;

    private async Task<FeatureLookup> GetFeatureLookupAsync()
    {
        var enabledFeatureIds = (await _shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var featureNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var feature in await _shellFeaturesManager.GetAvailableFeaturesAsync())
        {
            featureNames[feature.Id] = string.IsNullOrEmpty(feature.Name) ? feature.Id : feature.Name;
        }

        return new FeatureLookup(enabledFeatureIds, featureNames);
    }

    private static ProfileScenarioCardViewModel BuildCard(AIProfileTemplate template, ProfileScenarioMetadata scenario, FeatureLookup features)
    {
        return new ProfileScenarioCardViewModel
        {
            TemplateId = template.ItemId,
            Title = GetTitle(template),
            Description = template.Description,
            Category = template.Category,
            Icon = scenario.Icon,
            IsFeatured = scenario.Featured,
            ProfileType = template.GetOrCreate<ProfileTemplateMetadata>().ProfileType,
            MissingFeatureNames = GetMissingFeatureNames(scenario, features),
        };
    }

    private static List<string> GetMissingFeatureNames(ProfileScenarioMetadata scenario, FeatureLookup features)
    {
        if (scenario.RequiresFeatures is not { Length: > 0 })
        {
            return [];
        }

        return scenario.RequiresFeatures
            .Where(featureId => !features.EnabledFeatureIds.Contains(featureId))
            .Select(featureId => features.FeatureNames.TryGetValue(featureId, out var name) ? name : featureId)
            .ToList();
    }

    private sealed record Entry(AIProfileTemplate Template, ProfileScenarioMetadata Scenario, AIProfileType? ProfileType);

    private sealed record FeatureLookup(HashSet<string> EnabledFeatureIds, Dictionary<string, string> FeatureNames);
}
