using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Decides whether a request that reached the Default tenant was meant for a tenant of a hierarchy that is not
/// running. When the Default tenant has no host name, it serves every request no running tenant claims, so without
/// this rule the address of a suspended or removed child tenant would show the platform site and its sign-in page.
/// </summary>
public static class UnavailableAddressRules
{
    /// <summary>
    /// Returns whether a host belongs to a tenant of a hierarchy: the host of a parent or child tenant, or any
    /// address under the host of a parent tenant, where its child tenants live.
    /// </summary>
    /// <param name="requestHost">The host of the request, with its port when it has one.</param>
    /// <param name="tenants">The settings of every tenant.</param>
    public static bool IsHierarchyAddress(string requestHost, IEnumerable<ShellSettings> tenants)
    {
        ArgumentNullException.ThrowIfNull(tenants);

        if (string.IsNullOrWhiteSpace(requestHost))
        {
            return false;
        }

        var (requestName, requestPort) = Split(requestHost);

        foreach (var tenant in tenants)
        {
            if (tenant.IsDefaultShell() || !tenant.IsInTenantHierarchy())
            {
                continue;
            }

            foreach (var host in tenant.RequestUrlHosts)
            {
                if (string.IsNullOrWhiteSpace(host))
                {
                    continue;
                }

                var (name, port) = Split(host);
                var portMatches = port is null || string.Equals(port, requestPort, StringComparison.Ordinal);

                if (!portMatches)
                {
                    continue;
                }

                if (string.Equals(name, requestName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (tenant.IsParentTenant() && requestName.EndsWith($".{name}", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (string Name, string Port) Split(string host)
    {
        var value = host.Trim();
        var colon = value.LastIndexOf(':');

        // A bracketed IPv6 address carries colons of its own; only a colon after the closing bracket starts a port.
        if (colon > 0 && colon > value.LastIndexOf(']'))
        {
            return (value[..colon], value[(colon + 1)..]);
        }

        return (value, null);
    }
}
