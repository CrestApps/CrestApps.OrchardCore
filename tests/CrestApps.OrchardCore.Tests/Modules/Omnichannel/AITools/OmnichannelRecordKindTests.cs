using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements;
using CrestApps.OrchardCore.Omnichannel.Managements.AI;
using CrestApps.OrchardCore.Tests.Agent.Records;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;

/// <summary>
/// The omnichannel setup records are record kinds of the generic record tools, registered by the feature that owns them
/// with the permission its admin page checks, and hold the rules those pages add around their editors: a unique name,
/// the lead status flags moved after a save, the converted status kept, and no delete where the page has none. Cadences
/// and addresses are read-only, and their fields carry the argument names of the tools that write them.
/// </summary>
public sealed class OmnichannelRecordKindTests
{
    public static TheoryData<Type, string, bool, string> Kinds
        => new()
        {
            { typeof(AIToolsStartup), OmnichannelRecordKinds.CampaignKind, true, OmnichannelConstants.Permissions.ManageCampaigns.Name },
            { typeof(AIToolsStartup), OmnichannelRecordKinds.CampaignGroupKind, true, OmnichannelConstants.Permissions.ManageCampaignGroups.Name },
            { typeof(AIToolsStartup), OmnichannelRecordKinds.DispositionKind, true, OmnichannelConstants.Permissions.ManageDispositions.Name },
            { typeof(AIToolsStartup), OmnichannelRecordKinds.CadenceKind, false, OmnichannelConstants.Permissions.ManageCadences.Name },
            { typeof(ChannelEndpointsAIToolsStartup), OmnichannelRecordKinds.AddressKind, false, OmnichannelConstants.Permissions.ManageChannelEndpoints.Name },
            { typeof(CrmAIToolsStartup), OmnichannelRecordKinds.LeadStatusKind, true, OmnichannelConstants.Permissions.ManageLeadStatuses.Name },
            { typeof(CrmAIToolsStartup), OmnichannelRecordKinds.OpportunityStageKind, true, OmnichannelConstants.Permissions.ManageOpportunityStages.Name },
        };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Kind_IsRegisteredByItsFeature_WithThePagesPermission(Type startup, string name, bool canWrite, string permission)
    {
        // Act
        var kind = GetKinds(startup)[name];

        // Assert
        Assert.Equal(canWrite, kind.CanWrite);
        Assert.Equal(permission, kind.Permission.Name);
        Assert.Equal(permission, kind.EffectiveReadPermission.Name);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
        Assert.NotEqual(kind.Name, kind.Title);
    }

    public static TheoryData<Type, string, string, string> ReadOnlyKinds
        => new()
        {
            { typeof(AIToolsStartup), OmnichannelRecordKinds.CadenceKind, "saveOmnichannelCadence", "deleteOmnichannelRecord" },
            { typeof(ChannelEndpointsAIToolsStartup), OmnichannelRecordKinds.AddressKind, "saveOmnichannelAddress", null },
        };

    [Theory]
    [MemberData(nameof(ReadOnlyKinds))]
    public void ReadOnlyKind_NamesTheToolsThatSaveAndDeleteIt(Type startup, string name, string saveTool, string deleteTool)
    {
        // Act
        var kind = GetKinds(startup)[name];

        // Assert
        Assert.False(kind.CanWrite);
        Assert.Equal(saveTool, kind.SaveToolName);
        Assert.Equal(deleteTool, kind.DeleteToolName);
        Assert.DoesNotContain(saveTool, kind.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Startups_RegisterSevenKinds()
    {
        // Act
        var names = new[] { typeof(AIToolsStartup), typeof(ChannelEndpointsAIToolsStartup), typeof(CrmAIToolsStartup), typeof(ManagementAIToolsStartup) }
            .SelectMany(startup => GetKinds(startup).Keys)
            .Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(
            new[] { "leadStatus", "omnichannelAddress", "omnichannelCadence", "omnichannelCampaign", "omnichannelCampaignGroup", "omnichannelDisposition", "opportunityStage" },
            names);
    }

    [Fact]
    public async Task CampaignKinds_AreNeverDeleted_AsTheirPagesHaveNoDelete()
    {
        // Arrange
        var harness = new OmnichannelToolHarness();
        var services = harness.Build();
        var campaigns = Kind<OmnichannelCampaign>(typeof(AIToolsStartup), OmnichannelRecordKinds.CampaignKind);
        var groups = Kind<OmnichannelCampaignGroup>(typeof(AIToolsStartup), OmnichannelRecordKinds.CampaignGroupKind);

        // Act
        var campaignUse = await campaigns.InUseAsync(services, new OmnichannelCampaign { ItemId = "campaign-1" }, CancellationToken.None);
        var groupUse = await groups.InUseAsync(services, new OmnichannelCampaignGroup { ItemId = "group-1" }, CancellationToken.None);

        // Assert
        Assert.Contains("cannot be deleted", campaignUse);
        Assert.Contains("cannot be deleted", groupUse);
    }

    [Fact]
    public async Task CampaignKind_RefusesAGroupThatDoesNotExist_AndKeepsTheGroupItHad()
    {
        // Arrange
        var harness = new OmnichannelToolHarness();
        harness.Campaigns.Add(new OmnichannelCampaign { ItemId = "campaign-1", DisplayText = "Renewals", CampaignGroupId = "retired" });
        var services = harness.Build();
        var kind = Kind<OmnichannelCampaign>(typeof(AIToolsStartup), OmnichannelRecordKinds.CampaignKind);

        // Act
        var refused = await kind.ValidateAsync(services, new OmnichannelCampaign { ItemId = "campaign-1", DisplayText = "Renewals", CampaignGroupId = "missing" }, CancellationToken.None);
        var kept = await kind.ValidateAsync(services, new OmnichannelCampaign { ItemId = "campaign-1", DisplayText = "Renewals 2026", CampaignGroupId = "retired" }, CancellationToken.None);

        // Assert
        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Errors, error => error.MemberNames.Contains(nameof(OmnichannelCampaign.CampaignGroupId)));
        Assert.True(kept.Succeeded);
    }

    [Fact]
    public async Task DispositionKind_RefusesASecondDispositionWithTheSameName()
    {
        // Arrange
        var harness = new OmnichannelToolHarness();
        harness.Dispositions.Add(new OmnichannelDisposition { ItemId = "d-1", Name = "Busy" });
        var services = harness.Build();
        var kind = Kind<OmnichannelDisposition>(typeof(AIToolsStartup), OmnichannelRecordKinds.DispositionKind);

        // Act
        var duplicate = await kind.ValidateAsync(services, new OmnichannelDisposition { ItemId = "d-2", Name = "busy" }, CancellationToken.None);
        var itself = await kind.ValidateAsync(services, new OmnichannelDisposition { ItemId = "d-1", Name = "Busy", Description = "Line busy" }, CancellationToken.None);

        // Assert
        Assert.False(duplicate.Succeeded);
        Assert.Equal("A disposition with the same name already exists.", Assert.Single(duplicate.Errors).ErrorMessage);
        Assert.True(itself.Succeeded);
    }

    [Fact]
    public async Task OpportunityStageKind_RefusesASecondStageWithTheSameName()
    {
        // Arrange
        var harness = new OmnichannelToolHarness();
        harness.OpportunityStages.Add(new OpportunityStage { ItemId = "s-1", Name = "Proposal" });
        var kind = Kind<OpportunityStage>(typeof(CrmAIToolsStartup), OmnichannelRecordKinds.OpportunityStageKind);

        // Act
        var result = await kind.ValidateAsync(harness.Build(), new OpportunityStage { Name = "Proposal" }, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(nameof(OpportunityStage.Name), Assert.Single(result.Errors).MemberNames);
    }

    [Fact]
    public async Task LeadStatusKind_KeepsTheConvertedStatus()
    {
        // Arrange
        var harness = new OmnichannelToolHarness();
        var kind = Kind<LeadStatus>(typeof(CrmAIToolsStartup), OmnichannelRecordKinds.LeadStatusKind);
        var services = harness.Build();

        // Act
        var converted = await kind.InUseAsync(services, new LeadStatus { ItemId = "s-1", Name = "Converted", IsConverted = true }, CancellationToken.None);
        var open = await kind.InUseAsync(services, new LeadStatus { ItemId = "s-2", Name = "New" }, CancellationToken.None);

        // Assert
        Assert.Contains("converted status cannot be deleted", converted);
        Assert.Null(open);
    }

    [Fact]
    public async Task LeadStatusKind_MovesTheConvertedFlagOffTheOtherStatusWhenItSaves()
    {
        // Arrange
        var harness = new ManagementToolHarness();
        var old = harness.LeadStatuses.Add(new LeadStatus { ItemId = "old", Name = "Won", IsConverted = true, IsClosed = true });
        var kind = Kind<LeadStatus>(typeof(CrmAIToolsStartup), OmnichannelRecordKinds.LeadStatusKind);
        var status = new LeadStatus { ItemId = "new", Name = "Converted", IsConverted = true, IsClosed = true };

        // Act
        await kind.SaveAsync(harness.Build(), status, true, CancellationToken.None);

        // Assert
        Assert.Same(status, Assert.Single(harness.LeadStatuses.Created));
        Assert.False(old.IsConverted);
        Assert.True(status.IsConverted);
    }

    [Fact]
    public async Task LeadStatusKind_RefusesASecondStatusWithTheSameName()
    {
        // Arrange
        var harness = new ManagementToolHarness();
        harness.LeadStatuses.Add(new LeadStatus { ItemId = "contacted", Name = "Contacted" });
        var kind = Kind<LeadStatus>(typeof(CrmAIToolsStartup), OmnichannelRecordKinds.LeadStatusKind);

        // Act
        var result = await kind.ValidateAsync(harness.Build(), new LeadStatus { Name = "Contacted" }, CancellationToken.None);

        // Assert
        Assert.Equal("A lead status with the same name already exists.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void CadenceKind_ReturnsTheArgumentNamesOfSaveOmnichannelCadence()
    {
        // Arrange
        var kind = Kind<Cadence>(typeof(AIToolsStartup), OmnichannelRecordKinds.CadenceKind);

        // Act
        var projected = JsonSerializer.SerializeToElement(kind.Projection(new Cadence
        {
            ItemId = "cadence-1",
            DisplayText = "Lead nurture",
            Steps = [new CadenceStep { DelayMinutes = 60, Message = "Hi" }, new CadenceStep { DelayMinutes = 1440, IsAiGenerated = true }],
        }));

        // Assert
        AssertArgumentsOf(new SaveOmnichannelCadenceTool(), projected);
        Assert.Equal(1440, projected.GetProperty("steps")[1].GetProperty("delayMinutes").GetInt32());
        Assert.True(projected.GetProperty("steps")[1].GetProperty("isAiGenerated").GetBoolean());
    }

    [Fact]
    public void AddressKind_ReturnsTheArgumentNamesOfSaveOmnichannelAddress_AndNamesTheFeatureSettingsWithoutTheirValues()
    {
        // Arrange
        var kind = Kind<OmnichannelChannelEndpoint>(typeof(ChannelEndpointsAIToolsStartup), OmnichannelRecordKinds.AddressKind);
        var endpoint = new OmnichannelChannelEndpoint
        {
            ItemId = "line-1",
            DisplayText = "Support line",
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Value = "+15550109000",
            Capabilities = ["Phone", "SMS"],
        };
        endpoint.Put(new ProviderDetails { ConnectionToken = "secret-value" });

        // Act
        var projected = JsonSerializer.SerializeToElement(kind.Projection(endpoint));

        // Assert
        AssertArgumentsOf(new SaveOmnichannelAddressTool(), projected, "featureSettings");
        Assert.Equal(nameof(ProviderDetails), Assert.Single(projected.GetProperty("featureSettings").EnumerateArray()).GetString());
        Assert.DoesNotContain("secret-value", projected.GetRawText());
    }

    // Every field the kind returns is an argument of the tool that writes the record, so what getRecord returns can be
    // given back to that tool; the fields named as read-only are shown but set elsewhere.
    private static void AssertArgumentsOf(AIFunction tool, JsonElement projected, params string[] readOnlyFields)
    {
        var arguments = tool.JsonSchema.GetProperty("properties");

        foreach (var field in projected.EnumerateObject())
        {
            if (readOnlyFields.Contains(field.Name, StringComparer.Ordinal))
            {
                continue;
            }

            Assert.True(arguments.TryGetProperty(field.Name, out _), $"{tool.Name} has no argument named '{field.Name}'.");
        }
    }

    private static AIRecordKind<T> Kind<T>(Type startupType, string name)
        where T : CrestApps.Core.Models.CatalogItem, new()
        => (AIRecordKind<T>)GetKinds(startupType)[name];

    private static IReadOnlyDictionary<string, AIRecordKind> GetKinds(Type startupType)
    {
        var services = new ServiceCollection();
        var localizer = Activator.CreateInstance(typeof(RecordTestLocalizer<>).MakeGenericType(startupType));
        var startup = (StartupBase)Activator.CreateInstance(startupType, localizer);

        startup.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
    }

    private sealed class ProviderDetails
    {
        public string ConnectionToken { get; set; }
    }
}
