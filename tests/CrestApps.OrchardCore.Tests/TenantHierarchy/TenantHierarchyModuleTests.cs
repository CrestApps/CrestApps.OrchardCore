using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.Handlers;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class TenantHierarchyModuleTests
{
    [Fact]
    public void DelegatedAccessClaims_CarryTheSessionAndTheParentButNoSecret()
    {
        // Arrange
        var redemption = new DelegatedAccessRedemption
        {
            SessionId = "session",
            ParentTenantId = "parent-id",
            ParentUserId = "user-id",
            ParentDisplayName = "Firm A",
            ParentAddress = "https://firma.platform.com",
            ChildLabel = "Client",
            RolesVersion = "v1",
            SwitcherMode = SwitcherMode.Embedded,
            AuthenticationMethods = ["pwd", "mfa"],
            ValidationInterval = TimeSpan.FromMinutes(3),
        };

        // Act
        var principal = new ClaimsPrincipal(new ClaimsIdentity(DelegatedAccessClaims.Create(redemption), "Test"));

        // Assert
        Assert.True(DelegatedAccessClaims.IsDelegated(principal));
        Assert.Equal("session", DelegatedAccessClaims.GetSessionId(principal));
        Assert.Equal(TimeSpan.FromMinutes(3), DelegatedAccessClaims.GetValidationInterval(principal));
        Assert.Equal("Firm A", principal.FindFirst(TenantHierarchyConstants.ClaimTypes.ParentDisplayName)?.Value);
        Assert.Equal("Embedded", principal.FindFirst(TenantHierarchyConstants.ClaimTypes.SwitcherMode)?.Value);
        Assert.Equal(["pwd", "mfa"], principal.FindAll(TenantHierarchyConstants.ClaimTypes.AuthenticationMethods).Select(claim => claim.Value));
    }

    [Theory]
    [InlineData(null, 120)]
    [InlineData("abc", 120)]
    [InlineData("0", 120)]
    [InlineData("30", 30)]
    [InlineData("999999", 3600)]
    public void GetValidationInterval_IsBounded(string value, int expectedSeconds)
    {
        // Arrange
        var claims = value is null
            ? new List<Claim>()
            : [new Claim(TenantHierarchyConstants.ClaimTypes.ValidationInterval, value)];

        // Act
        var interval = DelegatedAccessClaims.GetValidationInterval(new ClaimsPrincipal(new ClaimsIdentity(claims)));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), interval);
    }

    [Fact]
    public void CopyTo_KeepsTheDelegatedClaimsAndReplacesTheRolesVersion()
    {
        // Arrange
        var source = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(TenantHierarchyConstants.ClaimTypes.SessionId, "session"),
                new Claim(TenantHierarchyConstants.ClaimTypes.RolesVersion, "old"),
                new Claim(TenantHierarchyConstants.ClaimTypes.AuthenticationMethods, "pwd"),
                new Claim(ClaimTypes.Role, "Editor"),
            ],
            "Test"));
        var target = new ClaimsIdentity([new Claim(ClaimTypes.Role, "Administrator")], "Test");

        // Act
        DelegatedAccessClaims.CopyTo(source, target, "new");

        // Assert
        Assert.Equal("session", target.FindFirst(TenantHierarchyConstants.ClaimTypes.SessionId)?.Value);
        Assert.Equal(["new"], target.FindAll(TenantHierarchyConstants.ClaimTypes.RolesVersion).Select(claim => claim.Value));
        Assert.Equal("pwd", target.FindFirst(TenantHierarchyConstants.ClaimTypes.AuthenticationMethods)?.Value);
        Assert.Equal(["Administrator"], target.FindAll(ClaimTypes.Role).Select(claim => claim.Value));
    }

    [Theory]
    [InlineData("EditUsers", true)]
    [InlineData("DeleteUsers", true)]
    [InlineData("ManageUsers", true)]
    [InlineData("AssignRoleToUsers", true)]
    [InlineData("DisableTwoFactorAuthenticationForUsers", true)]
    [InlineData("EditUsersInRole_Editor", true)]
    [InlineData("AssignRoleToUsers_Administrator", true)]
    [InlineData("View Users", false)]
    [InlineData("ManageOwnUserInformation", false)]
    [InlineData("ViewContent", false)]
    [InlineData(null, false)]
    public void IsUserManagementPermission_ProtectsTheUserManagementPermissions(string permission, bool expected)
    {
        // Assert
        Assert.Equal(expected, LinkedUserAuthorizationHandler.IsUserManagementPermission(permission));
    }

    [Fact]
    public void TenantSwitchViewModel_PutsFavoritesAndRecentFirst()
    {
        // Arrange
        var children = new[] { "Charlie", "Alpha", "Bravo", "Delta" }
            .Select(name => new ChildTenantInfo
            {
                Entry = new ChildTenantEntry { EntryId = name.ToLowerInvariant(), DisplayName = name, Status = ChildTenantStatus.Ready },
                State = ChildTenantRuntimeState.Running,
            })
            .ToList();

        var preference = new TenantSwitcherPreference
        {
            Favorites = ["delta"],
            Recent = ["bravo", "delta", "unknown"],
        };

        // Act
        var model = TenantSwitchViewModel.Create(children, preference, new HierarchyLabels(), embedded: false);

        // Assert
        Assert.Equal(["Delta"], model.Favorites.Select(item => item.Info.Entry.DisplayName));
        Assert.Equal(["Bravo"], model.Recent.Select(item => item.Info.Entry.DisplayName));
        Assert.Equal(["Alpha", "Bravo", "Charlie", "Delta"], model.All.Select(item => item.Info.Entry.DisplayName));
        Assert.True(model.All.Single(item => item.Info.Entry.DisplayName == "Delta").IsFavorite);
    }

    [Theory]
    [InlineData("Northwind Traders", "NT")]
    [InlineData("Contoso", "CO")]
    [InlineData("a", "A")]
    [InlineData("", "?")]
    public void TenantSwitchItem_Initials_ComeFromTheName(string name, string expected)
    {
        // Arrange
        var item = new TenantSwitchItem
        {
            Info = new ChildTenantInfo { Entry = new ChildTenantEntry { DisplayName = name } },
        };

        // Assert
        Assert.Equal(expected, item.Initials);
    }

    [Fact]
    public void ParentPolicyEditViewModel_RoundTripsAPolicy()
    {
        // Arrange
        var policy = new ParentTenantPolicy
        {
            MaxChildren = 12,
            ChildHostPattern = "{business}.clients.example.org",
            Recipes = ["Blank"],
            BlockedFeatures = ["A.B", "C.D"],
            DeniedLocalPermissions = ["ManageRecipes"],
            RequireMfa = true,
            SessionValidationInterval = TimeSpan.FromMinutes(4),
            SessionIdleTimeout = TimeSpan.FromMinutes(20),
            SessionLifetime = TimeSpan.FromHours(6),
            SwitcherMode = SwitcherMode.Embedded,
            RemovalGraceDays = 3,
            Labels = new TenantHierarchyLabels { Parent = "Practice", Child = "Client", Children = "Clients" },
        };

        var model = new ParentPolicyEditViewModel();

        // Act
        model.FromPolicy(policy);
        var read = model.ToPolicy();

        // Assert
        Assert.Equal(policy.MaxChildren, read.MaxChildren);
        Assert.Equal(policy.ChildHostPattern, read.ChildHostPattern);
        Assert.Equal(policy.Recipes, read.Recipes);
        Assert.Equal(policy.BlockedFeatures, read.BlockedFeatures);
        Assert.Equal(policy.DeniedLocalPermissions, read.DeniedLocalPermissions);
        Assert.True(read.RequireMfa);
        Assert.Equal(policy.SessionValidationInterval, read.SessionValidationInterval);
        Assert.Equal(policy.SessionIdleTimeout, read.SessionIdleTimeout);
        Assert.Equal(policy.SessionLifetime, read.SessionLifetime);
        Assert.Equal(SwitcherMode.Embedded, read.SwitcherMode);
        Assert.Equal(3, read.RemovalGraceDays);
        Assert.Equal("Clients", read.Labels.Children);
    }

    [Fact]
    public void ParentPolicyEditViewModel_SplitsListsOnLinesAndCommas()
    {
        // Arrange
        var model = new ParentPolicyEditViewModel
        {
            BlockedFeatures = "A.B\r\nC.D, E.F\n\nA.B",
            DeniedLocalPermissions = "  ",
        };

        // Act
        var policy = model.ToPolicy();

        // Assert
        Assert.Equal(["A.B", "C.D", "E.F"], policy.BlockedFeatures);
        Assert.Empty(policy.DeniedLocalPermissions);
    }

    [Fact]
    public void ChildTenantInfo_CanEnter_OnlyWhenReadyAndRunning()
    {
        // Arrange
        var ready = new ChildTenantEntry { Status = ChildTenantStatus.Ready };
        var provisioning = new ChildTenantEntry { Status = ChildTenantStatus.Provisioning };

        // Assert
        Assert.True(new ChildTenantInfo { Entry = ready, State = ChildTenantRuntimeState.Running }.CanEnter);
        Assert.False(new ChildTenantInfo { Entry = ready, State = ChildTenantRuntimeState.Suspended }.CanEnter);
        Assert.False(new ChildTenantInfo { Entry = provisioning, State = ChildTenantRuntimeState.Running }.CanEnter);
        Assert.False(new ChildTenantInfo { Entry = ready, State = ChildTenantRuntimeState.ChangedByPlatform }.CanEnter);
    }

    [Fact]
    public void TenantHierarchyResult_FailureNeedsAMessage()
    {
        // Assert
        Assert.True(TenantHierarchyResult.Success.Succeeded);
        Assert.False(TenantHierarchyResult.Failure("No").Succeeded);
        Assert.Throws<ArgumentException>(() => TenantHierarchyResult.Failure(string.Empty));
    }

    [Fact]
    public void Permissions_SecurityCriticalOnesAreMarked()
    {
        // Assert
        Assert.True(TenantHierarchyPermissions.ManageTenantHierarchy.IsSecurityCritical);
        Assert.True(TenantHierarchyPermissions.CreateChildTenants.IsSecurityCritical);
        Assert.True(TenantHierarchyPermissions.RemoveChildTenants.IsSecurityCritical);
        Assert.True(TenantHierarchyPermissions.ManageChildAccess.IsSecurityCritical);
        Assert.False(TenantHierarchyPermissions.ViewChildTenants.IsSecurityCritical);
        Assert.False(TenantHierarchyPermissions.EnterChildTenants.IsSecurityCritical);
        Assert.Contains(TenantHierarchyPermissions.ManageChildTenants, TenantHierarchyPermissions.ViewChildTenants.ImpliedBy);
    }
}
