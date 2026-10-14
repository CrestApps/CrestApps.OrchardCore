namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Shares one tenant hierarchy between the integration tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TenantHierarchyCollection : ICollectionFixture<TenantHierarchyFixture>
{
    /// <summary>
    /// The collection name.
    /// </summary>
    public const string Name = "Tenant hierarchy";
}
