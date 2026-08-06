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

    [Fact]
    public void ForUser_SameTenantAndUser_IsDeterministic()
    {
        // A publisher and the hub that joins the connection must derive the identical name.

        // Act
        var first = TenantSignalRGroupName.ForUser("TenantA", "user-1");
        var second = TenantSignalRGroupName.ForUser("TenantA", "user-1");

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void UserAndGroup_WithMatchingIdentifiers_NeverCollide()
    {
        // A user id and a logical group name that happen to be equal must remain distinct
        // destinations so a broadcast to one is never delivered to the other.

        // Act
        var userDestination = TenantSignalRGroupName.ForUser("TenantA", "shared");
        var groupDestination = TenantSignalRGroupName.ForGroup("TenantA", "shared");

        // Assert
        Assert.NotEqual(userDestination, groupDestination);
    }

    [Fact]
    public void ForUser_TenantAndUserBoundaryIsUnambiguous()
    {
        // Without length prefixing, tenant "ab" + user "c" and tenant "a" + user "bc" could
        // encode to the same string and leak across tenants. The length markers prevent that.

        // Act
        var first = TenantSignalRGroupName.ForUser("ab", "c");
        var second = TenantSignalRGroupName.ForUser("a", "bc");

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ForGroup_TenantAndGroupBoundaryIsUnambiguous()
    {
        // Act
        var first = TenantSignalRGroupName.ForGroup("ab", "c");
        var second = TenantSignalRGroupName.ForGroup("a", "bc");

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ForGroup_GroupNameCarryingDelimiters_CannotSpoofAnotherTenant()
    {
        // A crafted group name that embeds the internal delimiter format must not be able to
        // impersonate a destination in another tenant.

        // Act
        var crafted = TenantSignalRGroupName.ForGroup("TenantA", "tenant:7:TenantB:group:5:admin");
        var legitimate = TenantSignalRGroupName.ForGroup("TenantB", "admin");

        // Assert
        Assert.NotEqual(crafted, legitimate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ForUser_InvalidTenantName_Throws(string tenantName)
    {
        Assert.ThrowsAny<ArgumentException>(() => TenantSignalRGroupName.ForUser(tenantName, "user-1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ForUser_InvalidUserId_Throws(string userId)
    {
        Assert.ThrowsAny<ArgumentException>(() => TenantSignalRGroupName.ForUser("TenantA", userId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ForGroup_InvalidTenantName_Throws(string tenantName)
    {
        Assert.ThrowsAny<ArgumentException>(() => TenantSignalRGroupName.ForGroup(tenantName, "supervisors"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ForGroup_InvalidGroupName_Throws(string groupName)
    {
        Assert.ThrowsAny<ArgumentException>(() => TenantSignalRGroupName.ForGroup("TenantA", groupName));
    }
}
