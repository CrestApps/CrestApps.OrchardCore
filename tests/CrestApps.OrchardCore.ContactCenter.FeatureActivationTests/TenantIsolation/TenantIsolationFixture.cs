namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.TenantIsolation;

/// <summary>
/// Provisions two Contact Center tenants for adversarial tenant-isolation contract tests.
/// </summary>
public sealed class TenantIsolationFixture : IAsyncLifetime
{
    /// <summary>
    /// Gets the in-process Orchard Core host shared by the tenant-isolation tests.
    /// </summary>
    public ContactCenterFeatureActivationHost Host { get; private set; } = null!;

    /// <summary>
    /// Gets the first Contact Center tenant.
    /// </summary>
    public ContactCenterTenant TenantA { get; private set; } = null!;

    /// <summary>
    /// Gets the second Contact Center tenant.
    /// </summary>
    public ContactCenterTenant TenantB { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var matrix = await ContactCenterSupportMatrix.LoadAsync();
        var profile = matrix.TenantProfiles.Single(profile => profile.Id == "ga-core-asterisk");
        Host = await ContactCenterFeatureActivationHost.StartAsync();
        TenantA = await Host.CreateTenantAsync(profile);
        TenantB = await Host.CreateTenantAsync(profile);

        await Host.AssertTenantAsync(TenantA);
        await Host.AssertTenantAsync(TenantB);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Host is not null)
        {
            await Host.DisposeAsync();
        }
    }
}
