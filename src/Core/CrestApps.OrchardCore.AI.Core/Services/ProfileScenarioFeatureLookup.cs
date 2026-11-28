using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Tells which features a profile template's scenario still needs on this tenant, by the features' names.
/// </summary>
public sealed class ProfileScenarioFeatureLookup
{
    private readonly HashSet<string> _enabledFeatureIds;
    private readonly Dictionary<string, string> _featureNames;

    private ProfileScenarioFeatureLookup(HashSet<string> enabledFeatureIds, Dictionary<string, string> featureNames)
    {
        _enabledFeatureIds = enabledFeatureIds;
        _featureNames = featureNames;
    }

    /// <summary>
    /// Reads the enabled and the available features of the tenant once, so many scenarios can be checked against them.
    /// </summary>
    /// <param name="shellFeaturesManager">The shell features manager.</param>
    /// <returns>The lookup.</returns>
    public static async Task<ProfileScenarioFeatureLookup> CreateAsync(IShellFeaturesManager shellFeaturesManager)
    {
        ArgumentNullException.ThrowIfNull(shellFeaturesManager);

        var enabledFeatureIds = (await shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var featureNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var feature in await shellFeaturesManager.GetAvailableFeaturesAsync())
        {
            featureNames[feature.Id] = string.IsNullOrEmpty(feature.Name) ? feature.Id : feature.Name;
        }

        return new ProfileScenarioFeatureLookup(enabledFeatureIds, featureNames);
    }

    /// <summary>
    /// Gets the names of the features a scenario needs that are not enabled.
    /// </summary>
    /// <param name="scenario">The scenario of a profile template.</param>
    /// <returns>The names of the missing features, or the IDs of features that are not available at all.</returns>
    public List<string> GetMissingFeatureNames(ProfileScenarioMetadata scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        if (scenario.RequiresFeatures is not { Length: > 0 })
        {
            return [];
        }

        return scenario.RequiresFeatures
            .Where(featureId => !_enabledFeatureIds.Contains(featureId))
            .Select(featureId => _featureNames.TryGetValue(featureId, out var name) ? name : featureId)
            .ToList();
    }
}
