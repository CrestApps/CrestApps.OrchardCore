using System.Text.RegularExpressions;
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
        [nameof(Cadence)] = OmnichannelDeploymentSteps.Cadence,
        [nameof(OmnichannelCampaign)] = OmnichannelDeploymentSteps.Campaign,
        [nameof(OmnichannelCampaignGroup)] = OmnichannelDeploymentSteps.CampaignGroup,
        [nameof(OmnichannelChannelEndpoint)] = OmnichannelDeploymentSteps.ChannelEndpoint,
        [nameof(OmnichannelDisposition)] = OmnichannelDeploymentSteps.Disposition,
        [nameof(SubjectAction)] = OmnichannelDeploymentSteps.SubjectAction,
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

    [Fact]
    public void EveryAutomationTunable_IsReadFromOptions_NotFromAConstant()
    {
        // A tuning number compiled into the pass cannot be changed for a node that is slower or busier than the
        // one it was chosen on, so an operator's only remedy is a rebuild. The lease is the single exception: it
        // is an attribute argument, which the language requires to be a constant.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Modules",
            "CrestApps.OrchardCore.Omnichannel.Managements",
            "BackgroundTasks",
            "AutomatedActivitiesProcessorBackgroundTask.cs"));

        var constants = Regex.Matches(source, @"private const int (?<name>\w+)")
            .Select(match => match.Groups["name"].Value)
            .Where(name => !string.Equals(name, "_leaseMilliseconds", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            constants.Length == 0,
            "These tuning values are compiled into the automated-activity processor instead of being bound from " +
            "'CrestApps:Omnichannel:Automation': " + string.Join(", ", constants) + ". Add the value to " +
            nameof(OmnichannelAutomationOptions) + ", validate it, and read it through IOptions in DoWorkAsync.");

        Assert.Contains("IOptions<" + nameof(OmnichannelAutomationOptions) + ">", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryAutomationOption_IsValidated()
    {
        // An option nobody validates is one a typo can set to zero, which presents as a queue that silently never
        // drains rather than as a tenant that refuses to start.
        var validatorSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Core",
            "CrestApps.OrchardCore.Omnichannel.Core",
            "Services",
            "OmnichannelAutomationOptionsValidator.cs"));

        var unvalidated = typeof(OmnichannelAutomationOptions)
            .GetProperties()
            .Where(property => property.CanWrite)
            .Select(property => property.Name)
            .Where(name => !validatorSource.Contains("options." + name, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unvalidated.Length == 0,
            "These automation options are never validated, so a nonsensical value reaches the processing pass: " +
            string.Join(", ", unvalidated) + ".");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
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
