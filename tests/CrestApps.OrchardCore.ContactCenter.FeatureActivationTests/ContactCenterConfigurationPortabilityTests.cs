using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves that a Contact Center tenant's configuration can leave one environment and arrive intact in another.
/// </summary>
/// <remarks>
/// A deployment step that exists is not the same as a deployment step that works. The failure this guards against is
/// silent and total: a step that hand-lists the properties it exports drops every property added after it was written,
/// and the operator only discovers it after promoting to production, because the import succeeds and the missing
/// settings simply read as their defaults. The oracle is therefore a real export from one live tenant replayed into a
/// second, empty tenant, compared field by field over entries whose every property was deliberately set away from its
/// default. Each configuration entity carries its own deployment step and its own recipe step, and an import matches an
/// exported entry to a stored one by identifier alone, creating the entry when the identifier is absent and preserving
/// the exported identifier so that the references other entries hold keep resolving.
/// </remarks>
public sealed class ContactCenterConfigurationPortabilityTests
{
    private const string DeploymentFeatureId = "OrchardCore.Deployment";
    private const string RecipesFeatureId = "OrchardCore.Recipes.Core";

    private const string ContactCenterGroup = "ContactCenter";
    private const string OmnichannelGroup = "Omnichannel";

    /// <summary>
    /// The members a destination environment writes for itself, excluded from the settings comparison. Creation and
    /// modification stamps are written by the receiving tenant's clock, and ownership is written by the receiving
    /// tenant's identity, so requiring them to match would assert that a copy is indistinguishable from the original
    /// rather than that its configuration is. The identifier is excluded because its preservation is proven on its own
    /// by the identifier round-trip test, which keeps this comparison focused on the settings an entry carries rather
    /// than on its identity.
    /// </summary>
    private static readonly string[] _environmentOwnedMembers =
    [
        "CreatedUtc",
        "ModifiedUtc",
        "OwnerId",
        "Author",
        "ItemId",
    ];

    private static readonly string[] _configurationFeatures =
    [
        ContactCenterConstants.Feature.Agents,
        ContactCenterConstants.Feature.Queues,
        ContactCenterConstants.Feature.InboundVoice,
        ContactCenterConstants.Feature.Dialer,
        DeploymentFeatureId,
        RecipesFeatureId,
    ];

    private static readonly ConfigurationGroup[] _groups =
    [
        new ConfigurationGroup(
            ContactCenterGroup,
            [
                new ConfigurationCatalog(ContactCenterDeploymentSteps.Skill, "Skills", typeof(ContactCenterSkill), typeof(IContactCenterSkillManager), static () => new ContactCenterSkillDeploymentStep()),
                new ConfigurationCatalog(ContactCenterDeploymentSteps.QueueGroup, "QueueGroups", typeof(ActivityQueueGroup), typeof(IActivityQueueGroupManager), static () => new ContactCenterQueueGroupDeploymentStep()),
                new ConfigurationCatalog(ContactCenterDeploymentSteps.BusinessHoursCalendar, "Calendars", typeof(BusinessHoursCalendar), typeof(IBusinessHoursCalendarManager), static () => new ContactCenterBusinessHoursCalendarDeploymentStep()),
                new ConfigurationCatalog(ContactCenterDeploymentSteps.Queue, "Queues", typeof(ActivityQueue), typeof(IActivityQueueManager), static () => new ContactCenterQueueDeploymentStep()),
                new ConfigurationCatalog(ContactCenterDeploymentSteps.EntryPoint, "EntryPoints", typeof(ContactCenterEntryPoint), typeof(IContactCenterEntryPointManager), static () => new ContactCenterEntryPointDeploymentStep()),
                new ConfigurationCatalog(ContactCenterDeploymentSteps.DialerProfile, "DialerProfiles", typeof(DialerProfile), typeof(IDialerProfileManager), static () => new ContactCenterDialerProfileDeploymentStep()),
                new ConfigurationCatalog(ContactCenterDeploymentSteps.AgentStateReasonCode, "ReasonCodes", typeof(AgentStateReasonCode), typeof(IAgentStateReasonCodeManager), static () => new AgentStateReasonCodeDeploymentStep()),
            ]),
        new ConfigurationGroup(
            OmnichannelGroup,
            [
                new ConfigurationCatalog(OmnichannelDeploymentSteps.Disposition, "Dispositions", typeof(OmnichannelDisposition), typeof(INamedCatalogManager<OmnichannelDisposition>), static () => new OmnichannelDispositionDeploymentStep()),
                new ConfigurationCatalog(OmnichannelDeploymentSteps.ChannelEndpoint, "ChannelEndpoints", typeof(OmnichannelChannelEndpoint), typeof(IOmnichannelChannelEndpointManager), static () => new OmnichannelChannelEndpointDeploymentStep()),
                new ConfigurationCatalog(OmnichannelDeploymentSteps.CampaignGroup, "CampaignGroups", typeof(OmnichannelCampaignGroup), typeof(ICatalogManager<OmnichannelCampaignGroup>), static () => new OmnichannelCampaignGroupDeploymentStep()),
                new ConfigurationCatalog(OmnichannelDeploymentSteps.Campaign, "Campaigns", typeof(OmnichannelCampaign), typeof(ICatalogManager<OmnichannelCampaign>), static () => new OmnichannelCampaignDeploymentStep()),
                new ConfigurationCatalog(OmnichannelDeploymentSteps.SubjectAction, "SubjectActions", typeof(SubjectAction), typeof(ISourceCatalogManager<SubjectAction>), static () => new OmnichannelSubjectActionDeploymentStep()),
            ]),
    ];

    /// <summary>
    /// Describes one configuration entity that travels between environments as its own recipe step.
    /// </summary>
    /// <param name="StepName">The name shared by the deployment step and the recipe step that carry the entity.</param>
    /// <param name="CollectionName">The property that holds the exported entries inside the recipe step.</param>
    /// <param name="EntryType">The entity type the catalog stores.</param>
    /// <param name="ManagerType">The catalog manager that owns the stored entries.</param>
    /// <param name="CreateStep">Creates the deployment step that exports the entity.</param>
    private sealed record ConfigurationCatalog(
        string StepName,
        string CollectionName,
        Type EntryType,
        Type ManagerType,
        Func<DeploymentStep> CreateStep);

    /// <summary>
    /// Describes the catalogs a feature area exports, listed in the order a plan has to import them so that every
    /// reference resolves as it is written.
    /// </summary>
    /// <param name="Group">The area the catalogs belong to.</param>
    /// <param name="Catalogs">The catalogs the area exports, in dependency order.</param>
    private sealed record ConfigurationGroup(string Group, ConfigurationCatalog[] Catalogs);

    private static ConfigurationGroup GetGroup(string group)
        => _groups.Single(candidate => candidate.Group == group);

    [Theory]
    [InlineData(ContactCenterGroup)]
    [InlineData(OmnichannelGroup)]
    public async Task EveryConfigurationCatalog_IsExportedByTheDeploymentStep(string group)
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = $"configuration-export-coverage-{group.ToLowerInvariant()}",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        await SeedAsync(host, source, group);

        var plan = await ExportAsync(host, source, group);
        var exportedSteps = plan.Select(step => step["name"].GetValue<string>()).ToArray();

        Assert.Equal(
            GetGroup(group).Catalogs.Select(catalog => catalog.StepName).OrderBy(name => name, StringComparer.Ordinal),
            exportedSteps.OrderBy(name => name, StringComparer.Ordinal));

        Assert.All(plan, step => Assert.NotEmpty(GetEntries(step)));
    }

    [Theory]
    [InlineData(ContactCenterGroup)]
    [InlineData(OmnichannelGroup)]
    public async Task ExportedConfiguration_ReplaysIntoAnEmptyTenantWithoutLosingAnySetting(string group)
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = $"configuration-export-origin-{group.ToLowerInvariant()}",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = $"configuration-export-target-{group.ToLowerInvariant()}",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var seeded = await SeedAsync(host, source, group);

        var plan = await ExportAsync(host, source, group);

        await ImportAsync(host, destination, plan);

        var original = await ReadAsync(host, source, group);
        var replayed = await ReadAsync(host, destination, group);

        Assert.All(original, step => Assert.NotEmpty(GetEntries(step)));
        Assert.All(replayed, step => Assert.NotEmpty(GetEntries(step)));

        var differences = Compare(original, replayed);

        Assert.True(
            differences.Length == 0,
            Describe(
                $"{group} configuration did not survive a deployment plan, so promoting a tenant between " +
                "environments silently loses settings.",
                "Export the whole entry rather than a hand-listed subset of its properties.",
                differences));

        Assert.Equal(GetGroup(group).Catalogs.Length, replayed.Length);
        Assert.Equal(GetGroup(group).Catalogs.Length, plan.Length);
        Assert.True(
            seeded > GetGroup(group).Catalogs.Length * 4,
            $"The probe only set {seeded} properties across the configuration catalogs, which is too few to prove a " +
            "property is not being dropped. Populate every writable member before comparing.");
    }

    [Theory]
    [InlineData(ContactCenterGroup)]
    [InlineData(OmnichannelGroup)]
    public async Task ReplayingTheSamePlanTwice_DoesNotDuplicateConfiguration(string group)
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = $"configuration-idempotency-origin-{group.ToLowerInvariant()}",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = $"configuration-idempotency-target-{group.ToLowerInvariant()}",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        await SeedAsync(host, source, group);

        var plan = await ExportAsync(host, source, group);

        await ImportAsync(host, destination, plan);
        var afterFirst = await ReadAsync(host, destination, group);

        await ImportAsync(host, destination, plan);
        var afterSecond = await ReadAsync(host, destination, group);

        Assert.Empty(Compare(afterFirst, afterSecond));
        Assert.Empty(Compare(await ReadAsync(host, source, group), afterSecond));
        Assert.All(afterSecond, step => Assert.NotEmpty(GetEntries(step)));
    }

    /// <summary>
    /// Configuration is changed by clearing a setting as often as by filling one in. If the plan does not carry the
    /// cleared value, replaying it leaves the destination holding the value the source deliberately removed, and the
    /// two environments quietly disagree about a setting an operator believes they have just synchronised.
    /// </summary>
    [Fact]
    public async Task ClearingASettingAtTheSource_ClearsItWhenThePlanIsReplayed()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-cleared-origin",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-cleared-target",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        await host.ExecuteInTenantScopeAsync(source, async serviceProvider =>
        {
            var manager = serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaign>>();
            var campaign = await manager.NewAsync(new JsonObject());

            campaign.DisplayText = "Retention";
            campaign.Description = "Filled in before the plan was first taken.";
            await manager.CreateAsync(campaign);

            return true;
        });

        await ImportAsync(host, destination, await ExportAsync(host, source, OmnichannelGroup));

        var carried = await ReadDescriptionAsync(host, destination);

        Assert.Equal("Filled in before the plan was first taken.", carried);

        await host.ExecuteInTenantScopeAsync(source, async serviceProvider =>
        {
            var manager = serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaign>>();
            var campaigns = await manager.GetAllAsync();
            var campaign = campaigns.Single(candidate => candidate.DisplayText == "Retention");

            campaign.Description = null;
            await manager.UpdateAsync(campaign);

            return true;
        });

        await ImportAsync(host, destination, await ExportAsync(host, source, OmnichannelGroup));

        var cleared = await ReadDescriptionAsync(host, destination);

        Assert.True(
            cleared is null,
            Describe(
                "A setting cleared at the source is still set on the destination after the plan was replayed, so the " +
                "two environments disagree about configuration an operator believes is synchronised.",
                "Carry cleared values in the plan instead of omitting them.",
                [$"The destination still holds '{cleared}'."]));
    }

    /// <summary>
    /// A queue references a channel endpoint that a different deployment step carries, so the endpoint is not present
    /// on the destination when the queue is imported. Because an import preserves the identifier every entry was
    /// exported with, the endpoint lands under that same identifier and the queue's reference resolves regardless of
    /// the order the two steps are imported in. This is what makes a cross-step reference survive a replay now that
    /// nothing re-points references during import.
    /// </summary>
    [Fact]
    public async Task AQueueThatReferencesAChannelEndpoint_LandsPointingAtTheEndpointTheTenantHolds()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-crossgroup-origin",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-crossgroup-target",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var sourceEndpointId = await host.ExecuteInTenantScopeAsync(source, async serviceProvider =>
        {
            var endpointId = await CreateEndpointAsync(serviceProvider);
            var queueManager = serviceProvider.GetRequiredService<IActivityQueueManager>();
            var queue = await queueManager.NewAsync(new JsonObject());

            queue.Name = "Inbound";
            queue.InboundChannelEndpointId = endpointId;
            await queueManager.CreateAsync(queue);

            return endpointId;
        });

        // The Contact Center step is placed first, the way the operator documentation describes building a plan, so the
        // queue is imported before the endpoint it references has been created on the destination.
        var plan = await ExportAsync(host, source, ContactCenterGroup);
        var crmPlan = await ExportAsync(host, source, OmnichannelGroup);

        await ImportAsync(host, destination, [.. plan, .. crmPlan]);

        var landed = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var queues = await serviceProvider.GetRequiredService<IActivityQueueManager>().GetAllAsync();
            var endpoints = await serviceProvider.GetRequiredService<IOmnichannelChannelEndpointManager>().GetAllAsync();

            return new
            {
                EndpointId = queues.Single(candidate => candidate.Name == "Inbound").InboundChannelEndpointId,
                EndpointIds = endpoints.Select(endpoint => endpoint.ItemId).ToArray(),
            };
        });

        Assert.Equal(sourceEndpointId, landed.EndpointId);

        Assert.True(
            landed.EndpointIds.Contains(landed.EndpointId, StringComparer.Ordinal),
            Describe(
                "An imported queue references a channel endpoint that does not exist on the destination tenant, so " +
                "the plan configured a contact centre that cannot route an inbound number to its queue.",
                "Preserve the identifier of every imported entry so cross-step references resolve without re-pointing.",
                [$"The queue references '{landed.EndpointId}'.", $"The tenant holds: {string.Join(", ", landed.EndpointIds)}."]));
    }

    /// <summary>
    /// A queue overflows into another queue carried by the same step, and the two are imported in export order, which
    /// has nothing to do with the direction of the reference. Because an import preserves the identifier every entry
    /// was exported with, the queue that is overflowed into lands under the identifier the overflowing queue already
    /// names, so the overflow reference resolves whichever of the two is imported first.
    /// </summary>
    [Fact]
    public async Task AQueueThatOverflowsIntoAQueueImportedAfterIt_LandsPointingAtTheQueueTheTenantHolds()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-selfref-origin",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-selfref-target",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var sourceOverflowId = await host.ExecuteInTenantScopeAsync(source, async serviceProvider =>
        {
            var manager = serviceProvider.GetRequiredService<IActivityQueueManager>();
            var overflow = await manager.NewAsync(new JsonObject());

            overflow.Name = "Zulu";
            await manager.CreateAsync(overflow);

            var primary = await manager.NewAsync(new JsonObject());

            primary.Name = "Alpha";
            primary.OverflowQueueId = overflow.ItemId;
            await manager.CreateAsync(primary);

            return overflow.ItemId;
        });

        await ImportAsync(host, destination, await ExportAsync(host, source, ContactCenterGroup));

        var landed = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var queues = await serviceProvider.GetRequiredService<IActivityQueueManager>().GetAllAsync();

            return new
            {
                OverflowQueueId = queues.Single(candidate => candidate.Name == "Alpha").OverflowQueueId,
                QueueIds = queues.Select(queue => queue.ItemId).ToArray(),
            };
        });

        Assert.Equal(sourceOverflowId, landed.OverflowQueueId);

        Assert.True(
            landed.QueueIds.Contains(landed.OverflowQueueId, StringComparer.Ordinal),
            Describe(
                "An imported queue overflows into a queue that does not exist on the destination tenant, so overflow " +
                "routing points at nothing after the plan is replayed.",
                "Preserve the identifier of every imported entry so an overflow reference resolves regardless of order.",
                [$"'Alpha' overflows into '{landed.OverflowQueueId}'.", $"The tenant holds: {string.Join(", ", landed.QueueIds)}."]));
    }

    private static async Task<string> CreateEndpointAsync(IServiceProvider serviceProvider)
    {
        var manager = serviceProvider.GetRequiredService<IOmnichannelChannelEndpointManager>();
        var endpoint = await manager.NewAsync(new JsonObject());

        endpoint.DisplayText = "Main line";
        endpoint.Channel = "voice";
        endpoint.Value = "+15551230000";
        await manager.CreateAsync(endpoint);

        return endpoint.ItemId;
    }

    private static async Task<string> ReadDescriptionAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant)
    {
        return await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var campaigns = await serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaign>>().GetAllAsync();

            return campaigns.Single(candidate => candidate.DisplayText == "Retention").Description;
        });
    }

    /// <summary>
    /// Configuration entries point at one another by identifier: a queue names its queue group, an entry point names
    /// its queue, and a subject action names the disposition that triggers it. Those identifiers are only meaningful
    /// if importing an entry keeps the identifier it was exported with, so an import that mints a fresh identifier
    /// turns every one of those references into a pointer to nothing on the destination tenant.
    /// </summary>
    [Theory]
    [InlineData(ContactCenterGroup)]
    [InlineData(OmnichannelGroup)]
    public async Task ImportedConfiguration_KeepsTheIdentifiersThatOtherEntriesPointAt(string group)
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = $"configuration-identity-origin-{group.ToLowerInvariant()}",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = $"configuration-identity-target-{group.ToLowerInvariant()}",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        await SeedAsync(host, source, group);

        var plan = await ExportAsync(host, source, group);

        await ImportAsync(host, destination, plan);

        var replayed = await ReadAsync(host, destination, group);
        var problems = new List<string>();
        var checkedIdentifiers = 0;

        foreach (var step in plan)
        {
            var stepName = step["name"].GetValue<string>();

            var landed = replayed
                .Single(candidate => candidate["name"].GetValue<string>() == stepName);

            var arrived = GetEntries(landed)
                .OfType<JsonObject>()
                .Select(entry => entry[nameof(CatalogItem.ItemId)]?.GetValue<string>())
                .Where(itemId => !string.IsNullOrEmpty(itemId))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var entry in GetEntries(step).OfType<JsonObject>())
            {
                var itemId = entry[nameof(CatalogItem.ItemId)]?.GetValue<string>();

                if (string.IsNullOrEmpty(itemId))
                {
                    problems.Add($"{stepName}: an exported entry carries no {nameof(CatalogItem.ItemId)}, so nothing can reference it.");

                    continue;
                }

                checkedIdentifiers++;

                if (!arrived.Contains(itemId))
                {
                    problems.Add($"{stepName}: identifier '{itemId}' was exported but does not exist on the destination tenant.");
                }
            }
        }

        Assert.True(
            problems.Count == 0,
            Describe(
                $"{group} configuration was imported under different identifiers, so every cross-reference in the " +
                "plan now points at an entry that does not exist.",
                "Keep the exported identifier when an import creates an entry that the destination does not already have.",
                problems));

        Assert.True(
            checkedIdentifiers >= GetGroup(group).Catalogs.Length,
            $"Only {checkedIdentifiers} identifiers were checked across {GetGroup(group).Catalogs.Length} catalogs, " +
            "so this test would pass without proving anything.");
    }

    private static readonly (string OwningStep, string PropertyName, JsonNode Value)[] _seedOverrides =
    [
        (ContactCenterDeploymentSteps.DialerProfile, nameof(DialerProfile.Mode), JsonValue.Create(nameof(DialerMode.Preview))),
        (ContactCenterDeploymentSteps.DialerProfile, nameof(DialerProfile.CallsPerAgent), JsonValue.Create(PowerDialerStrategy.MaxCallsPerAgent)),
    ];

    private static readonly (string OwningStep, string PropertyName, string ReferencedStep)[] _references =
    [
        (ContactCenterDeploymentSteps.Queue, nameof(ActivityQueue.QueueGroupId), ContactCenterDeploymentSteps.QueueGroup),
    ];

    private static async Task<int> SeedAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant, string group)
    {
        return await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var handlers = serviceProvider.GetServices<IRecipeStepHandler>().ToArray();
            var seededIds = new Dictionary<string, string>(StringComparer.Ordinal);
            var assigned = 0;

            foreach (var catalog in GetGroup(group).Catalogs)
            {
                var (entry, populated) = BuildFullyPopulated(catalog.EntryType, catalog.StepName);

                assigned += populated;
                ApplySeedOverrides(entry, catalog.StepName);
                ApplyReferences(entry, catalog.StepName, seededIds);

                var context = new RecipeExecutionContext
                {
                    Name = catalog.StepName,
                    Step = new JsonObject
                    {
                        ["name"] = catalog.StepName,
                        [catalog.CollectionName] = new JsonArray(entry),
                    },
                };

                foreach (var handler in handlers)
                {
                    await handler.ExecuteAsync(context);
                }

                Assert.True(
                    context.Errors.Count == 0,
                    $"{catalog.StepName}: {string.Join("; ", context.Errors)} :: {entry.ToJsonString()}");

                var stored = (await GetAllAsync(serviceProvider, catalog.ManagerType)).OfType<CatalogItem>().FirstOrDefault();

                if (stored is not null)
                {
                    seededIds[catalog.StepName] = stored.ItemId;
                }
            }

            return assigned;
        });
    }

    private static async Task<JsonObject[]> ExportAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant, string group)
    {
        return await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var sources = serviceProvider.GetServices<IDeploymentSource>();
            var steps = new List<JsonObject>();

            foreach (var catalog in GetGroup(group).Catalogs)
            {
                var step = catalog.CreateStep();
                var result = new DeploymentPlanResult(new NullFileBuilder(), new RecipeDescriptor());

                // Every source is offered the step because a source only contributes to the step type it declares. A
                // source that ignores it contributes nothing, which the step-count assertions would catch. The catalogs
                // are visited in dependency order, so the emitted steps arrive in the order an import has to replay them.
                foreach (var source in sources)
                {
                    await source.ProcessDeploymentStepAsync(step, result);
                }

                steps.AddRange(result.Steps.Cast<JsonObject>());
            }

            return steps.ToArray();
        });
    }

    /// <summary>
    /// Reads the stored entries straight from the catalogs that own them, bypassing the export path entirely.
    /// </summary>
    /// <remarks>
    /// The comparison has to be anchored to something the export code cannot influence. Comparing one export against
    /// another is satisfied by a step that drops the same property on both sides, which is precisely the defect being
    /// guarded against, so the entities themselves are the oracle.
    /// </remarks>
    private static async Task<JsonObject[]> ReadAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant, string group)
    {
        return await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var steps = new List<JsonObject>();

            foreach (var catalog in GetGroup(group).Catalogs)
            {
                var entries = new JsonArray();

                foreach (var entry in await GetAllAsync(serviceProvider, catalog.ManagerType))
                {
                    entries.Add(JsonSerializer.SerializeToNode(entry, entry.GetType(), JOptions.Default));
                }

                steps.Add(new JsonObject
                {
                    ["name"] = catalog.StepName,
                    ["entries"] = Sort(entries),
                });
            }

            return steps.ToArray();
        });
    }

    private static JsonArray Sort(JsonArray entries)
    {
        var sorted = entries
            .Cast<JsonObject>()
            .OrderBy(entry => entry["Name"]?.GetValue<string>() ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(entry => entry["ItemId"]?.GetValue<string>() ?? string.Empty, StringComparer.Ordinal)
            .Select(entry => entry.DeepClone())
            .ToArray();

        return new JsonArray(sorted);
    }

    private static async Task<IEnumerable<object>> GetAllAsync(IServiceProvider serviceProvider, Type managerType)
    {
        var manager = serviceProvider.GetService(managerType);

        Assert.True(manager is not null, $"No catalog manager is registered for '{managerType.Name}'.");

        var method = managerType
            .GetInterfaces()
            .Append(managerType)
            .SelectMany(candidate => candidate.GetMethods())
            .First(candidate => candidate.Name == "GetAllAsync" && candidate.GetParameters().Length == 1);

        var pending = method.Invoke(manager, [CancellationToken.None]);
        var task = pending as Task
            ?? (Task)pending.GetType().GetMethod("AsTask", Type.EmptyTypes).Invoke(pending, null);

        await task;

        return (IEnumerable<object>)task.GetType().GetProperty("Result").GetValue(task);
    }

    private static async Task ImportAsync(
        ContactCenterFeatureActivationHost host,
        ContactCenterTenant tenant,
        JsonObject[] plan)
    {
        // Each step is executed in a scope of its own, the way the recipe executor runs them, so anything a step has
        // to tell the next one cannot be smuggled through a scoped service that would not exist in production.
        var executionId = Guid.NewGuid().ToString("n");

        foreach (var step in plan)
        {
            await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
            {
                var context = new RecipeExecutionContext
                {
                    ExecutionId = executionId,
                    Name = step["name"].GetValue<string>(),
                    Step = step.DeepClone().AsObject(),
                };

                foreach (var handler in serviceProvider.GetServices<IRecipeStepHandler>())
                {
                    await handler.ExecuteAsync(context);
                }

                Assert.Empty(context.Errors);
            });
        }
    }

    private static JsonArray GetEntries(JsonObject step)
    {
        return step.First(property => property.Key != "name").Value.AsArray();
    }

    private static string[] Compare(JsonObject[] expected, JsonObject[] actual)
    {
        var differences = new List<string>();
        var actualByName = actual.ToDictionary(step => step["name"].GetValue<string>(), StringComparer.Ordinal);

        foreach (var step in expected)
        {
            var name = step["name"].GetValue<string>();

            if (!actualByName.TryGetValue(name, out var counterpart))
            {
                differences.Add($"{name}: the replayed tenant has no such step.");

                continue;
            }

            var expectedEntries = GetEntries(step);
            var actualEntries = GetEntries(counterpart);

            if (expectedEntries.Count != actualEntries.Count)
            {
                differences.Add($"{name}: exported {expectedEntries.Count} entries but replayed {actualEntries.Count}.");

                continue;
            }

            for (var i = 0; i < expectedEntries.Count; i++)
            {
                CompareEntry(name, expectedEntries[i].AsObject(), actualEntries[i].AsObject(), differences);
            }
        }

        return [.. differences.OrderBy(difference => difference, StringComparer.Ordinal)];
    }

    private static void CompareEntry(
        string stepName,
        JsonObject expected,
        JsonObject actual,
        List<string> differences)
    {
        foreach (var property in expected)
        {
            if (_environmentOwnedMembers.Contains(property.Key, StringComparer.Ordinal))
            {
                continue;
            }

            var actualValue = actual[property.Key];
            var expectedText = property.Value?.ToJsonString();
            var actualText = actualValue?.ToJsonString();

            if (!string.Equals(expectedText, actualText, StringComparison.Ordinal))
            {
                differences.Add($"{stepName}.{property.Key}: exported {expectedText ?? "null"} but replayed {actualText ?? "null"}.");
            }
        }

        foreach (var property in actual)
        {
            if (_environmentOwnedMembers.Contains(property.Key, StringComparer.Ordinal))
            {
                continue;
            }

            if (!expected.ContainsKey(property.Key))
            {
                differences.Add($"{stepName}.{property.Key}: present after replay but absent from the export.");
            }
        }
    }

    /// <summary>
    /// Replaces seed values that the generated shape cannot get right on its own.
    /// </summary>
    /// <remarks>
    /// The generator picks the last enum member and a fixed number so that every property carries a distinguishable
    /// value. Some of those values are genuinely refused by the entry's rules, so they are replaced with an accepted
    /// value that still differs from the type's default and therefore still proves the property round-trips.
    /// </remarks>
    /// <param name="entry">The entry about to be imported.</param>
    /// <param name="stepName">The catalog step the entry belongs to.</param>
    private static void ApplySeedOverrides(JsonObject entry, string stepName)
    {
        foreach (var (owningStep, propertyName, value) in _seedOverrides)
        {
            if (!string.Equals(owningStep, stepName, StringComparison.Ordinal) || !entry.ContainsKey(propertyName))
            {
                continue;
            }

            entry[propertyName] = value.DeepClone();
        }
    }

    /// <summary>
    /// Points a seeded entry at the identifiers of the entries seeded before it.
    /// </summary>
    /// <remarks>
    /// A synthetic seed value satisfies the shape of a reference but not its meaning. Entries whose handlers require a
    /// reference to resolve are rejected by the import when the reference points at nothing, so the reference is
    /// rewritten to the identifier the earlier catalog actually produced. Catalogs are seeded in dependency order, so
    /// the target is always present by the time it is needed.
    /// </remarks>
    /// <param name="entry">The entry about to be imported.</param>
    /// <param name="stepName">The catalog step the entry belongs to.</param>
    /// <param name="seededIds">The identifiers produced by the catalogs seeded so far, keyed by step name.</param>
    private static void ApplyReferences(JsonObject entry, string stepName, Dictionary<string, string> seededIds)
    {
        foreach (var (owningStep, propertyName, referencedStep) in _references)
        {
            if (!string.Equals(owningStep, stepName, StringComparison.Ordinal)
                || !entry.ContainsKey(propertyName)
                || !seededIds.TryGetValue(referencedStep, out var referencedId))
            {
                continue;
            }

            entry[propertyName] = referencedId;
        }
    }

    private static (JsonObject Entry, int Populated) BuildFullyPopulated(Type type, string seed)
    {
        var instance = Activator.CreateInstance(type);
        var populated = Populate(instance, seed, depth: 0);
        var node = JsonSerializer.SerializeToNode(instance, type, JOptions.Default).AsObject();

        node.Remove(nameof(CatalogItem.ItemId));

        return (node, populated);
    }

    private static int Populate(object instance, string seed, int depth)
    {
        var assigned = 0;

        foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var value = CreateValue(property.PropertyType, $"{seed}-{property.Name}", depth);

            if (value is null)
            {
                continue;
            }

            property.SetValue(instance, value);
            assigned++;
        }

        return assigned;
    }

    private static object CreateValue(Type type, string seed, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string))
        {
            return seed;
        }

        if (underlying.IsEnum)
        {
            var values = Enum.GetValues(underlying).Cast<object>().ToArray();

            return values[values.Length - 1];
        }

        if (underlying == typeof(bool))
        {
            return true;
        }

        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short))
        {
            return Convert.ChangeType(7, underlying);
        }

        if (underlying == typeof(double) || underlying == typeof(decimal) || underlying == typeof(float))
        {
            return Convert.ChangeType(7.5, underlying);
        }

        if (underlying == typeof(DateTime))
        {
            return new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        }

        if (underlying == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        }

        if (underlying == typeof(TimeSpan))
        {
            return TimeSpan.FromMinutes(7);
        }

        if (underlying == typeof(TimeOnly))
        {
            return new TimeOnly(7, 30);
        }

        if (underlying == typeof(DateOnly))
        {
            return new DateOnly(2024, 1, 2);
        }

        if (depth >= 3)
        {
            return null;
        }

        if (underlying.IsGenericType && IsSupportedDictionary(underlying.GetGenericTypeDefinition()))
        {
            var arguments = underlying.GetGenericArguments();
            var key = CreateValue(arguments[0], $"{seed}-key", depth + 1);
            var item = arguments[1] == typeof(object)
                ? seed
                : CreateValue(arguments[1], $"{seed}-value", depth + 1);

            if (key is null || item is null)
            {
                return null;
            }

            var dictionary = (System.Collections.IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments));

            dictionary[key] = item;

            return dictionary;
        }

        if (underlying.IsArray)
        {
            var elementType = underlying.GetElementType();
            var element = CreateValue(elementType, $"{seed}-0", depth + 1);

            if (element is null)
            {
                return null;
            }

            var array = Array.CreateInstance(elementType, 1);
            array.SetValue(element, 0);

            return array;
        }

        if (underlying.IsGenericType && IsSupportedCollection(underlying.GetGenericTypeDefinition()))
        {
            var elementType = underlying.GetGenericArguments()[0];
            var element = CreateValue(elementType, $"{seed}-0", depth + 1);

            if (element is null)
            {
                return null;
            }

            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            list.Add(element);

            return list;
        }

        if (underlying.IsClass && !underlying.IsAbstract && underlying.GetConstructor(Type.EmptyTypes) is not null)
        {
            var nested = Activator.CreateInstance(underlying);

            Populate(nested, seed, depth + 1);

            return nested;
        }

        return null;
    }

    private static bool IsSupportedDictionary(Type definition)
    {
        return definition == typeof(IDictionary<,>)
            || definition == typeof(Dictionary<,>)
            || definition == typeof(IReadOnlyDictionary<,>);
    }

    private static bool IsSupportedCollection(Type definition)
    {
        return definition == typeof(IList<>)
            || definition == typeof(List<>)
            || definition == typeof(ICollection<>)
            || definition == typeof(IEnumerable<>)
            || definition == typeof(IReadOnlyList<>)
            || definition == typeof(IReadOnlyCollection<>);
    }

    private static string Describe(string problem, string remedy, IEnumerable<string> details)
    {
        var builder = new StringBuilder();

        builder.AppendLine(problem);
        builder.AppendLine(remedy);

        foreach (var detail in details)
        {
            builder.AppendLine($"  - {detail}");
        }

        return builder.ToString();
    }

    private sealed class NullFileBuilder : IFileBuilder
    {
        public Task SetFileAsync(string subpath, Stream stream)
        {
            return Task.CompletedTask;
        }
    }
}
