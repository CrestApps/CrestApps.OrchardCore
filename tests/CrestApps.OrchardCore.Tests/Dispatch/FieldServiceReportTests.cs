using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Reports;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Workforce;
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
