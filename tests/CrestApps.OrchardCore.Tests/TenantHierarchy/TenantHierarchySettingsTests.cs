using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class TenantHierarchySettingsTests
{
    [Theory]
    [InlineData("Parent", TenantHierarchyConstants.Roles.Parent)]
    [InlineData("parent", TenantHierarchyConstants.Roles.Parent)]
    [InlineData("CHILD", TenantHierarchyConstants.Roles.Child)]
    [InlineData("Something", null)]
    [InlineData(null, null)]
    public void GetHierarchyRole_ReadsTheRoleIgnoringCase(string value, string expected)
    {
        // Arrange
        var settings = new ShellSettings { Name = "tenant" };
        settings[TenantHierarchyConstants.SettingsKeys.Role] = value;

        // Act
        var role = settings.GetHierarchyRole();

        // Assert
        Assert.Equal(expected, role);
    }

    [Fact]
    public void IsChildOf_MatchesByTenantIdentifierOnly()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        var impostor = TestShellSettings.Parent("firma-copy");
        var child = TestShellSettings.Child("child", parent);

        // Assert
        Assert.True(child.IsChildOf(parent));
        Assert.False(child.IsChildOf(impostor));
        Assert.False(parent.IsChildOf(parent));
    }

    [Fact]
    public void IsChildOf_ParentThatIsNoLongerAParent_IsFalse()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        var child = TestShellSettings.Child("child", parent);
        parent[TenantHierarchyConstants.SettingsKeys.Role] = null;

        // Assert
        Assert.False(child.IsChildOf(parent));
    }

    [Fact]
    public void GetParentPolicy_WithoutValues_ReturnsTheDefaults()
    {
        // Act
        var policy = TestShellSettings.Parent("firma").GetParentPolicy();

        // Assert
        Assert.Equal(ParentTenantPolicy.DefaultMaxChildren, policy.MaxChildren);
        Assert.Equal(ChildDatabaseStrategy.SqlitePerChild, policy.DatabaseStrategy);
        Assert.Equal(TimeSpan.FromMinutes(2), policy.SessionValidationInterval);
        Assert.Equal(SwitcherMode.Hosted, policy.SwitcherMode);
        Assert.Empty(policy.Recipes);
        Assert.NotNull(policy.Labels);
    }

    [Fact]
    public void WritePolicy_ThenGetParentPolicy_RoundTrips()
    {
        // Arrange
        var settings = TestShellSettings.Parent("firma");
        var policy = new ParentTenantPolicy
        {
            MaxChildren = 42,
            ChildHostPattern = "{business}.clients.example.org",
            Recipes = ["Blank", "Agency"],
            DatabaseStrategy = ChildDatabaseStrategy.SchemaPerChild,
            DatabasePool = "pool-a",
            BlockedFeatures = ["OrchardCore.Workflows.Http"],
            DeniedLocalPermissions = ["ManageRecipes", "Import"],
            RequireMfa = true,
            SessionValidationInterval = TimeSpan.FromMinutes(5),
            SessionIdleTimeout = TimeSpan.FromMinutes(45),
            SessionLifetime = TimeSpan.FromHours(10),
            SwitcherMode = SwitcherMode.Embedded,
            RemovalGraceDays = 14,
            Labels = new TenantHierarchyLabels
            {
                Parent = "Practice",
                Child = "Client",
                Children = "Clients",
            },
        };

        // Act
        TenantHierarchySettingsWriter.WritePolicy(settings, policy);
        var read = settings.GetParentPolicy();

        // Assert
        Assert.Equal(42, read.MaxChildren);
        Assert.Equal("{business}.clients.example.org", read.ChildHostPattern);
        Assert.Equal(["Blank", "Agency"], read.Recipes);
        Assert.Equal(ChildDatabaseStrategy.SchemaPerChild, read.DatabaseStrategy);
        Assert.Equal("pool-a", read.DatabasePool);
        Assert.Equal(["OrchardCore.Workflows.Http"], read.BlockedFeatures);
        Assert.Equal(["ManageRecipes", "Import"], read.DeniedLocalPermissions);
        Assert.True(read.RequireMfa);
        Assert.Equal(TimeSpan.FromMinutes(5), read.SessionValidationInterval);
        Assert.Equal(TimeSpan.FromMinutes(45), read.SessionIdleTimeout);
        Assert.Equal(TimeSpan.FromHours(10), read.SessionLifetime);
        Assert.Equal(SwitcherMode.Embedded, read.SwitcherMode);
        Assert.Equal(14, read.RemovalGraceDays);
        Assert.Equal("Practice", read.Labels.Parent);
        Assert.Equal("Client", read.Labels.Child);
        Assert.Equal("Clients", read.Labels.Children);
    }

    [Fact]
    public void WritePolicy_ShorterLists_ReplaceTheLongerOnes()
    {
        // Arrange
        var settings = TestShellSettings.Parent("firma");
        TenantHierarchySettingsWriter.WritePolicy(settings, new ParentTenantPolicy { Recipes = ["A", "B", "C"] });

        // Act
        TenantHierarchySettingsWriter.WritePolicy(settings, new ParentTenantPolicy { Recipes = ["D"] });

        // Assert
        Assert.Equal(["D"], settings.GetParentPolicy().Recipes);
    }

    [Fact]
    public void WriteChildPolicy_CopiesWhatTheChildEnforces()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        var child = TestShellSettings.Child("child", parent);

        // Act
        TenantHierarchySettingsWriter.WriteChildPolicy(child, new ParentTenantPolicy
        {
            BlockedFeatures = ["X.Feature"],
            DeniedLocalPermissions = ["ManageRecipes"],
        });

        // Assert
        Assert.Equal(["ManageRecipes"], child.GetDeniedLocalPermissions());
        Assert.Equal("X.Feature", child[$"{TenantHierarchyConstants.SettingsKeys.BlockedFeatures}:0"]);
    }

    [Fact]
    public void EnsureAlwaysEnabledFeature_AddsOnceAndKeepsExistingFeatures()
    {
        // Arrange
        var settings = TestShellSettings.Ordinary("tenant");
        settings["Features:0"] = "Some.Feature";

        // Act
        TenantHierarchySettingsWriter.EnsureAlwaysEnabledFeature(settings, TenantHierarchyConstants.Features.Child);
        TenantHierarchySettingsWriter.EnsureAlwaysEnabledFeature(settings, TenantHierarchyConstants.Features.Child);

        // Assert
        Assert.Equal(["Some.Feature", TenantHierarchyConstants.Features.Child], TenantHierarchySettingsWriter.GetAlwaysEnabledFeatures(settings));
    }

    [Fact]
    public void RemoveAlwaysEnabledFeature_RemovesOnlyThatFeature()
    {
        // Arrange
        var settings = TestShellSettings.Ordinary("tenant");
        settings["Features:0"] = TenantHierarchyConstants.Features.Parent;
        settings["Features:1"] = "Some.Feature";

        // Act
        TenantHierarchySettingsWriter.RemoveAlwaysEnabledFeature(settings, TenantHierarchyConstants.Features.Parent);

        // Assert
        Assert.Equal(["Some.Feature"], TenantHierarchySettingsWriter.GetAlwaysEnabledFeatures(settings));
    }

    [Fact]
    public void ClearHierarchy_RemovesEveryHierarchyKeyAndForcedFeature()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        TenantHierarchySettingsWriter.WritePolicy(parent, new ParentTenantPolicy { Recipes = ["Blank"] });
        TenantHierarchySettingsWriter.EnsureAlwaysEnabledFeature(parent, TenantHierarchyConstants.Features.Parent);

        // Act
        TenantHierarchySettingsWriter.ClearHierarchy(parent);

        // Assert
        Assert.False(parent.IsInTenantHierarchy());
        Assert.Null(parent.GetHierarchySlug());
        Assert.Empty(parent.GetParentPolicy().Recipes);
        Assert.Empty(TenantHierarchySettingsWriter.GetAlwaysEnabledFeatures(parent));
    }

    [Fact]
    public void GetHierarchyDisplayName_FallsBackToTheTenantName()
    {
        // Arrange
        var settings = TestShellSettings.Parent("firma");

        // Act + Assert
        Assert.Equal("firma", settings.GetHierarchyDisplayName());

        settings[TenantHierarchyConstants.SettingsKeys.DisplayName] = "Firm A";
        Assert.Equal("Firm A", settings.GetHierarchyDisplayName());
    }

    [Fact]
    public void GetPrimaryHost_ReturnsTheFirstHost()
    {
        // Arrange
        var settings = TestShellSettings.Ordinary("tenant");
        settings.RequestUrlHost = "one.example.com, two.example.com";

        // Act + Assert
        Assert.Equal("one.example.com", settings.GetPrimaryHost());
        Assert.Null(TestShellSettings.Ordinary("nohost").GetPrimaryHost());
    }
}
