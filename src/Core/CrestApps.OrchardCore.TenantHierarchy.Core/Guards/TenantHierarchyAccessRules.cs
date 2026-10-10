using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Decides whether code running in one tenant may reach another tenant through <see cref="IShellHost"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Code that runs in no tenant (host startup, distributed sync, the host background loop) and code that runs
/// in Default may reach any tenant.</item>
/// <item>Every tenant may reach itself and Default.</item>
/// <item>An ordinary tenant may reach any tenant that is not part of a hierarchy.</item>
/// <item>A parent may read its own children, and may change or open a scope on one of them only inside a broker call
/// that targets that child.</item>
/// <item>A child may reach its own parent only inside a broker call that targets that parent.</item>
/// </list>
/// </remarks>
internal static class TenantHierarchyAccessRules
{
    /// <summary>
    /// Returns whether code running in the current tenant may reach the target tenant.
    /// </summary>
    /// <param name="current">The settings of the tenant whose code is running, or <see langword="null"/> outside any tenant.</param>
    /// <param name="target">The settings of the tenant the call targets.</param>
    /// <param name="brokerTargetTenantName">The tenant the current broker call may reach, or <see langword="null"/>.</param>
    /// <param name="isRead"><see langword="true"/> when the call only reads settings or shell contexts.</param>
    public static bool CanReach(
        ShellSettings current,
        ShellSettings target,
        string brokerTargetTenantName,
        bool isRead)
    {
        if (current is null || target is null)
        {
            return true;
        }

        if (current.IsDefaultShell() ||
            target.IsDefaultShell() ||
            string.Equals(current.Name, target.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var currentRole = current.GetHierarchyRole();
        var isBrokerTarget = brokerTargetTenantName != null &&
            string.Equals(brokerTargetTenantName, target.Name, StringComparison.OrdinalIgnoreCase);

        if (currentRole is null)
        {
            return !target.IsInTenantHierarchy();
        }

        if (currentRole == TenantHierarchyConstants.Roles.Parent)
        {
            if (!target.IsChildOf(current))
            {
                return false;
            }

            return isRead || isBrokerTarget;
        }

        if (target.IsParentTenant() &&
            !string.IsNullOrEmpty(target.TenantId) &&
            string.Equals(current.GetParentTenantId(), target.TenantId, StringComparison.Ordinal))
        {
            return isBrokerTarget;
        }

        return false;
    }

    /// <summary>
    /// Returns whether a tenant appears in the lists that code running in the current tenant reads. In a parent the
    /// lists hold the parent and its own children; in a child they hold only the child.
    /// </summary>
    /// <param name="current">The settings of the tenant whose code is running, or <see langword="null"/> outside any tenant.</param>
    /// <param name="target">The settings of the listed tenant.</param>
    /// <param name="brokerTargetTenantName">The tenant the current broker call may reach, or <see langword="null"/>.</param>
    public static bool IsListed(
        ShellSettings current,
        ShellSettings target,
        string brokerTargetTenantName)
    {
        if (current is null || current.IsDefaultShell())
        {
            return true;
        }

        if (string.Equals(current.Name, target.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!current.IsInTenantHierarchy())
        {
            return !target.IsInTenantHierarchy();
        }

        if (target.IsDefaultShell())
        {
            return false;
        }

        return CanReach(current, target, brokerTargetTenantName, isRead: true);
    }
}
