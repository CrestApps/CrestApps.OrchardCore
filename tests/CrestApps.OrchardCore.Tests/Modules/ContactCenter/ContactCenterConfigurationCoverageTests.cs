using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves that every entity a tenant configures can leave the tenant in a deployment plan. A configuration entity that
/// no plan exports cannot be scripted, reviewed in source control, or promoted from a staging environment, so it has to
/// be rebuilt by hand in production; the failure is discovered during a cutover rather than during a build. An entity
/// that is deliberately excluded has to say so in writing, because silence is indistinguishable from an oversight.
/// </summary>
public sealed class ContactCenterConfigurationCoverageTests
{
    private const int MinimumEntityCount = 20;

    /// <summary>
    /// The entities that are runtime state rather than configuration, and why. Runtime state is produced by traffic and
    /// is meaningless in another environment, so carrying it in a deployment plan would move a source tenant's live work
    /// into a destination tenant.
    /// </summary>
    private static readonly Dictionary<string, string> _runtimeState = new(StringComparer.Ordinal)
    {
        ["AgentProfile"] = "Tenant-local. It binds a Contact Center agent to an Orchard user that does not exist in the destination environment, and it carries live presence and the agent's current reservation, which are produced by traffic. Carrying it would also write the agent roster, with user names and identifiers, into a plan committed to source control.",
        ["ActivityReservation"] = "Runtime state. One row per offer of work to an agent; replaying one would assign work that no longer exists.",
        ["AgentSession"] = "Runtime state. One row per signed-in agent, heartbeat driven.",
        ["CallSession"] = "Runtime state. One row per call in progress or completed.",
        ["CallbackRequest"] = "Runtime state. One row per caller waiting to be called back.",
        ["ContactCenterEventMetric"] = "Derived state. Aggregated counters rebuilt by projecting events.",
        ["ContactCenterEventMetricDelta"] = "Derived state. Counts appended but not yet folded into the daily totals; the roller drains them within a minute.",
        ["ContactCenterOutboxMessage"] = "Runtime state. Messages awaiting delivery by this node.",
        ["ContactCenterProcessedEvent"] = "Runtime state. Deduplication ledger for events this tenant already handled.",
        ["ContactCenterProjectionCheckpoint"] = "Runtime state. Per-projection position; copying one would skip events in the destination.",
        ["ContactCenterWorkState"] = "Runtime state. The current disposition of work in flight.",
        ["Interaction"] = "Communication history. One row per contact attempt, produced by traffic.",
        ["InteractionEvent"] = "Communication history. The append-only event stream behind an interaction.",
        ["ProviderCommand"] = "Runtime state. Commands issued to a telephony provider for a live call.",
        ["ProviderWebhookInboxMessage"] = "Runtime state. Provider callbacks awaiting processing.",
        ["QueueItem"] = "Runtime state. Work currently waiting in a queue.",
    };

    /// <summary>
    /// The entities a deployment plan carries, mapped to the recipe step that carries them. Adding a configuration
    /// entity without adding it here fails the build.
    /// </summary>
    private static readonly Dictionary<string, string> _configuration = new(StringComparer.Ordinal)
    {
        [nameof(ActivityQueue)] = ContactCenterDeploymentSteps.Queue,
        [nameof(ActivityQueueGroup)] = ContactCenterDeploymentSteps.QueueGroup,
        [nameof(AgentStateReasonCode)] = ContactCenterDeploymentSteps.AgentStateReasonCode,
        [nameof(BusinessHoursCalendar)] = ContactCenterDeploymentSteps.BusinessHoursCalendar,
        [nameof(ContactCenterEntryPoint)] = ContactCenterDeploymentSteps.EntryPoint,
        [nameof(ContactCenterSkill)] = ContactCenterDeploymentSteps.Skill,
        [nameof(DialerProfile)] = ContactCenterDeploymentSteps.DialerProfile,
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
            "decided whether an operator can script them: " + string.Join(", ", undeclared) + ". Register the entity " +
            "with a deployment source and recipe step in the Contact Center startup and add it to the configuration map, or record " +
            "in the runtime state map why it must not travel between environments.");

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
        return typeof(ContactCenterSkill).Assembly
            .GetTypes()
            .Where(type => type.IsClass
                && !type.IsAbstract
                && type.IsPublic
                && typeof(CatalogItem).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
