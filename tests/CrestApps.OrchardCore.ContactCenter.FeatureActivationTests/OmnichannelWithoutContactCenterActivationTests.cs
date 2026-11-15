using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Services;
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
        Assert.IsType<OpenHoursBusinessHoursGate>(gate);
        Assert.True(await gate.IsOpenAsync("any-calendar", DateTime.UtcNow, timeZoneId: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WithBusinessHours_TheGateReadsTheBusinessHoursSchedules()
    {
        // Arrange
        // The business hours feature has no dependency on Omnichannel, so its registration may run before or after
        // the gate's; either way the gate reads the shared business hours, whose schedules the Business Hours module
        // keeps.
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile(
            "crm-with-business-hours",
            OmnichannelConstants.Features.Crm,
            ContactCenterConstants.Feature.BusinessHours));

        // Act
        var (gate, sources) = await host.ExecuteInTenantScopeAsync(tenant, services =>
            Task.FromResult((services.GetRequiredService<IBusinessHoursGate>(), services.GetServices<IHoursScheduleSource>().ToArray())));

        // Assert
        Assert.IsType<OpenHoursBusinessHoursGate>(gate);
        Assert.Single(sources, source => source is BusinessHoursScheduleSource);
    }

    [Fact]
    public async Task WithBusinessHoursAlone_TheBusinessHoursModuleComesWithIt()
    {
        // Arrange
        // Pulled in without the base feature (Omnichannel Activities depends on it alone), the Contact Center business
        // hours bring the Business Hours module, whose schedules they read.
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile(
            "business-hours-alone",
            OmnichannelConstants.Features.Crm,
            ContactCenterConstants.Feature.BusinessHours));

        // Act
        var (contactCenterEnabled, businessHoursEnabled, schedules) = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var enabled = await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync();

            return (
                enabled.Any(feature => feature.Id == ContactCenterConstants.Feature.Area),
                enabled.Any(feature => feature.Id == BusinessHoursConstants.Features.BusinessHours),
                await services.GetRequiredService<IOpenHoursService>().ListSchedulesAsync());
        });

        // Assert
        Assert.False(contactCenterEnabled);
        Assert.True(businessHoursEnabled);
        Assert.Empty(schedules);
    }

    private static ContactCenterTenantProfile Profile(string id, params string[] features)
        => new()
        {
            Id = id,
            ProviderProfile = "none",
            Features = features,
        };
}
