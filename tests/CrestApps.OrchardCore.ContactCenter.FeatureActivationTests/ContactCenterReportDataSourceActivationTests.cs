using CrestApps.OrchardCore.ContactCenter.Reports.DataSources;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves on a live tenant that the Contact Center report data source offers the data sets of the features that are
/// enabled, and only those: a data set registered in the wrong feature still compiles, and would list records whose
/// feature is off.
/// </summary>
public sealed class ContactCenterReportDataSourceActivationTests
{
    [Fact]
    public async Task WorkDistributionWithTheReportBuilder_OffersItsDataSets_ButNotThoseOfDisabledFeatures()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "report-builder-work-distribution",
            ProviderProfile = "none",
            Features = [ContactCenterConstants.Feature.Queues, ReportsConstants.BuilderFeature],
        });

        // Act
        var (sources, dataSets) = await host.ExecuteInTenantScopeAsync(tenant, serviceProvider =>
        {
            var sourceNames = serviceProvider.GetServices<IReportDataSource>().Select(source => source.Name).ToArray();
            var dataSetNames = serviceProvider.GetServices<IContactCenterReportDataSet>().Select(dataSet => dataSet.Descriptor.Name).ToArray();

            return Task.FromResult((sourceNames, dataSetNames));
        });

        // Assert
        Assert.Contains(ContactCenterReportDataSets.Source, sources);
        Assert.Subset(
            dataSets.ToHashSet(StringComparer.Ordinal),
            new HashSet<string>(
                [
                    ContactCenterReportDataSets.Interactions,
                    ContactCenterReportDataSets.InteractionEvents,
                    ContactCenterReportDataSets.CallSessions,
                    ContactCenterReportDataSets.CallQuality,
                    ContactCenterReportDataSets.Queues,
                    ContactCenterReportDataSets.QueueGroups,
                    ContactCenterReportDataSets.QueueItems,
                    ContactCenterReportDataSets.AgentProfiles,
                    ContactCenterReportDataSets.AgentSessions,
                ],
                StringComparer.Ordinal));
        Assert.DoesNotContain(ContactCenterReportDataSets.CallRecordings, dataSets);
        Assert.DoesNotContain(ContactCenterReportDataSets.DialerProfiles, dataSets);
        Assert.DoesNotContain(ContactCenterReportDataSets.CallbackRequests, dataSets);
        Assert.DoesNotContain(ContactCenterReportDataSets.SharedVoicemails, dataSets);
    }

    [Fact]
    public async Task ContactCenterWithoutTheReportBuilder_RegistersNoDataSource()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "report-builder-off",
            ProviderProfile = "none",
            Features = [ContactCenterConstants.Feature.Queues],
        });

        // Act
        var dataSets = await host.ExecuteInTenantScopeAsync(tenant, serviceProvider =>
            Task.FromResult(serviceProvider.GetServices<IContactCenterReportDataSet>().Count()));

        // Assert
        Assert.Equal(0, dataSets);
    }
}
