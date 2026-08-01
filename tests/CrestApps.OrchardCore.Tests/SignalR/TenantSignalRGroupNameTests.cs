using CrestApps.OrchardCore.SignalR;

namespace CrestApps.OrchardCore.Tests.SignalR;

public sealed class TenantSignalRGroupNameTests
{
    [Fact]
    public void ForUser_SameUserInDifferentTenants_ReturnsDifferentGroups()
    {
        // Act
        var tenantAGroup = TenantSignalRGroupName.ForUser("TenantA", "user-1");
        var tenantBGroup = TenantSignalRGroupName.ForUser("TenantB", "user-1");

        // Assert
        Assert.NotEqual(tenantAGroup, tenantBGroup);
    }

    [Fact]
    public void ForGroup_SameLogicalGroupInDifferentTenants_ReturnsDifferentGroups()
    {
        // Act
        var tenantAGroup = TenantSignalRGroupName.ForGroup("TenantA", "cc:supervisors");
        var tenantBGroup = TenantSignalRGroupName.ForGroup("TenantB", "cc:supervisors");

        // Assert
        Assert.NotEqual(tenantAGroup, tenantBGroup);
    }
}
