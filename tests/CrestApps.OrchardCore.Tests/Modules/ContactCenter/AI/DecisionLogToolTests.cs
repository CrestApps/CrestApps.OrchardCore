using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AI;

/// <summary>
/// Regression for the drain: 103 hand-created preview activities had an empty channel, so the dialer suppressed every one as
/// NoDestination within four seconds, failed them for good, and the preview queue was empty.
/// </summary>
public sealed class DecisionLogToolTests
{
    private const string CampaignId = "leadgen";

    private static readonly string _campaignQueueId = ContactCenterConstants.CampaignQueue.CreateId(CampaignId);
    private static readonly DateTime _start = ContactCenterToolHost.Now.AddHours(-3);

    [Fact]
    public async Task InvokeAsync_WhenManyRecordsWereSuppressedAsNoDestinationInSeconds_CallsOutTheDrain()
    {
        // Arrange
        var host = HostFor([.. Suppressions(count: 103, spread: TimeSpan.FromSeconds(4), DialerSuppressionReason.NoDestination)]);

        // Act
        var result = await host.RunAsync(new DecisionLogTool());

        // Assert
        Assert.Equal("Drain.NoDestination", ContactCenterToolHost.FindingCodes(result)[0]);

        var drain = result.GetProperty("findings")[0];
        Assert.Equal("certain", drain.GetProperty("likelihood").GetString());
        Assert.Equal(103, drain.GetProperty("facts").GetProperty("activities").GetInt32());
        Assert.Equal(4, drain.GetProperty("facts").GetProperty("seconds").GetInt32());
        Assert.Contains("Lead Generation", drain.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Contains("fails a record with no number for good", drain.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Contains("drain", result.GetProperty("verdict").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(103, result.GetProperty("byReason").GetProperty("items")[0].GetProperty("decisions").GetInt32());
        Assert.Equal("NoDestination", result.GetProperty("byReason").GetProperty("items")[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenTheSameSuppressionsAreSpreadOverHours_DoesNotCallItADrain()
    {
        // Arrange: one a minute for 103 minutes is a trickle, not a burst.
        var host = HostFor([.. Suppressions(count: 103, spread: TimeSpan.FromMinutes(102), DialerSuppressionReason.NoDestination)]);

        // Act
        var result = await host.RunAsync(new DecisionLogTool());

        // Assert
        Assert.DoesNotContain("Drain.NoDestination", ContactCenterToolHost.FindingCodes(result));
        Assert.Contains("Suppressed.NoDestination", ContactCenterToolHost.FindingCodes(result));
    }

    [Fact]
    public async Task InvokeAsync_WhenFilteredByReasonAndActivity_ReturnsOnlyThoseDecisions()
    {
        // Arrange
        var entries = Suppressions(count: 20, spread: TimeSpan.FromSeconds(2), DialerSuppressionReason.NoDestination)
            .Concat(Suppressions(count: 5, spread: TimeSpan.FromSeconds(2), DialerSuppressionReason.RetryCoolDown, prefix: "cool"))
            .ToArray();
        var host = HostFor(entries);

        // Act
        var result = await host.RunAsync(new DecisionLogTool(), ("reason", "RetryCoolDown"));
        var single = await HostFor(entries).RunAsync(new DecisionLogTool(), ("activityId", "act-3"));

        // Assert
        Assert.Equal(5, result.GetProperty("matchingDecisions").GetInt32());
        Assert.Equal(1, single.GetProperty("matchingDecisions").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_WhenTheSupervisorDoesNotOverseeTheCampaign_HidesItsDecisionsAndSaysHowManyWereHidden()
    {
        // Arrange
        var host = HostFor([.. Suppressions(count: 12, spread: TimeSpan.FromSeconds(2), DialerSuppressionReason.NoDestination)]);
        host.DoesNotOversee(_campaignQueueId);

        // Act
        var result = await host.RunAsync(new DecisionLogTool());

        // Assert
        Assert.Equal(0, result.GetProperty("matchingDecisions").GetInt32());
        Assert.Equal(12, result.GetProperty("hiddenBecauseYouDoNotOverseeTheQueue").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_WhenTheLogIsEmpty_SaysNothingWasRecordedInsteadOfGuessing()
    {
        // Arrange
        var host = HostFor([]);

        // Act
        var result = await host.RunAsync(new DecisionLogTool());

        // Assert
        Assert.Contains("No dialer decisions match", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
    }

    private static ContactCenterToolHost HostFor(DialerDecisionIndex[] entries)
        => new ContactCenterToolHost()
            .WithQueues()
            .WithCampaigns((CampaignId, "Lead Generation"))
            .WithAgents()
            .WithDecisionLog(entries);

    private static IEnumerable<DialerDecisionIndex> Suppressions(int count, TimeSpan spread, DialerSuppressionReason reason, string prefix = "act")
    {
        for (var i = 0; i < count; i++)
        {
            var at = _start + (spread * i / Math.Max(1, count - 1));

            yield return new DialerDecisionIndex
            {
                EventType = ContactCenterConstants.Events.DialSuppressed,
                Reason = reason.ToString(),
                CampaignId = CampaignId,
                QueueId = _campaignQueueId,
                ActivityItemId = $"{prefix}-{i}",
                OccurrenceCount = 1,
                OccurredUtc = at,
                LastSeenUtc = at,
            };
        }
    }
}
