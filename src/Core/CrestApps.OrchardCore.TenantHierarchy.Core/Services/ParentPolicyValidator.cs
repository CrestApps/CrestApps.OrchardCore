using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Checks a parent policy before the platform saves it.
/// </summary>
public static class ParentPolicyValidator
{
    /// <summary>
    /// The highest number of child tenants a policy may allow.
    /// </summary>
    public const int MaxChildrenLimit = 10000;

    /// <summary>
    /// Returns the problems of a policy, by property name. An empty result means the policy is valid.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <param name="canProvision">Returns whether a strategy and pool can be provisioned on this host.</param>
    public static IReadOnlyDictionary<string, string> Validate(ParentTenantPolicy policy, Func<ChildDatabaseStrategy, string, bool> canProvision)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(canProvision);

        var errors = new Dictionary<string, string>(StringComparer.Ordinal);

        if (policy.MaxChildren < 0 || policy.MaxChildren > MaxChildrenLimit)
        {
            errors[nameof(ParentTenantPolicy.MaxChildren)] = $"The limit must be between 0 and {MaxChildrenLimit}.";
        }

        if (!string.IsNullOrWhiteSpace(policy.ChildHostPattern) && !TenantHierarchyNaming.IsValidHostPattern(policy.ChildHostPattern.Trim()))
        {
            errors[nameof(ParentTenantPolicy.ChildHostPattern)] = "The pattern must start with '{business}.' followed by a host name, with no wildcard, list or path.";
        }

        if (!canProvision(policy.DatabaseStrategy, policy.DatabasePool))
        {
            errors[nameof(ParentTenantPolicy.DatabasePool)] = "No database provisioner handles this strategy with this pool. Configure the pool under TenantHierarchy:DatabasePools.";
        }

        ValidatePositive(policy.SessionValidationInterval, nameof(ParentTenantPolicy.SessionValidationInterval), errors);
        ValidatePositive(policy.SessionIdleTimeout, nameof(ParentTenantPolicy.SessionIdleTimeout), errors);
        ValidatePositive(policy.SessionLifetime, nameof(ParentTenantPolicy.SessionLifetime), errors);

        if (policy.SessionValidationInterval > TimeSpan.FromHours(1))
        {
            errors[nameof(ParentTenantPolicy.SessionValidationInterval)] = "The validation interval can be at most one hour.";
        }

        if (policy.RemovalGraceDays < 0 || policy.RemovalGraceDays > 365)
        {
            errors[nameof(ParentTenantPolicy.RemovalGraceDays)] = "The grace period must be between 0 and 365 days.";
        }

        return errors;
    }

    private static void ValidatePositive(TimeSpan value, string name, Dictionary<string, string> errors)
    {
        if (value <= TimeSpan.Zero)
        {
            errors[name] = "The duration must be greater than zero.";
        }
    }
}
