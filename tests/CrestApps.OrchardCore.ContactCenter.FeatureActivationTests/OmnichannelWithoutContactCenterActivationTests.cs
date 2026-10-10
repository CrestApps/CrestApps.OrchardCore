using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Omnichannel activities and the CRM run without the Contact Center. The always-open business-hours gate was
/// registered by the Contact Center feature only, so on a tenant with the CRM alone the load editor could not be
/// built and Load Activities answered 500.
/// </summary>
public sealed class OmnichannelWithoutContactCenterActivationTests
{
    [Fact]
    public async Task WithTheCrmAlone_TheLoadEditorIsBuilt_AndBusinessHoursAreAlwaysOpen()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile("crm-without-contact-center", OmnichannelConstants.Features.Crm));

        // Act
        var (contactCenterEnabled, drivers, gate) = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var enabled = await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync();

            return (
                enabled.Any(feature => feature.Id == ContactCenterConstants.Feature.Area),
                services.GetServices<IDisplayDriver<OmnichannelActivityBatch>>().ToArray(),
                services.GetRequiredService<IBusinessHoursGate>());
        });

        // Assert
        Assert.False(contactCenterEnabled);
        Assert.NotEmpty(drivers);
        Assert.IsType<AlwaysOpenBusinessHoursGate>(gate);
    }

    [Fact]
    public async Task WithBusinessHours_TheCalendarGateReplacesTheAlwaysOpenDefault()
    {
        // Arrange
        // The business hours feature has no dependency on Omnichannel, so its registration may run before or after
        // the default's; the calendar gate must win either way.
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile(
            "crm-with-business-hours",
            OmnichannelConstants.Features.Crm,
            ContactCenterConstants.Feature.BusinessHours));

        // Act
        var gate = await host.ExecuteInTenantScopeAsync(tenant, services =>
            Task.FromResult(services.GetRequiredService<IBusinessHoursGate>()));

        // Assert
        Assert.IsType<BusinessHoursGate>(gate);
    }

    private static ContactCenterTenantProfile Profile(string id, params string[] features)
        => new()
        {
            Id = id,
            ProviderProfile = "none",
            Features = features,
        };
}
