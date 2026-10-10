namespace CrestApps.OrchardCore.TenantHierarchy.Core;

/// <summary>
/// Registered by <c>AddTenantHierarchy()</c>, so the module features can check that the host-level guards are installed
/// before they let a parent create child tenants.
/// </summary>
public sealed class TenantHierarchyHostMarker
{
}
