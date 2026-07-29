namespace CrestApps.OrchardCore.SignalR;

/// <summary>
/// Builds SignalR group names that isolate destinations by Orchard tenant.
/// </summary>
public static class TenantSignalRGroupName
{
    /// <summary>
    /// Builds the tenant-qualified group name for a user.
    /// </summary>
    /// <param name="tenantName">The immutable Orchard shell name.</param>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The tenant-qualified user group name.</returns>
    public static string ForUser(string tenantName, string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantName);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return Build(tenantName, "user", userId);
    }

    /// <summary>
    /// Builds the tenant-qualified group name for an application-defined logical group.
    /// </summary>
    /// <param name="tenantName">The immutable Orchard shell name.</param>
    /// <param name="groupName">The logical group name.</param>
    /// <returns>The tenant-qualified application group name.</returns>
    public static string ForGroup(string tenantName, string groupName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantName);
        ArgumentException.ThrowIfNullOrEmpty(groupName);

        return Build(tenantName, "group", groupName);
    }

    private static string Build(string tenantName, string destinationType, string destination)
    {
        return $"tenant:{tenantName.Length}:{tenantName}:{destinationType}:{destination.Length}:{destination}";
    }
}
