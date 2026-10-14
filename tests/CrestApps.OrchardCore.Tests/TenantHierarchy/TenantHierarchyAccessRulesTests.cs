using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class TenantHierarchyAccessRulesTests
{
    private readonly ShellSettings _default = TestShellSettings.Default();
    private readonly ShellSettings _plain = TestShellSettings.Ordinary("plain");
    private readonly ShellSettings _firmA = TestShellSettings.Parent("firma");
    private readonly ShellSettings _firmB = TestShellSettings.Parent("firmb");
    private readonly ShellSettings _childA1;
    private readonly ShellSettings _childA2;
    private readonly ShellSettings _childB1;

    public TenantHierarchyAccessRulesTests()
    {
        _childA1 = TestShellSettings.Child("a1", _firmA);
        _childA2 = TestShellSettings.Child("a2", _firmA);
        _childB1 = TestShellSettings.Child("b1", _firmB);
    }

    [Fact]
    public void CanReach_OutsideAnyTenant_ReachesEveryTenant()
    {
        // Assert
        Assert.True(TenantHierarchyAccessRules.CanReach(null, _childA1, null, isRead: false));
        Assert.True(TenantHierarchyAccessRules.CanReach(null, _firmB, null, isRead: false));
    }

    [Fact]
    public void CanReach_FromDefault_ReachesEveryTenant()
    {
        // Assert
        Assert.True(TenantHierarchyAccessRules.CanReach(_default, _childA1, null, isRead: false));
        Assert.True(TenantHierarchyAccessRules.CanReach(_default, _firmB, null, isRead: false));
        Assert.True(TenantHierarchyAccessRules.CanReach(_default, _plain, null, isRead: false));
    }

    [Fact]
    public void CanReach_AnyTenant_ReachesItselfAndDefault()
    {
        // Assert
        foreach (var current in new[] { _plain, _firmA, _childA1 })
        {
            Assert.True(TenantHierarchyAccessRules.CanReach(current, current, null, isRead: false));
            Assert.True(TenantHierarchyAccessRules.CanReach(current, _default, null, isRead: false));
        }
    }

    [Fact]
    public void CanReach_FromOrdinaryTenant_ReachesOrdinaryTenantsOnly()
    {
        // Arrange
        var other = TestShellSettings.Ordinary("other");

        // Assert
        Assert.True(TenantHierarchyAccessRules.CanReach(_plain, other, null, isRead: false));
        Assert.False(TenantHierarchyAccessRules.CanReach(_plain, _firmA, null, isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(_plain, _childA1, null, isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(_plain, _childA1, "a1", isRead: false));
    }

    [Fact]
    public void CanReach_FromParent_ReadsItsOwnChildren()
    {
        // Assert
        Assert.True(TenantHierarchyAccessRules.CanReach(_firmA, _childA1, null, isRead: true));
        Assert.True(TenantHierarchyAccessRules.CanReach(_firmA, _childA2, null, isRead: true));
    }

    [Fact]
    public void CanReach_FromParent_ChangesItsOwnChildOnlyInsideABrokerCallForThatChild()
    {
        // Assert
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, _childA1, null, isRead: false));
        Assert.True(TenantHierarchyAccessRules.CanReach(_firmA, _childA1, "a1", isRead: false));
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, _childA1, "a2", isRead: false));
    }

    [Fact]
    public void CanReach_FromParent_NeverReachesAnotherHierarchy()
    {
        // Assert
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, _firmB, null, isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, _firmB, "firmb", isRead: false));
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, _childB1, null, isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, _childB1, "b1", isRead: false));
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, _plain, null, isRead: true));
    }

    [Fact]
    public void CanReach_FromChild_ReachesItsParentOnlyInsideABrokerCallForThatParent()
    {
        // Assert
        Assert.False(TenantHierarchyAccessRules.CanReach(_childA1, _firmA, null, isRead: true));
        Assert.True(TenantHierarchyAccessRules.CanReach(_childA1, _firmA, "firma", isRead: true));
        Assert.True(TenantHierarchyAccessRules.CanReach(_childA1, _firmA, "firma", isRead: false));
    }

    [Fact]
    public void CanReach_FromChild_NeverReachesSiblingsOrOtherTenants()
    {
        // Assert
        Assert.False(TenantHierarchyAccessRules.CanReach(_childA1, _childA2, null, isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(_childA1, _childA2, "a2", isRead: false));
        Assert.False(TenantHierarchyAccessRules.CanReach(_childA1, _firmB, "firmb", isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(_childA1, _childB1, "b1", isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(_childA1, _plain, null, isRead: true));
    }

    [Fact]
    public void CanReach_ChildThatClaimsAParentWithoutATenantId_IsRefused()
    {
        // Arrange
        var parentWithoutId = new ShellSettings { Name = "noid" };
        parentWithoutId[TenantHierarchyConstants.SettingsKeys.Role] = "Parent";

        var child = TestShellSettings.Child("orphan", _firmA);
        child[TenantHierarchyConstants.SettingsKeys.ParentTenantId] = null;

        // Assert
        Assert.False(TenantHierarchyAccessRules.CanReach(child, parentWithoutId, "noid", isRead: true));
    }

    [Fact]
    public void IsListed_InParent_ListsTheParentAndItsChildrenOnly()
    {
        // Arrange
        var all = new[] { _default, _plain, _firmA, _firmB, _childA1, _childA2, _childB1 };

        // Act
        var listed = all.Where(target => TenantHierarchyAccessRules.IsListed(_firmA, target, null)).Select(settings => settings.Name);

        // Assert
        Assert.Equal(["firma", "a1", "a2"], listed);
    }

    [Fact]
    public void IsListed_InChild_ListsTheChildOnly()
    {
        // Arrange
        var all = new[] { _default, _plain, _firmA, _firmB, _childA1, _childA2, _childB1 };

        // Act
        var listed = all.Where(target => TenantHierarchyAccessRules.IsListed(_childA1, target, null)).Select(settings => settings.Name);

        // Assert
        Assert.Equal(["a1"], listed);
    }

    [Fact]
    public void IsListed_InOrdinaryTenant_HidesTheHierarchies()
    {
        // Arrange
        var all = new[] { _default, _plain, _firmA, _firmB, _childA1, _childA2, _childB1 };

        // Act
        var listed = all.Where(target => TenantHierarchyAccessRules.IsListed(_plain, target, null)).Select(settings => settings.Name);

        // Assert
        Assert.Equal(["Default", "plain"], listed);
    }

    [Fact]
    public void IsListed_InDefault_ListsEveryTenant()
    {
        // Arrange
        var all = new[] { _default, _plain, _firmA, _firmB, _childA1, _childA2, _childB1 };

        // Act
        var listed = all.Count(target => TenantHierarchyAccessRules.IsListed(_default, target, null));

        // Assert
        Assert.Equal(all.Length, listed);
    }
}
