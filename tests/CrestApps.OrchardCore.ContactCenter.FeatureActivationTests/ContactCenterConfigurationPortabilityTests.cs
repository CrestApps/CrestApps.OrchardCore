using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Configuration;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Configuration;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using CrestApps.OrchardCore.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Json;
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
/// default.
/// </remarks>
public sealed class ContactCenterConfigurationPortabilityTests
{
    private const string DeploymentFeatureId = "OrchardCore.Deployment";
    private const string RecipesFeatureId = "OrchardCore.Recipes.Core";

    /// <summary>
    /// The members a destination environment owns rather than inherits. Creation and modification stamps are written by
    /// the receiving tenant's clock, and ownership is written by the receiving tenant's identity, so requiring them to
    /// match would assert that a copy is indistinguishable from the original rather than that the configuration is.
    /// The identifier is included because a tenant that already holds an entry of the same name keeps its own
    /// identifier when the plan is replayed, which is what stops a replay from duplicating configuration that a
    /// migration seeded independently in both environments.
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
        ContactCenterConstants.Feature.EntryPoints,
        ContactCenterConstants.Feature.Dialer,
        DeploymentFeatureId,
        RecipesFeatureId,
    ];

    private static readonly ConfigurationGroup[] _groups =
    [
        new ConfigurationGroup(
            ContactCenterConfigurationCatalogs.Group,
            static () => new ContactCenterConfigurationDeploymentStep(),
            [
                (ContactCenterConfigurationCatalogs.Skill, typeof(ContactCenterSkill), typeof(IContactCenterSkillManager)),
                (ContactCenterConfigurationCatalogs.QueueGroup, typeof(ActivityQueueGroup), typeof(IActivityQueueGroupManager)),
                (ContactCenterConfigurationCatalogs.BusinessHoursCalendar, typeof(BusinessHoursCalendar), typeof(IBusinessHoursCalendarManager)),
                (ContactCenterConfigurationCatalogs.Queue, typeof(ActivityQueue), typeof(IActivityQueueManager)),
                (ContactCenterConfigurationCatalogs.EntryPoint, typeof(ContactCenterEntryPoint), typeof(IContactCenterEntryPointManager)),
                (ContactCenterConfigurationCatalogs.DialerProfile, typeof(DialerProfile), typeof(IDialerProfileManager)),
                (ContactCenterConfigurationCatalogs.AgentStateReasonCode, typeof(AgentStateReasonCode), typeof(IAgentStateReasonCodeManager)),
            ]),
        new ConfigurationGroup(
            OmnichannelConfigurationCatalogs.Group,
            static () => new OmnichannelConfigurationDeploymentStep(),
            [
                (OmnichannelConfigurationCatalogs.Disposition, typeof(OmnichannelDisposition), typeof(INamedCatalogManager<OmnichannelDisposition>)),
                (OmnichannelConfigurationCatalogs.ChannelEndpoint, typeof(OmnichannelChannelEndpoint), typeof(IOmnichannelChannelEndpointManager)),
                (OmnichannelConfigurationCatalogs.CampaignGroup, typeof(OmnichannelCampaignGroup), typeof(ICatalogManager<OmnichannelCampaignGroup>)),
                (OmnichannelConfigurationCatalogs.Campaign, typeof(OmnichannelCampaign), typeof(ICatalogManager<OmnichannelCampaign>)),
                (OmnichannelConfigurationCatalogs.SubjectFlowSettings, typeof(SubjectFlowSettings), typeof(ICatalogManager<SubjectFlowSettings>)),
                (OmnichannelConfigurationCatalogs.SubjectAction, typeof(SubjectAction), typeof(ISourceCatalogManager<SubjectAction>)),
            ]),
    ];

    /// <summary>
    /// Describes one deployable group of configuration catalogs.
    /// </summary>
    /// <param name="Group">The catalog group identifier.</param>
    /// <param name="CreateStep">Creates the deployment step that exports the group.</param>
    /// <param name="Catalogs">The catalogs the group is expected to export.</param>
    private sealed record ConfigurationGroup(
        string Group,
        Func<ConfigurationCatalogDeploymentStep> CreateStep,
        (string StepName, Type EntryType, Type ManagerType)[] Catalogs);

    private static ConfigurationGroup GetGroup(string group)
        => _groups.Single(candidate => candidate.Group == group);

    [Theory]
    [InlineData(ContactCenterConfigurationCatalogs.Group)]
    [InlineData(OmnichannelConfigurationCatalogs.Group)]
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
    [InlineData(ContactCenterConfigurationCatalogs.Group)]
    [InlineData(OmnichannelConfigurationCatalogs.Group)]
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
    [InlineData(ContactCenterConfigurationCatalogs.Group)]
    [InlineData(OmnichannelConfigurationCatalogs.Group)]
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
    /// The destination is rarely empty. When both environments created the same queue group independently, the two
    /// copies carry the same name and different identifiers, and the import has to reconcile them - but every queue
    /// in the same plan still names the source's identifier. Unless the substitution is carried forward into the
    /// steps that follow, the plan lands with its references pointing at entries that do not exist there, which is
    /// worse than failing because the tenant looks configured and routes nothing.
    /// </summary>
    [Fact]
    public async Task ImportingIntoATenantThatAlreadyOwnsAnEntry_RepointsTheReferencesInTheRestOfThePlan()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-reference-origin",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-reference-target",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var sourceGroupId = await host.ExecuteInTenantScopeAsync(source, async serviceProvider =>
        {
            var manager = serviceProvider.GetRequiredService<IActivityQueueGroupManager>();
            var group = await manager.NewAsync(new JsonObject());

            group.Name = "Shared group";
            await manager.CreateAsync(group);

            var queueManager = serviceProvider.GetRequiredService<IActivityQueueManager>();
            var queue = await queueManager.NewAsync(new JsonObject());

            queue.Name = "Support";
            queue.QueueGroupId = group.ItemId;
            await queueManager.CreateAsync(queue);

            return group.ItemId;
        });

        var destinationGroupId = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var manager = serviceProvider.GetRequiredService<IActivityQueueGroupManager>();
            var group = await manager.NewAsync(new JsonObject());

            group.Name = "Shared group";
            await manager.CreateAsync(group);

            return group.ItemId;
        });

        Assert.NotEqual(sourceGroupId, destinationGroupId);

        var plan = await ExportAsync(host, source, ContactCenterConfigurationCatalogs.Group);

        await ImportAsync(host, destination, plan);

        var landed = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var queues = await serviceProvider.GetRequiredService<IActivityQueueManager>().GetAllAsync();
            var groups = await serviceProvider.GetRequiredService<IActivityQueueGroupManager>().GetAllAsync();

            var queue = queues.Single(candidate => candidate.Name == "Support");

            return new
            {
                queue.QueueGroupId,
                GroupIds = groups.Select(group => group.ItemId).ToArray(),
                GroupCount = groups.Count(group => group.Name == "Shared group"),
            };
        });

        Assert.Equal(1, landed.GroupCount);

        Assert.True(
            landed.GroupIds.Contains(landed.QueueGroupId, StringComparer.Ordinal),
            Describe(
                "An imported queue references a queue group that does not exist on the destination tenant, so the " +
                "plan configured a contact centre that cannot route.",
                "Carry the identifiers reconciled by one step forward into the steps that follow.",
                [$"The queue references '{landed.QueueGroupId}'.", $"The tenant holds: {string.Join(", ", landed.GroupIds)}."]));

        Assert.Equal(destinationGroupId, landed.QueueGroupId);
    }

    /// <summary>
    /// Not every catalog identifies its entries by a name. Campaigns and campaign groups are identified by display
    /// text, and an import that only understands names would fail to recognise the copy the destination already had,
    /// duplicate it, and leave the campaigns in the plan pointing at the copy nobody is looking at.
    /// </summary>
    [Fact]
    public async Task ImportingAnEntryIdentifiedByDisplayText_ReconcilesItWithTheCopyTheTenantAlreadyHad()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-displaytext-origin",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-displaytext-target",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var sourceGroupId = await host.ExecuteInTenantScopeAsync(source, async serviceProvider =>
        {
            var groupManager = serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaignGroup>>();
            var group = await groupManager.NewAsync(new JsonObject());

            group.DisplayText = "Shared campaign group";
            await groupManager.CreateAsync(group);

            var campaignManager = serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaign>>();
            var campaign = await campaignManager.NewAsync(new JsonObject());

            campaign.DisplayText = "Winback";
            campaign.CampaignGroupId = group.ItemId;
            await campaignManager.CreateAsync(campaign);

            return group.ItemId;
        });

        var destinationGroupId = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var groupManager = serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaignGroup>>();
            var group = await groupManager.NewAsync(new JsonObject());

            group.DisplayText = "Shared campaign group";
            await groupManager.CreateAsync(group);

            return group.ItemId;
        });

        Assert.NotEqual(sourceGroupId, destinationGroupId);

        var plan = await ExportAsync(host, source, OmnichannelConfigurationCatalogs.Group);

        await ImportAsync(host, destination, plan);

        var landed = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var campaigns = await serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaign>>().GetAllAsync();
            var groups = await serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaignGroup>>().GetAllAsync();

            var campaign = campaigns.Single(candidate => candidate.DisplayText == "Winback");

            return new
            {
                campaign.CampaignGroupId,
                GroupIds = groups.Select(group => group.ItemId).ToArray(),
                GroupCount = groups.Count(group => group.DisplayText == "Shared campaign group"),
            };
        });

        Assert.True(
            landed.GroupCount == 1,
            Describe(
                "Importing a campaign group the destination tenant already had produced a second copy of it, because " +
                "the import only recognises entries identified by name.",
                "Match entries identified by display text as well as entries identified by name.",
                [$"The tenant holds {landed.GroupCount} groups named 'Shared campaign group'."]));

        Assert.Equal(destinationGroupId, landed.CampaignGroupId);
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

        await ImportAsync(host, destination, await ExportAsync(host, source, OmnichannelConfigurationCatalogs.Group));

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

        await ImportAsync(host, destination, await ExportAsync(host, source, OmnichannelConfigurationCatalogs.Group));

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
    /// A queue references a channel endpoint that a different deployment step carries, so no ordering of the two
    /// steps makes both references resolvable at the moment they are written. If the plan is replayed into a tenant
    /// that independently created the same endpoints, a queue imported before the CRM step lands pointing at an
    /// endpoint the destination does not hold - and an inbound number stops reaching its queue while the tenant
    /// still looks configured.
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

        var destinationEndpointId = await host.ExecuteInTenantScopeAsync(destination, CreateEndpointAsync);

        Assert.NotEqual(sourceEndpointId, destinationEndpointId);

        // The Contact Center step is placed first, the way the operator documentation describes building a plan, so
        // the queue is imported before the endpoint it references has been reconciled.
        var plan = await ExportAsync(host, source, ContactCenterConfigurationCatalogs.Group);
        var crmPlan = await ExportAsync(host, source, OmnichannelConfigurationCatalogs.Group);

        await ImportAsync(host, destination, [.. plan, .. crmPlan]);

        var landed = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var queues = await serviceProvider.GetRequiredService<IActivityQueueManager>().GetAllAsync();

            return queues.Single(candidate => candidate.Name == "Inbound").InboundChannelEndpointId;
        });

        Assert.Equal(destinationEndpointId, landed);
    }

    /// <summary>
    /// A queue overflows into another queue in the same step, and entries are imported in export order, which has
    /// nothing to do with the direction of the reference. The queue that overflows is therefore routinely imported
    /// before the queue it overflows into is reconciled, and an import that only looks forward leaves overflow
    /// routing pointing at a queue the destination does not have.
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

        await host.ExecuteInTenantScopeAsync(source, async serviceProvider =>
        {
            var manager = serviceProvider.GetRequiredService<IActivityQueueManager>();
            var overflow = await manager.NewAsync(new JsonObject());

            overflow.Name = "Zulu";
            await manager.CreateAsync(overflow);

            var primary = await manager.NewAsync(new JsonObject());

            primary.Name = "Alpha";
            primary.OverflowQueueId = overflow.ItemId;
            await manager.CreateAsync(primary);

            return true;
        });

        var destinationOverflowId = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var manager = serviceProvider.GetRequiredService<IActivityQueueManager>();
            var overflow = await manager.NewAsync(new JsonObject());

            overflow.Name = "Zulu";
            await manager.CreateAsync(overflow);

            return overflow.ItemId;
        });

        await ImportAsync(host, destination, await ExportAsync(host, source, ContactCenterConfigurationCatalogs.Group));

        var landed = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var queues = await serviceProvider.GetRequiredService<IActivityQueueManager>().GetAllAsync();

            return queues.Single(candidate => candidate.Name == "Alpha").OverflowQueueId;
        });

        Assert.Equal(destinationOverflowId, landed);
    }

    /// <summary>
    /// Reconciling an entry with the copy the destination already had depends on the entry carrying something that
    /// identifies it. Subject flow settings and subject actions carry a display text that nothing in the product
    /// ever fills in - they are identified by the subject they belong to and, for an action, by the disposition
    /// that triggers it. A catalog that leans on the unfilled display text creates a second copy on every replay,
    /// and the product then acts on both: the effective flow for a subject becomes whichever copy is read first,
    /// and a duplicated action runs twice for one disposition.
    /// </summary>
    [Fact]
    public async Task ImportingConfigurationThatCarriesNoNameOrDisplayText_ReconcilesItInsteadOfDuplicatingIt()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-keyless-origin",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-keyless-target",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        await host.ExecuteInTenantScopeAsync(source, serviceProvider => CreateFlowAsync(serviceProvider, "The source tenant's copy."));
        await host.ExecuteInTenantScopeAsync(destination, serviceProvider => CreateFlowAsync(serviceProvider, null));

        await ImportAsync(host, destination, await ExportAsync(host, source, OmnichannelConfigurationCatalogs.Group));

        var landed = await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var flows = await serviceProvider.GetRequiredService<ICatalogManager<SubjectFlowSettings>>().GetAllAsync();

            return flows.Where(candidate => candidate.SubjectContentType == "SupportCase").ToArray();
        });

        Assert.True(
            landed.Length == 1,
            Describe(
                "Replaying a plan created a second set of flow settings for a subject the destination had already " +
                "configured, so which settings the product uses now depends on which copy it happens to read first.",
                "Identify entries that carry no name or display text by the members that make them the same entry.",
                [$"The tenant holds {landed.Length} sets of flow settings for 'SupportCase'."]));

        Assert.Equal("The source tenant's copy.", landed[0].SubjectGoal);
    }

    private static async Task<bool> CreateFlowAsync(IServiceProvider serviceProvider, string goal)
    {
        var manager = serviceProvider.GetRequiredService<ICatalogManager<SubjectFlowSettings>>();
        var flow = await manager.NewAsync(new JsonObject());

        // The display text is left unset on purpose: no screen in the product fills it in, so a gate that set it
        // would be proving something about the fixture rather than about the configuration the product produces.
        flow.SubjectContentType = "SupportCase";
        flow.SubjectGoal = goal;
        await manager.CreateAsync(flow);

        return true;
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

    private static async Task<string> ReadDescriptionAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant)    {
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
    [InlineData(ContactCenterConfigurationCatalogs.Group)]
    [InlineData(OmnichannelConfigurationCatalogs.Group)]
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

    [Fact]
    public async Task AnExportedPlan_OrdersEveryCatalogAfterTheCatalogsItReferences()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-export-order",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        await SeedAsync(host, tenant, ContactCenterConfigurationCatalogs.Group);
        await SeedAsync(host, tenant, OmnichannelConfigurationCatalogs.Group);

        var plan = await ExportAsync(host, tenant, ContactCenterConfigurationCatalogs.Group);
        var order = plan.Select(step => step["name"].GetValue<string>()).ToArray();

        AssertPrecedes(order, ContactCenterConfigurationCatalogs.Skill, ContactCenterConfigurationCatalogs.Queue);
        AssertPrecedes(order, ContactCenterConfigurationCatalogs.QueueGroup, ContactCenterConfigurationCatalogs.Queue);
        AssertPrecedes(order, ContactCenterConfigurationCatalogs.BusinessHoursCalendar, ContactCenterConfigurationCatalogs.Queue);
        AssertPrecedes(order, ContactCenterConfigurationCatalogs.Queue, ContactCenterConfigurationCatalogs.EntryPoint);

        var omnichannelOrder = (await ExportAsync(host, tenant, OmnichannelConfigurationCatalogs.Group))
            .Select(step => step["name"].GetValue<string>())
            .ToArray();

        AssertPrecedes(omnichannelOrder, OmnichannelConfigurationCatalogs.CampaignGroup, OmnichannelConfigurationCatalogs.Campaign);
        AssertPrecedes(omnichannelOrder, OmnichannelConfigurationCatalogs.Disposition, OmnichannelConfigurationCatalogs.SubjectFlowSettings);
        AssertPrecedes(omnichannelOrder, OmnichannelConfigurationCatalogs.Disposition, OmnichannelConfigurationCatalogs.SubjectAction);
    }

    private static void AssertPrecedes(string[] order, string first, string second)
    {
        var firstIndex = Array.IndexOf(order, first);
        var secondIndex = Array.IndexOf(order, second);

        Assert.True(firstIndex >= 0, $"The plan does not export '{first}'.");
        Assert.True(secondIndex >= 0, $"The plan does not export '{second}'.");
        Assert.True(
            firstIndex < secondIndex,
            $"'{first}' must be imported before '{second}' because '{second}' references it, but the plan orders " +
            $"'{first}' at {firstIndex} and '{second}' at {secondIndex}.");
    }

    private static async Task<int> SeedAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant, string group)
    {
        return await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var catalogs = GetCatalogs(serviceProvider, group);
            var handlers = serviceProvider.GetServices<IRecipeStepHandler>().ToArray();
            var assigned = 0;

            foreach (var (stepName, entryType, _) in GetGroup(group).Catalogs)
            {
                var catalog = catalogs.Single(candidate => candidate.StepName == stepName);
                var (entry, populated) = BuildFullyPopulated(entryType, stepName);

                assigned += populated;

                var context = new RecipeExecutionContext
                {
                    Name = stepName,
                    Step = new JsonObject
                    {
                        ["name"] = stepName,
                        [catalog.CollectionName] = new JsonArray(entry),
                    },
                };

                foreach (var handler in handlers)
                {
                    await handler.ExecuteAsync(context);
                }

                Assert.True(
                    context.Errors.Count == 0,
                    $"{stepName}: {string.Join("; ", context.Errors)} :: {entry.ToJsonString()}");
            }

            return assigned;
        });
    }

    private static async Task<JsonObject[]> ExportAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant, string group)
    {
        return await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var step = GetGroup(group).CreateStep();
            var result = new DeploymentPlanResult(new NullFileBuilder(), new RecipeDescriptor());

            // Every source is offered the step because a source only contributes to the step type it declares. A
            // source that ignores it contributes nothing, which the step-count assertions would catch.
            foreach (var source in serviceProvider.GetServices<IDeploymentSource>())
            {
                await source.ProcessDeploymentStepAsync(step, result);
            }

            return result.Steps.Cast<JsonObject>().ToArray();
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

            foreach (var (stepName, _, managerType) in GetGroup(group).Catalogs)
            {
                var entries = new JsonArray();

                foreach (var entry in await GetAllAsync(serviceProvider, managerType))
                {
                    entries.Add(JsonSerializer.SerializeToNode(entry, entry.GetType(), JOptions.Default));
                }

                steps.Add(new JsonObject
                {
                    ["name"] = stepName,
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

    private static IConfigurationCatalog[] GetCatalogs(IServiceProvider serviceProvider, string group)
    {
        return serviceProvider
            .GetServices<IConfigurationCatalog>()
            .Where(catalog => catalog.Group == group)
            .ToArray();
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
