using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Indexes;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public class AgentAllowedQueueIndexProviderTests
{
    [Fact]
    public void BuildRows_MapsUnionOfAllowedAndSignedInQueues_LowercasedAndDistinct()
    {
        var profile = new AgentProfile { ItemId = "a1" };
        profile.AllowedQueueIds.Add("Q1");
        profile.AllowedQueueIds.Add("q2");
        profile.QueueIds.Add("q2"); // duplicate across the two lists
        profile.QueueIds.Add("Q3");

        var rows = AgentAllowedQueueIndexProvider.BuildRows(profile).ToArray();

        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.Equal("a1", row.ItemId));
        Assert.Contains(rows, row => row.QueueId == "q1");
        Assert.Contains(rows, row => row.QueueId == "q2");
        Assert.Contains(rows, row => row.QueueId == "q3");
    }

    [Fact]
    public void BuildRows_WithNoQueues_ReturnsEmpty()
    {
        var profile = new AgentProfile { ItemId = "a1" };

        Assert.Empty(AgentAllowedQueueIndexProvider.BuildRows(profile));
    }
}
