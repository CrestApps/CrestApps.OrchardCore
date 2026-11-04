using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Reports;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// The field service reports: the visits data set's arrival, time on site and first-time fix, and the KPIs over a day
/// with one job done the first time and one that needed a return visit.
/// </summary>
public sealed class FieldServiceReportTests
{
    [Fact]
    public async Task TheKpis_CountFirstTimeFixes_ReturnVisits_Arrivals_AndTimeOnSite()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var (done, incomplete) = await TwoJobsAsync(kit);

        var result = await kit.Harness.RunAsync(services => services.GetRequiredService<FieldServiceKpiService>().GetAsync(DispatchKit.Monday(0), DispatchKit.Monday(23), PlanningKit.Zone), commit: false);

        Assert.Equal(2, result.FinishedVisits);
        Assert.Equal(0.5, result.FirstTimeFixRate);
        Assert.Equal(0.5, result.ReturnVisitRate);
        Assert.Equal(0.5, result.OnTimeArrivalRate);
        Assert.Equal(1, result.JobsPerTechnicianDay);
        Assert.Equal(90 + 30, result.OnSiteMinutes);
        Assert.Equal(2, result.WorkOrders);
        Assert.False(result.Truncated);
        Assert.NotEqual(done.ItemId, incomplete.ItemId);
    }

    [Fact]
    public async Task TheVisitsDataSet_GivesArrival_TimeOnSite_AndFirstTimeFix()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var (done, incomplete) = await TwoJobsAsync(kit);

        var rows = await kit.Harness.RunAsync(async services =>
        {
            var source = new FieldServiceReportDataSource([ActivatorUtilities.CreateInstance<VisitsReportDataSet>(services), ActivatorUtilities.CreateInstance<WorkOrdersReportDataSet>(services)]);
            var table = await source.ReadRowsAsync(new DataSourceQuery
            {
                DataSet = VisitsReportDataSet.DataSetName,
                Fields = new HashSet<string>(StringComparer.Ordinal),
                MaxRows = 100,
                Context = new DataSourceContext { User = CalendarTestHarness.User("dispatcher", DispatchPermissions.ViewDispatch.Name) },
            }, 100, TestContext.Current.CancellationToken);
            var fields = table.Fields.Select(field => field.Name).ToList();

            return table.Rows.ToDictionary(row => (string)row[fields.IndexOf("ItemId")], row => (OnTime: row[fields.IndexOf("OnTime")], OnSite: row[fields.IndexOf("OnSiteMinutes")], FirstTime: row[fields.IndexOf("FirstTimeFix")]));
        }, commit: false);

        Assert.Equal(((bool?)true, (long?)90, (bool?)true), ((bool?)rows[done.ItemId].OnTime, (long?)rows[done.ItemId].OnSite, (bool?)rows[done.ItemId].FirstTime));
        Assert.Equal(((bool?)false, (long?)30, (bool?)null), ((bool?)rows[incomplete.ItemId].OnTime, (long?)rows[incomplete.ItemId].OnSite, (bool?)rows[incomplete.ItemId].FirstTime));
    }

    [Fact]
    public async Task TheKpis_CountRevenue_PartsByLocation_AndRepeatWorkOnTheSameAsset()
    {
        await using var kit = await DispatchKit.CreateAsync();

        await kit.UpdateTypeAsync(type =>
        {
            type.BillingMode = WorkOrderBillingMode.TimeAndMaterials;
            type.Currency = "USD";
        });
        kit.Now = DispatchKit.Monday(7);

        // The boiler is serviced (two filters from Van 12 and an hour of labor), then called out again two days later.
        var (first, _) = await kit.AddJobAsync("Boiler service", DispatchKit.Depot, order =>
        {
            order.AssetIds = ["boiler"];
            order.Lines =
            [
                new WorkOrderLine { Id = "filters", Kind = WorkOrderLineKind.Product, Description = "Filter", QuantityUsed = 2, UnitPrice = 15, LocationId = "van-12" },
                new WorkOrderLine { Id = "labor", Kind = WorkOrderLineKind.Labor, Description = "Labor", QuantityUsed = 1, UnitPrice = 90 },
            ];
        });
        var (other, _) = await kit.AddJobAsync("Fit a thermostat", DispatchKit.Depot, order => order.Lines = [new WorkOrderLine { Id = "labor", Kind = WorkOrderLineKind.Labor, Description = "Labor", QuantityUsed = 1, UnitPrice = 50 }]);

        await kit.Harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CrestApps.OrchardCore.WorkOrders.Core.Services.WorkOrderStore>();

            foreach (var id in new[] { first.ItemId, other.ItemId })
            {
                var workOrder = await store.FindByIdAsync(id);

                workOrder.Status = WorkOrderStatus.Completed;
                workOrder.CompletedUtc = DispatchKit.Monday(12);
                await store.UpdateAsync(workOrder);
            }
        });

        kit.Now = DispatchKit.Monday(12).AddDays(2);
        await kit.AddJobAsync("Boiler leaking again", DispatchKit.Depot, order => order.AssetIds = ["boiler"]);

        var result = await kit.Harness.RunAsync(services => services.GetRequiredService<FieldServiceKpiService>().GetAsync(DispatchKit.Monday(0), DispatchKit.Monday(23), PlanningKit.Zone, 30), commit: false);
        var shortWindow = await kit.Harness.RunAsync(services => services.GetRequiredService<FieldServiceKpiService>().GetAsync(DispatchKit.Monday(0), DispatchKit.Monday(23), PlanningKit.Zone, 1), commit: false);

        // (2 × 15 + 90 + 50) / 2.
        Assert.Equal(85m, result.RevenuePerWorkOrder);
        Assert.Equal("USD", result.RevenueCurrency);
        var parts = Assert.Single(result.PartsByLocation);
        Assert.Equal(("van-12", 2m, 1), (parts.LocationId, parts.Quantity, parts.WorkOrders));
        Assert.Equal(1, result.RepeatWorkRate);
        Assert.Equal(0, shortWindow.RepeatWorkRate);
    }

    [Fact]
    public async Task TheAssetDataSets_ListAssets_AndAServiceHistoryRowForEachAssetOnAWorkOrder()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var (boiler, pump) = await kit.Harness.RunAsync(async services =>
        {
            var types = services.GetRequiredService<CrestApps.OrchardCore.Assets.Core.Services.AssetTypeManager>();
            var type = await types.NewAsync();

            type.Name = "Heating";
            await types.CreateAsync(type);

            var manager = services.GetRequiredService<CrestApps.OrchardCore.Assets.Core.Services.AssetManager>();
            var first = await manager.NewAsync();

            first.Name = "Boiler";
            first.AssetTypeId = type.ItemId;
            await manager.CreateAsync(first);

            var second = await manager.NewAsync();

            second.Name = "Pump";
            second.AssetTypeId = type.ItemId;
            await manager.CreateAsync(second);

            return (first, second);
        });

        var (workOrder, _) = await kit.AddJobAsync("Annual service", DispatchKit.Depot, order => order.AssetIds = [boiler.ItemId, pump.ItemId]);
        var reader = CalendarTestHarness.User("manager", DispatchPermissions.ViewDispatch.Name, CrestApps.OrchardCore.WorkOrders.WorkOrdersPermissions.ViewWorkOrders.Name, CrestApps.OrchardCore.Assets.AssetsPermissions.ViewAssets.Name);

        async Task<List<Dictionary<string, object>>> ReadAsync(string dataSet)
            => await kit.Harness.RunAsync(async services =>
            {
                var source = new FieldServiceReportDataSource([
                    ActivatorUtilities.CreateInstance<AssetsReportDataSet>(services),
                    ActivatorUtilities.CreateInstance<MeterReadingsReportDataSet>(services),
                    ActivatorUtilities.CreateInstance<ServiceHistoryReportDataSet>(services),
                ]);
                var table = await source.ReadRowsAsync(new DataSourceQuery
                {
                    DataSet = dataSet,
                    Fields = new HashSet<string>(StringComparer.Ordinal),
                    MaxRows = 100,
                    Context = new DataSourceContext { User = reader },
                }, 100, TestContext.Current.CancellationToken);
                var fields = table.Fields.Select(field => field.Name).ToList();

                return table.Rows.Select(row => fields.Select((field, i) => (field, value: row[i])).ToDictionary(cell => cell.field, cell => cell.value)).ToList();
            }, commit: false);

        var assets = await ReadAsync(AssetsReportDataSet.DataSetName);
        var history = await ReadAsync(ServiceHistoryReportDataSet.DataSetName);

        Assert.Equal(["Boiler", "Pump"], assets.Select(row => (string)row["Name"]).Order());
        Assert.Equal(new[] { boiler.ItemId, pump.ItemId }.Order(StringComparer.Ordinal), history.Select(row => (string)row["AssetId"]).Order(StringComparer.Ordinal));
        Assert.All(history, row => Assert.Equal(workOrder.Number, row["WorkOrderNumber"]));
        Assert.Empty(await ReadAsync(MeterReadingsReportDataSet.DataSetName));
    }

    // Ana arrives at 10:00, in her 09:00–11:00 window, and finishes at 11:30; Ben arrives at 13:30, after his 11:00–13:00
    // window, and can't finish at 14:00, which makes a return visit.
    private static async Task<(ServiceVisit Done, ServiceVisit Incomplete)> TwoJobsAsync(DispatchKit kit)
    {
        var ana = await kit.AddTechnicianAsync("Ana Field");
        var ben = await kit.AddTechnicianAsync("Ben Field");

        await kit.PublishShiftsAsync(ana, ben);
        kit.Now = DispatchKit.Monday(7);

        var (_, first) = await kit.AddJobAsync("No heat", DispatchKit.Depot);
        var (_, second) = await kit.AddJobAsync("Leak", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(first, DispatchKit.Monday(10), ana)).Succeeded);
        Assert.True((await kit.AssignAsync(second, DispatchKit.Monday(12), ben)).Succeeded);

        kit.Now = DispatchKit.Monday(10);
        Assert.True((await kit.MoveAsync(first, VisitStatus.OnSite)).Succeeded);
        kit.Now = DispatchKit.Monday(11, 30);
        Assert.True((await kit.MoveAsync(first, VisitStatus.Completed)).Succeeded);
        kit.Now = DispatchKit.Monday(13, 30);
        Assert.True((await kit.MoveAsync(second, VisitStatus.OnSite)).Succeeded);
        kit.Now = DispatchKit.Monday(14);
        Assert.True((await kit.MoveAsync(second, VisitStatus.Incomplete, "Needs a part.")).Succeeded);

        return (first, second);
    }
}
