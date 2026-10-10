using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class AccessAndSessionRulesTests
{
    private static readonly DateTime _now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ResolveChildRoles_CombinesChildAndParentWideGrants()
    {
        // Arrange
        var grants = new[]
        {
            new AccessGrant { ChildEntryId = null, ChildRoles = ["Editor"] },
            new AccessGrant { ChildEntryId = "one", ChildRoles = ["Administrator", "editor"] },
            new AccessGrant { ChildEntryId = "two", ChildRoles = ["Author"] },
        };

        // Act
        var roles = AccessGrantResolver.ResolveChildRoles(grants, "one");

        // Assert
        Assert.Equal(["Administrator", "Editor"], roles);
    }

    [Fact]
    public void ResolveChildRoles_NoMatchingGrant_IsEmpty()
    {
        // Arrange
        var grants = new[] { new AccessGrant { ChildEntryId = "two", ChildRoles = ["Author"] } };

        // Act
        var roles = AccessGrantResolver.ResolveChildRoles(grants, "one");

        // Assert
        Assert.Empty(roles);
        Assert.Empty(AccessGrantResolver.ResolveChildRoles(null, "one"));
    }

    [Fact]
    public void ResolveEnterableEntries_ParentWideGrant_CoversEveryEntry()
    {
        // Arrange
        var grants = new[] { new AccessGrant { ChildEntryId = null, ChildRoles = ["Editor"] } };

        // Act
        var entries = AccessGrantResolver.ResolveEnterableEntries(grants, ["one", "two"]);

        // Assert
        Assert.Equal(["one", "two"], entries);
    }

    [Fact]
    public void ResolveEnterableEntries_GrantWithoutRoles_CoversNothing()
    {
        // Arrange
        var grants = new[]
        {
            new AccessGrant { ChildEntryId = null, ChildRoles = [] },
            new AccessGrant { ChildEntryId = "two", ChildRoles = ["Author"] },
        };

        // Act
        var entries = AccessGrantResolver.ResolveEnterableEntries(grants, ["one", "two", "three"]);

        // Assert
        Assert.Equal(["two"], entries);
    }

    [Fact]
    public void GetEndReason_ActiveSession_IsNull()
    {
        // Act
        var reason = Evaluate(CreateSession());

        // Assert
        Assert.Null(reason);
    }

    [Fact]
    public void GetEndReason_EndedSession_IsEnded()
    {
        // Arrange
        var session = CreateSession();
        session.EndedUtc = _now.AddMinutes(-1);

        // Act + Assert
        Assert.Equal(DelegatedSessionRules.Ended, Evaluate(session));
    }

    [Fact]
    public void GetEndReason_IdleTooLong_IsIdleTimeout()
    {
        // Arrange
        var session = CreateSession();
        session.LastSeenUtc = _now.AddMinutes(-31);

        // Act + Assert
        Assert.Equal(DelegatedSessionRules.IdleTimeout, Evaluate(session));
    }

    [Fact]
    public void GetEndReason_OlderThanTheLifetime_IsLifetimeExceeded()
    {
        // Arrange
        var session = CreateSession();
        session.CreatedUtc = _now.AddHours(-9);

        // Act + Assert
        Assert.Equal(DelegatedSessionRules.LifetimeExceeded, Evaluate(session));
    }

    [Fact]
    public void GetEndReason_DisabledUser_IsUserDisabled()
        => Assert.Equal(DelegatedSessionRules.UserDisabled, Evaluate(CreateSession(), userIsEnabled: false));

    [Fact]
    public void GetEndReason_ChangedSecurityStamp_IsSecurityStampChanged()
        => Assert.Equal(DelegatedSessionRules.SecurityStampChanged, Evaluate(CreateSession(), securityStamp: "other"));

    [Fact]
    public void GetEndReason_ChildNotReady_IsChildUnavailable()
        => Assert.Equal(DelegatedSessionRules.ChildUnavailable, Evaluate(CreateSession(), childIsReady: false));

    [Fact]
    public void GetEndReason_NoRolesLeft_IsGrantRemoved()
        => Assert.Equal(DelegatedSessionRules.GrantRemoved, Evaluate(CreateSession(), roles: []));

    private static DelegatedAccessSession CreateSession()
    {
        return new DelegatedAccessSession
        {
            CreatedUtc = _now.AddHours(-1),
            LastSeenUtc = _now.AddMinutes(-5),
            SecurityStamp = "stamp",
        };
    }

    private static string Evaluate(
        DelegatedAccessSession session,
        bool userIsEnabled = true,
        string securityStamp = "stamp",
        bool childIsReady = true,
        string[] roles = null)
    {
        return DelegatedSessionRules.GetEndReason(
            session,
            new ParentTenantPolicy(),
            _now,
            userIsEnabled,
            securityStamp,
            childIsReady,
            roles ?? ["Editor"]);
    }
}
