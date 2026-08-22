using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentEntitlementUtilitiesTests
{
    [Fact]
    public void FilterEntitled_WhenRequestedIdIsNotAllowed_ExcludesIt()
    {
        // Act
        var result = AgentEntitlementUtilities.FilterEntitled(["q1", "q2"], ["q1"]);

        // Assert
        Assert.Equal(["q1"], result);
    }

    [Fact]
    public void FilterEntitled_ComparesIdsCaseInsensitively()
    {
        // Act
        var result = AgentEntitlementUtilities.FilterEntitled(["Q1"], ["q1"]);

        // Assert
        Assert.Equal(["Q1"], result);
    }

    [Fact]
    public void FilterEntitled_WhenAllowedIsEmpty_ReturnsEmpty()
    {
        // Act
        var result = AgentEntitlementUtilities.FilterEntitled(["q1"], []);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void FilterEntitled_WhenAllowedIsNull_FailsClosedAndReturnsEmpty()
    {
        // Act
        var result = AgentEntitlementUtilities.FilterEntitled(["q1"], null);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeIds_RemovesBlanksAndDuplicatesCaseInsensitively()
    {
        // Act
        var result = AgentEntitlementUtilities.NormalizeIds(["q1", " q1 ", "Q1", "", null, "q2"]);

        // Assert
        Assert.Equal(["q1", "q2"], result);
    }

    [Fact]
    public void HasQueueEntitlement_WhenLiveMembershipHasNoMatchingEntitlement_ReturnsFalse()
    {
        // Arrange
        var profile = new AgentProfile
        {
            QueueIds = ["q1"],
            AllowedQueueIds = ["q2"],
        };

        // Act & Assert
        Assert.False(AgentEntitlementUtilities.HasQueueEntitlement(profile, "q1"));
    }

    [Fact]
    public void HasQueueEntitlement_WhenLiveMembershipMatchesEntitlement_ReturnsTrue()
    {
        // Arrange
        var profile = new AgentProfile
        {
            QueueIds = ["q1"],
            AllowedQueueIds = ["Q1"],
        };

        // Act & Assert
        Assert.True(AgentEntitlementUtilities.HasQueueEntitlement(profile, "q1"));
    }

    [Fact]
    public void HasQueueEntitlement_WhenProfileHasNoEntitlements_FailsClosed()
    {
        // Arrange
        var profile = new AgentProfile
        {
            QueueIds = ["q1"],
            AllowedQueueIds = [],
        };

        // Act & Assert
        Assert.False(AgentEntitlementUtilities.HasQueueEntitlement(profile, "q1"));
    }

    [Fact]
    public void HasCampaignEntitlement_WhenProfileHasNoEntitlements_FailsClosed()
    {
        // Arrange
        var profile = new AgentProfile
        {
            CampaignIds = ["c1"],
            AllowedCampaignIds = [],
        };

        // Act & Assert
        Assert.False(AgentEntitlementUtilities.HasCampaignEntitlement(profile, "c1"));
    }

    [Fact]
    public void HasCampaignEntitlement_WhenLiveMembershipMatchesEntitlement_ReturnsTrue()
    {
        // Arrange
        var profile = new AgentProfile
        {
            CampaignIds = ["c1"],
            AllowedCampaignIds = ["C1"],
        };

        // Act & Assert
        Assert.True(AgentEntitlementUtilities.HasCampaignEntitlement(profile, "c1"));
    }
}
