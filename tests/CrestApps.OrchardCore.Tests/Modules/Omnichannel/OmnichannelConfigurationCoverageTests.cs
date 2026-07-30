using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Proves that every entity a tenant configures in the CRM can leave the tenant in a deployment plan. Dispositions,
/// channel endpoints and campaigns are the vocabulary the contact centre routes and reports on, so a tenant that can
/// only be configured by hand cannot be promoted from staging, and an entity that is deliberately excluded has to say
/// so in writing rather than by omission.
/// </summary>
public sealed class OmnichannelConfigurationCoverageTests
{
    private const int MinimumEntityCount = 7;

    /// <summary>
    /// The entities that are runtime state rather than configuration, and why.
    /// </summary>
    private static readonly Dictionary<string, string> _runtimeState = new(StringComparer.Ordinal)
    {
        ["OmnichannelActivity"] = "Runtime state. One row per unit of work, produced by traffic and campaigns.",
        ["OmnichannelActivityBatch"] = "Runtime state. One row per batch load of activities.",
    };

    /// <summary>
    /// The entities a deployment plan carries, mapped to the recipe step that carries them.
    /// </summary>
    private static readonly Dictionary<string, string> _configuration = new(StringComparer.Ordinal)
    {
        [nameof(OmnichannelCampaign)] = OmnichannelDeploymentSteps.Campaign,
        [nameof(OmnichannelCampaignGroup)] = OmnichannelDeploymentSteps.CampaignGroup,
        [nameof(OmnichannelChannelEndpoint)] = OmnichannelDeploymentSteps.ChannelEndpoint,
        [nameof(OmnichannelDisposition)] = OmnichannelDeploymentSteps.Disposition,
        [nameof(SubjectAction)] = OmnichannelDeploymentSteps.SubjectAction,
        [nameof(SubjectFlowSettings)] = OmnichannelDeploymentSteps.SubjectFlowSettings,
    };

    [Fact]
    public void EveryStoredEntity_IsEitherExportedAsConfigurationOrDeclaredAsRuntimeState()
    {
        var entities = GetEntities();

        Assert.True(
            entities.Length >= MinimumEntityCount,
            $"Only {entities.Length} stored entities were discovered, which is fewer than the {MinimumEntityCount} " +
            "known to exist. The reflection that finds them has stopped working, so this test would pass without " +
            "checking anything.");

        var undeclared = entities
            .Select(entity => entity.Name)
            .Where(name => !_configuration.ContainsKey(name) && !_runtimeState.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            undeclared.Length == 0,
            "These entities are neither exported in a deployment plan nor declared as runtime state, so nobody has " +
            "decided whether an operator can script them: " + string.Join(", ", undeclared) + ". Add a deployment " +
            "source and recipe step for the entity and register them in the Omnichannel configuration startups, then " +
            "add it to the configuration map, or record in the runtime state map why it must not travel between " +
            "environments.");

        var contradictory = _configuration.Keys.Intersect(_runtimeState.Keys, StringComparer.Ordinal).ToArray();

        Assert.True(
            contradictory.Length == 0,
            "These entities are declared as both configuration and runtime state: " + string.Join(", ", contradictory) + ".");
    }

    [Fact]
    public void EveryDeclaredEntity_StillExists()
    {
        var names = GetEntities().Select(entity => entity.Name).ToHashSet(StringComparer.Ordinal);

        var stale = _configuration.Keys
            .Concat(_runtimeState.Keys)
            .Where(name => !names.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            "These entities are declared here but no longer exist, so the declaration is no longer describing the " +
            "database: " + string.Join(", ", stale) + ".");
    }

    [Fact]
    public void EveryConfigurationEntity_IsCarriedByADistinctStep()
    {
        var steps = _configuration.Values.ToArray();

        Assert.Equal(steps.Length, steps.Distinct(StringComparer.Ordinal).Count());
        Assert.All(steps, step => Assert.False(string.IsNullOrWhiteSpace(step)));
    }

    private static Type[] GetEntities()
    {
        return typeof(OmnichannelDisposition).Assembly
            .GetTypes()
            .Where(type => type.IsClass
                && !type.IsAbstract
                && type.IsPublic
                && typeof(CatalogItem).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
