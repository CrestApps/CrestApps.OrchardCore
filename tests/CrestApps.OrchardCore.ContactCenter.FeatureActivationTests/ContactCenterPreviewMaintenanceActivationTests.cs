using CrestApps.OrchardCore.ContactCenter.Core.Maintenance;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Maintenance;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves the preview export, quiesce, reset, and verify procedure works on a real tenant, with the real
/// container wiring, rather than only against a hand-assembled service graph.
/// </summary>
/// <remarks>
/// The unit tests construct the maintenance service directly, so they cannot catch a registration defect: a
/// data set that is never registered, options that are never bound, or lifecycle participants that the
/// container does not surface. Those defects would make an operator's reset silently incomplete on a real
/// deployment, which is the exact failure this tooling exists to prevent.
/// </remarks>
public sealed class ContactCenterPreviewMaintenanceActivationTests
{
    private static readonly string[] _maintenanceFeatures =
    [
        ContactCenterConstants.Feature.Area,
        ContactCenterConstants.Feature.Maintenance,
    ];

    [Fact]
    public async Task MaintenanceFeature_RegistersADataSetForEveryDeclaredDocumentType()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "preview-maintenance-registration",
            ProviderProfile = "none",
            Features = _maintenanceFeatures,
        });

        var registered = await host.ExecuteInTenantScopeAsync(tenant, services =>
        {
            var dataSets = services.GetServices<IContactCenterPreviewDataSet>()
                .Select(dataSet => dataSet.Key)
                .Order(StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult(dataSets);
        });

        var declared = ContactCenterPreviewDataSetRegistry.Descriptors
            .Select(descriptor => descriptor.DocumentType.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(declared, registered);
    }

    [Fact]
    public async Task ResetIsRefusedByDefault_BecauseTheTenantDidNotOptIn()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "preview-maintenance-default-refusal",
            ProviderProfile = "none",
            Features = _maintenanceFeatures,
        });

        var report = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var service = services.GetRequiredService<IContactCenterPreviewMaintenanceService>();

            return await service.ResetAsync(new ContactCenterPreviewResetRequest
            {
                ConfirmationToken = tenant.Settings.Name,
                Scope = ContactCenterPreviewResetScope.All,
            });
        });

        Assert.Equal(ContactCenterPreviewResetRefusalReason.ResetNotAllowed, report.RefusalReason);
    }

    [Fact]
    public async Task ExportQuiesceResetVerify_ClearsOperationalDataOnAnOptedInTenant()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync(
            shellConfiguration: new Dictionary<string, string>
            {
                ["CrestApps_ContactCenter:PreviewMaintenance:AllowReset"] = bool.TrueString,
            });

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "preview-maintenance-cycle",
            ProviderProfile = "none",
            Features = _maintenanceFeatures,
        });

        await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var session = services.GetRequiredService<ISession>();

            session.Save(
                new Interaction
                {
                    ItemId = "activation-interaction",
                },
                collection: ContactCenterConstants.CollectionName);

            session.Save(
                new ActivityQueue
                {
                    ItemId = "activation-queue",
                    Name = "Activation queue",
                },
                collection: ContactCenterConstants.CollectionName);

            await session.SaveChangesAsync();
        });

        var seeded = await host.ExecuteInTenantScopeAsync(tenant, async services =>
            await services.GetRequiredService<IContactCenterPreviewMaintenanceService>().GetDataSetCountsAsync());

        Assert.Equal(1, seeded.Single(dataSet => dataSet.Key == nameof(Interaction)).Count);
        Assert.Equal(1, seeded.Single(dataSet => dataSet.Key == nameof(ActivityQueue)).Count);

        // The receipt is taken in one scope and presented in another, exactly as an operator does across two
        // admin requests.
        var receipt = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var service = services.GetRequiredService<IContactCenterPreviewMaintenanceService>();
            await service.QuiesceAsync(TimeSpan.FromSeconds(10));

            using var destination = new MemoryStream();
            var export = await service.ExportAsync(destination);

            Assert.Equal(2, export.DocumentCount);

            return export.Receipt;
        });

        var report = await host.ExecuteInTenantScopeAsync(tenant, async services =>
            await services.GetRequiredService<IContactCenterPreviewMaintenanceService>().ResetAsync(
                new ContactCenterPreviewResetRequest
                {
                    ConfirmationToken = tenant.Settings.Name,
                    ExportReceipt = receipt,
                    Scope = ContactCenterPreviewResetScope.OperationalData,
                }));

        Assert.True(report.Succeeded, $"Expected the reset to run but it was refused because {report.RefusalReason}.");

        var verification = await host.ExecuteInTenantScopeAsync(tenant, async services =>
            await services.GetRequiredService<IContactCenterPreviewMaintenanceService>()
                .VerifyAsync(ContactCenterPreviewResetScope.OperationalData));

        Assert.True(verification.IsClean, $"Residual data sets after the reset: {string.Join(", ", verification.ResidualDataSetKeys)}.");
        Assert.Equal(0, verification.DataSets.Single(dataSet => dataSet.Key == nameof(Interaction)).Count);
        Assert.Equal(1, verification.DataSets.Single(dataSet => dataSet.Key == nameof(ActivityQueue)).Count);
    }

    [Fact]
    public async Task ResetIsRefusedInProduction_EvenWhenTheTenantOptedIn()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync(
            environmentName: Microsoft.Extensions.Hosting.Environments.Production,
            shellConfiguration: new Dictionary<string, string>
            {
                ["CrestApps_ContactCenter:PreviewMaintenance:AllowReset"] = bool.TrueString,
                ["OrchardCore_HealthChecks:Url"] = "/health/aggregate",
            });

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "preview-maintenance-production",
            ProviderProfile = "none",
            Features = _maintenanceFeatures,
        });

        var report = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var service = services.GetRequiredService<IContactCenterPreviewMaintenanceService>();
            await service.QuiesceAsync(TimeSpan.FromSeconds(10));

            using var destination = new MemoryStream();
            var export = await service.ExportAsync(destination);

            return await service.ResetAsync(new ContactCenterPreviewResetRequest
            {
                ConfirmationToken = tenant.Settings.Name,
                ExportReceipt = export.Receipt,
                Scope = ContactCenterPreviewResetScope.All,
            });
        });

        Assert.Equal(ContactCenterPreviewResetRefusalReason.ProductionEnvironment, report.RefusalReason);
    }
}
