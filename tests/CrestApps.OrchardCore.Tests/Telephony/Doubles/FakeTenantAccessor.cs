using CrestApps.Core.Hosting;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// Reports a fixed tenant name, so tests can prove that tenant-keyed state stays separated.
/// </summary>
internal sealed class FakeTenantAccessor : ITenantAccessor
{
    public FakeTenantAccessor(string tenantName)
    {
        TenantName = tenantName;
    }

    public string TenantName { get; }
}
