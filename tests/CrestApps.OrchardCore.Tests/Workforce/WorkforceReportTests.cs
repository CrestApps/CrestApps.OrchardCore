using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The Workforce report data source: its data sets are listed to those with the permission, and their rows are
/// limited to the teams the reader manages, with workers' and teams' names.
/// </summary>
public sealed class WorkforceReportTests
{
    private static readonly DateOnly _monday = TimeClockKit.Monday;

    [Fact]
    public async Task Punches_AreLimitedToTheTeamsTheReaderManages()
    {
        await using var kit = await TimeClockKit.CreateAsync(services => services.AddScoped<IWorkforceReportDataSet, TimePunchesReportDataSet>());
        var east = await kit.AddTeamAsync("East");
        var west = await kit.AddTeamAsync("West");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", east);
        var ben = await PlanningKit.AddMemberAsync(kit.Harness, "Ben", west);

        await WorkforceTests.AddWorkerAsync(kit.Harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = east.ItemId, Role = TeamMemberRole.Manager });
        });
        await kit.WorkAsync(ana, _monday, 9, 17);
        await kit.WorkAsync(ben, _monday, 10, 18);

        var manager = CalendarTestHarness.User("maya", WorkforcePermissions.ApproveTimesheets.Name);
        var everyone = CalendarTestHarness.User("admin", WorkforcePermissions.ApproveTimesheets.Name, WorkforcePermissions.ManageAllWorkforce.Name);
        var nobody = CalendarTestHarness.User("someone");

        var managerRows = await ReadAsync(kit, manager);
        var everyoneRows = await ReadAsync(kit, everyone);
        var nobodyRows = await ReadAsync(kit, nobody);
        var listed = await kit.Harness.RunAsync(services => ActivatorUtilities.CreateInstance<WorkforceReportDataSource>(services).GetDataSetsAsync(new DataSourceContext { User = nobody }), commit: false);

        Assert.Equal(["Ana:East", "Ana:East"], managerRows);
        Assert.Equal(["Ana:East", "Ana:East", "Ben:West", "Ben:West"], everyoneRows.Order(StringComparer.Ordinal));
        Assert.Empty(nobodyRows);
        Assert.Empty(listed);
    }

    [Fact]
    public async Task WorkersAndScheduleChanges_AreListed_ForTheReadersTeams()
    {
        await using var kit = await TimeClockKit.CreateAsync(services =>
        {
            services.AddScoped<IWorkforceReportDataSet, WorkersReportDataSet>();
            services.AddScoped<IWorkforceReportDataSet, ScheduleChangesReportDataSet>();
        });
        var east = await kit.AddTeamAsync("East");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", east);
        var ben = await PlanningKit.AddMemberAsync(kit.Harness, "Ben", east);
        var period = await PlanningKit.CreatePeriodAsync(kit.Harness, east);

        await PlanningKit.AddDraftAsync(kit.Harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(kit.Harness, period);

        var shift = Assert.Single(await PlanningKit.ListShiftsAsync(kit.Harness, east));

        await kit.Harness.RunAsync(services => services.GetRequiredService<CrestApps.OrchardCore.Workforce.Services.IShiftService>().ReassignAsync(shift.ItemId, ben.ItemId, ScheduleChangeInitiator.Worker, "Swapped days", "manager"));

        var reader = CalendarTestHarness.User("admin", WorkforcePermissions.ViewWorkers.Name, WorkforcePermissions.ManageSchedules.Name, WorkforcePermissions.ManageAllWorkforce.Name);
        var workers = await ReadFieldsAsync(kit, reader, WorkersReportDataSet.DataSetName, "WorkerName", "TeamName");
        var changes = await ReadFieldsAsync(kit, reader, ScheduleChangesReportDataSet.DataSetName, "Kind", "WorkerName", "WorkerAsked", "Reason");

        Assert.Equal(["Ana|East", "Ben|East"], workers.Order(StringComparer.Ordinal));
        Assert.Equal(["Reassigned|Ben|True|Swapped days"], changes);
    }

    private static Task<string[]> ReadFieldsAsync(TimeClockKit kit, System.Security.Claims.ClaimsPrincipal user, string dataSet, params string[] names)
    {
        return kit.Harness.RunAsync(async services =>
        {
            var source = ActivatorUtilities.CreateInstance<WorkforceReportDataSource>(services);
            var table = await source.ReadRowsAsync(new DataSourceQuery
            {
                DataSet = dataSet,
                Fields = new HashSet<string>(StringComparer.Ordinal),
                MaxRows = 100,
                Context = new DataSourceContext { User = user },
            }, 100, TestContext.Current.CancellationToken);
            var fields = table.Fields.Select(field => field.Name).ToList();

            return table.Rows.Select(row => string.Join('|', names.Select(name => Convert.ToString(row[fields.IndexOf(name)], System.Globalization.CultureInfo.InvariantCulture)))).ToArray();
        }, commit: false);
    }

    private static Task<string[]> ReadAsync(TimeClockKit kit, System.Security.Claims.ClaimsPrincipal user)
    {
        return kit.Harness.RunAsync(async services =>
        {
            var source = ActivatorUtilities.CreateInstance<WorkforceReportDataSource>(services);
            var table = await source.ReadRowsAsync(new DataSourceQuery
            {
                DataSet = TimePunchesReportDataSet.DataSetName,
                Fields = new HashSet<string>(StringComparer.Ordinal),
                MaxRows = 100,
                Context = new DataSourceContext { User = user },
            }, 100, TestContext.Current.CancellationToken);
            var worker = table.Fields.ToList().FindIndex(field => field.Name == "WorkerName");
            var team = table.Fields.ToList().FindIndex(field => field.Name == "TeamName");

            return table.Rows.Select(row => $"{row[worker]}:{row[team]}").ToArray();
        }, commit: false);
    }
}
