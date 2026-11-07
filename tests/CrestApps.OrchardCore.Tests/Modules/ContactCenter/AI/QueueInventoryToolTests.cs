using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AI;

/// <summary>
/// Regression for the campaign that had no queue at all: its pending records were assigned to users for manual work or in no
/// queue, so no agent could ever be offered one, and nothing said so.
/// </summary>
public sealed class QueueInventoryToolTests
{
    private const string CampaignId = "leadgen";

    private static readonly string _campaignQueueId = ContactCenterConstants.CampaignQueue.CreateId(CampaignId);

    [Fact]
    public async Task InvokeAsync_WhenACampaignHasNoQueueAndItsRecordsAreAssignedForManualWork_SaysNoQueueHoldsThem()
    {
        // Arrange: 120 pending records, none in a queue; 87 are on users' own lists and 33 are in no queue at all.
        var summary = new DialerCampaignDispatchSummary
        {
            CampaignId = CampaignId,
            PendingCount = 120,
            HeldBackCount = 120,
            AssignedToUserCount = 87,
            NotInAnyQueueCount = 33,
            Buckets =
            [
                new DispatchBucket { Blocker = DispatchBlocker.AssignedToUser, Count = 87 },
                new DispatchBucket { Blocker = DispatchBlocker.NotQueued, Count = 33 },
            ],
        };
        var host = HostFor(summary, [new DialerQueueInventoryRow { CampaignId = CampaignId, PendingCount = 120, NotInAnyQueueCount = 120 }]);

        // Act
        var result = await host.RunAsync(new QueueInventoryTool());

        // Assert
        var campaign = result.GetProperty("campaigns").GetProperty("items")[0];
        Assert.Equal(120, campaign.GetProperty("pending").GetInt32());
        Assert.Equal(87, campaign.GetProperty("assignedToUserForManualWork").GetInt32());
        Assert.Equal(33, campaign.GetProperty("notInAnyQueue").GetInt32());
        Assert.Equal(0, campaign.GetProperty("dialableNow").GetInt32());
        Assert.Equal(0, campaign.GetProperty("queue").GetProperty("waiting").GetInt32());
        Assert.Equal("NoQueue", ContactCenterToolHost.FindingCodes(campaign)[0]);
        Assert.Contains("no queue holds any of them", campaign.GetProperty("verdict").GetString(), StringComparison.Ordinal);
        Assert.Contains("1 of them have a problem", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenACampaignHasNothingPending_ListsItByNameSoItIsNotMistakenForMissing()
    {
        // Arrange
        var host = HostFor(null, []);

        // Act
        var result = await host.RunAsync(new QueueInventoryTool());

        // Assert
        Assert.Equal("Lead Generation", result.GetProperty("campaignsWithNothingPending").GetProperty("items")[0].GetString());
        Assert.Contains("Nothing is pending", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheSupervisorDoesNotOverseeTheCampaign_RefusesToShowIt()
    {
        // Arrange
        var host = HostFor(new DialerCampaignDispatchSummary { CampaignId = CampaignId, PendingCount = 5 }, []);
        host.DoesNotOversee(_campaignQueueId);

        // Act
        var result = await host.RunAsync(new QueueInventoryTool());

        // Assert
        Assert.Contains("don't oversee", result.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_WhenCampaignHasAQueueWithDialableWorkAndAgentsAvailable_FindsNothingWrong()
    {
        // Arrange
        var summary = new DialerCampaignDispatchSummary { CampaignId = CampaignId, PendingCount = 10, DialableNowCount = 10 };
        var rows = new[] { new DialerQueueInventoryRow { CampaignId = CampaignId, QueueId = _campaignQueueId, WaitingCount = 10, PendingCount = 10, DialableNowCount = 10 } };

        // Act
        var analysis = CampaignInventoryAnalyzer.Analyze(new CampaignInventoryFacts(CampaignId, "Lead Generation", summary, rows, 3, 2));

        // Assert
        Assert.DoesNotContain(analysis.Findings, finding => finding.Likelihood is Likelihood.Certain or Likelihood.Likely);
        Assert.Equal("Dialable", analysis.Findings[0].Code);
    }

    [Fact]
    public void Analyze_WhenWorkIsDialableButNobodyIsSignedIn_SaysSo()
    {
        // Arrange
        var summary = new DialerCampaignDispatchSummary { CampaignId = CampaignId, PendingCount = 10, DialableNowCount = 10 };
        var rows = new[] { new DialerQueueInventoryRow { CampaignId = CampaignId, QueueId = _campaignQueueId, WaitingCount = 10, PendingCount = 10, DialableNowCount = 10 } };

        // Act
        var analysis = CampaignInventoryAnalyzer.Analyze(new CampaignInventoryFacts(CampaignId, "Lead Generation", summary, rows, 0, 0));

        // Assert
        Assert.Equal("NobodySignedIn", analysis.Findings[0].Code);
        Assert.Equal(Likelihood.Certain, analysis.Findings[0].Likelihood);
    }

    private static ContactCenterToolHost HostFor(DialerCampaignDispatchSummary summary, IList<DialerQueueInventoryRow> rows)
    {
        var host = new ContactCenterToolHost()
            .WithQueues()
            .WithCampaigns((CampaignId, "Lead Generation"))
            .WithAgents();

        var inventory = new Mock<IDialerQueueInventoryService>();
        inventory
            .Setup(service => service.GetInventoryAsync(It.IsAny<DialerQueueInventoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialerQueueInventory
            {
                GeneratedUtc = ContactCenterToolHost.Now,
                Campaigns = summary is null ? [] : [summary],
                Rows = rows,
            });
        host.Services.AddSingleton(inventory.Object);
        host.Services.AddSingleton(new Mock<CrestApps.OrchardCore.ContactCenter.Core.Services.IQueueItemManager>().Object);

        return host;
    }
}
