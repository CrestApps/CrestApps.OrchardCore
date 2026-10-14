using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

/// <summary>
/// Proves that two hierarchies never reach each other: no tenant of one parent's hierarchy, the parent or any of its
/// children, may read, list or change the other parent or any of its children, in or out of a broker call, whatever
/// its settings claim.
/// </summary>
public sealed class ParentIsolationRulesTests
{
    private static readonly ShellSettings _firmA = TestShellSettings.Parent("firma");
    private static readonly ShellSettings _firmB = TestShellSettings.Parent("firmb");
    private static readonly ShellSettings _childA1 = TestShellSettings.Child("a1", _firmA);
    private static readonly ShellSettings _childA2 = TestShellSettings.Child("a2", _firmA);
    private static readonly ShellSettings _childB1 = TestShellSettings.Child("b1", _firmB);
    private static readonly ShellSettings _childB2 = TestShellSettings.Child("b2", _firmB);

    private static readonly ShellSettings[] _hierarchyA = [_firmA, _childA1, _childA2];
    private static readonly ShellSettings[] _hierarchyB = [_firmB, _childB1, _childB2];

    /// <summary>
    /// Gets every pair of a tenant of one hierarchy and a tenant of the other, in both directions.
    /// </summary>
    public static TheoryData<string, string> CrossHierarchyPairs()
    {
        var data = new TheoryData<string, string>();

        foreach (var (from, to) in new[] { (_hierarchyA, _hierarchyB), (_hierarchyB, _hierarchyA) })
        {
            foreach (var current in from)
            {
                foreach (var target in to)
                {
                    data.Add(current.Name, target.Name);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CrossHierarchyPairs))]
    public void CanReach_AcrossHierarchies_IsAlwaysRefused(string currentName, string targetName)
    {
        // Arrange
        var current = Find(currentName);
        var target = Find(targetName);

        // The broker targets a call could name: none, the target itself, the caller, the caller's own parent or
        // child, and the target's name in another case.
        string[] brokerTargets = [null, target.Name, target.Name.ToUpperInvariant(), current.Name, _firmA.Name, _firmB.Name, _childA1.Name, _childB1.Name];

        // Act + Assert
        foreach (var brokerTarget in brokerTargets)
        {
            foreach (var isRead in new[] { true, false })
            {
                Assert.False(
                    TenantHierarchyAccessRules.CanReach(current, target, brokerTarget, isRead),
                    $"{current.Name} reached {target.Name} (broker target '{brokerTarget}', read {isRead}).");
            }
        }
    }

    [Theory]
    [MemberData(nameof(CrossHierarchyPairs))]
    public void IsListed_AcrossHierarchies_IsAlwaysHidden(string currentName, string targetName)
    {
        // Arrange
        var current = Find(currentName);
        var target = Find(targetName);

        // Act + Assert
        foreach (var brokerTarget in new[] { null, target.Name, current.Name })
        {
            Assert.False(
                TenantHierarchyAccessRules.IsListed(current, target, brokerTarget),
                $"{current.Name} listed {target.Name} (broker target '{brokerTarget}').");
        }
    }

    [Fact]
    public void CanReach_WithinAHierarchy_StillWorks_SoTheRefusalsAboveAreNotVacuous()
    {
        // Assert
        Assert.True(TenantHierarchyAccessRules.CanReach(_firmB, _childB1, null, isRead: true));
        Assert.True(TenantHierarchyAccessRules.CanReach(_firmB, _childB1, _childB1.Name, isRead: false));
        Assert.True(TenantHierarchyAccessRules.CanReach(_childB1, _firmB, _firmB.Name, isRead: true));
        Assert.True(TenantHierarchyAccessRules.IsListed(_firmB, _childB2, null));
    }

    [Fact]
    public void CanReach_ChildOfAnotherParentThatNamesThisParent_IsRefused()
    {
        // Arrange: a child of firm B whose settings carry firm A's name. Only the parent's tenant identifier counts.
        var impostor = TestShellSettings.Child("impostor", _firmB);
        impostor[TenantHierarchyConstants.SettingsKeys.ParentTenant] = _firmA.Name;

        // Assert
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, impostor, null, isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, impostor, impostor.Name, isRead: false));
        Assert.False(TenantHierarchyAccessRules.IsListed(_firmA, impostor, null));
    }

    [Fact]
    public void CanReach_OrdinaryTenantThatClaimsThisParentsIdentifier_IsRefused()
    {
        // Arrange: a tenant without the child role that names firm A as its parent.
        var claimant = TestShellSettings.Ordinary("claimant");
        claimant[TenantHierarchyConstants.SettingsKeys.ParentTenantId] = _firmA.TenantId;

        // Assert
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, claimant, claimant.Name, isRead: false));
        Assert.False(TenantHierarchyAccessRules.IsListed(_firmA, claimant, null));
    }

    [Fact]
    public void CanReach_ChildThatClaimsAnotherParentsIdentifier_ReachesOnlyThatParent_NotTheOneThatCreatedIt()
    {
        // Arrange: a child created under firm A whose settings were rewritten to name firm B.
        var rewritten = TestShellSettings.Child("rewritten", _firmA);
        rewritten[TenantHierarchyConstants.SettingsKeys.ParentTenantId] = _firmB.TenantId;

        // Assert: firm A loses it; it never reaches firm A or firm A's children.
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, rewritten, rewritten.Name, isRead: false));
        Assert.False(TenantHierarchyAccessRules.CanReach(rewritten, _firmA, _firmA.Name, isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(rewritten, _childA1, _childA1.Name, isRead: true));
    }

    [Fact]
    public void CanReach_ParentsWithoutATenantIdentifier_NeverClaimEachOthersChildren()
    {
        // Arrange: two parents whose identifiers are missing, and a child whose parent identifier is missing too.
        var parentOne = TestShellSettings.Parent("one");
        parentOne.VersionId = null;
        var parentTwo = TestShellSettings.Parent("two");
        parentTwo.VersionId = null;
        var child = TestShellSettings.Child("lost", parentTwo);
        child[TenantHierarchyConstants.SettingsKeys.ParentTenantId] = null;

        // Assert
        Assert.False(TenantHierarchyAccessRules.CanReach(parentOne, child, child.Name, isRead: false));
        Assert.False(TenantHierarchyAccessRules.CanReach(parentTwo, child, child.Name, isRead: false));
        Assert.False(TenantHierarchyAccessRules.CanReach(child, parentOne, parentOne.Name, isRead: true));
    }

    [Fact]
    public void CanReach_ParentToParent_IsRefusedEvenWhenOneIsTheBrokerTarget()
    {
        // Assert
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmA, _firmB, _firmB.Name, isRead: true));
        Assert.False(TenantHierarchyAccessRules.CanReach(_firmB, _firmA, _firmA.Name, isRead: false));
    }

    private static ShellSettings Find(string name)
        => _hierarchyA.Concat(_hierarchyB).Single(settings => settings.Name == name);
}
