namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.TenantIsolation;

/// <summary>
/// Defines the shared two-tenant Contact Center isolation fixture.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TenantIsolationCollection : ICollectionFixture<TenantIsolationFixture>
{
    /// <summary>
    /// The xUnit collection name used by tenant-isolation tests.
    /// </summary>
    public const string Name = "Contact Center tenant isolation";
}
