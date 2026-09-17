namespace CrestApps.Core.Hosting;

/// <summary>
/// The default <see cref="ITenantAccessor"/> for a host that serves one tenant.
/// </summary>
public sealed class SingleTenantAccessor : ITenantAccessor
{
    /// <summary>
    /// The tenant name reported when the host has no notion of tenancy.
    /// </summary>
    public const string DefaultTenantName = "Default";

    /// <inheritdoc/>
    public string TenantName => DefaultTenantName;
}
