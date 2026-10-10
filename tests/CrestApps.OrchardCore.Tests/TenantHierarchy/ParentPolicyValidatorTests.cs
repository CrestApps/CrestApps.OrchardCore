using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class ParentPolicyValidatorTests
{
    [Fact]
    public void Validate_DefaultPolicy_IsValid()
    {
        // Act
        var errors = ParentPolicyValidator.Validate(new ParentTenantPolicy(), (_, _) => true);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("*.platform.com")]
    [InlineData("{business}.*.platform.com")]
    [InlineData("platform.com")]
    [InlineData("{business}.platform.com/x")]
    public void Validate_UnsafeHostPattern_IsReported(string pattern)
    {
        // Act
        var errors = ParentPolicyValidator.Validate(new ParentTenantPolicy { ChildHostPattern = pattern }, (_, _) => true);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.ChildHostPattern)));
    }

    [Fact]
    public void Validate_StrategyWithoutAProvisioner_IsReported()
    {
        // Act
        var errors = ParentPolicyValidator.Validate(
            new ParentTenantPolicy { DatabaseStrategy = ChildDatabaseStrategy.DatabasePerChild, DatabasePool = "missing" },
            (strategy, pool) => strategy == ChildDatabaseStrategy.SqlitePerChild);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.DatabasePool)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ParentPolicyValidator.MaxChildrenLimit + 1)]
    public void Validate_LimitOutOfRange_IsReported(int maxChildren)
    {
        // Act
        var errors = ParentPolicyValidator.Validate(new ParentTenantPolicy { MaxChildren = maxChildren }, (_, _) => true);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.MaxChildren)));
    }

    [Fact]
    public void Validate_ZeroOrLongDurations_AreReported()
    {
        // Act
        var errors = ParentPolicyValidator.Validate(
            new ParentTenantPolicy
            {
                SessionValidationInterval = TimeSpan.FromHours(2),
                SessionIdleTimeout = TimeSpan.Zero,
                SessionLifetime = TimeSpan.FromMinutes(-1),
                RemovalGraceDays = 400,
            },
            (_, _) => true);

        // Assert
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.SessionValidationInterval)));
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.SessionIdleTimeout)));
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.SessionLifetime)));
        Assert.True(errors.ContainsKey(nameof(ParentTenantPolicy.RemovalGraceDays)));
    }
}
