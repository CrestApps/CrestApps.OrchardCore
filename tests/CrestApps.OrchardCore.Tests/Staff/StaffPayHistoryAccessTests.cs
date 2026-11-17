using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Drivers;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Reports;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.DataSources;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Staff;

/// <summary>
/// Pay changes in a staff member's history are for people who manage staff pay: the Staff history data set leaves
/// them out for other readers, and the history on the profile shows that pay changed, not what to.
/// </summary>
public sealed class StaffPayHistoryAccessTests
{
    [Fact]
    public async Task HistoryDataSet_LeavesOutPayRows_ForAReaderWhoMayNotManageStaffPay()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddScoped<IStaffReportDataSet, StaffHistoryReportDataSet>());
        var staff = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", member => member.Put(new StaffPayRates { Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2026, 1, 1), HourlyRate = 25m }] }));

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();
            var stored = await manager.FindByIdAsync(staff.ItemId);
            var pay = stored.TryGet<StaffPayRates>(out var rates) ? rates : new StaffPayRates();

            pay.Rates.Add(new StaffPayRate { EffectiveFrom = new DateOnly(2026, 7, 1), HourlyRate = 27m });
            stored.Put(pay);
            stored.JobTitle = "Lead";
            await manager.UpdateAsync(stored);
        });

        var payroll = CalendarTestHarness.User("payroll", StaffPermissions.ViewStaff.Name, StaffPermissions.ManageStaffPay.Name);
        var reader = CalendarTestHarness.User("reader", StaffPermissions.ViewStaff.Name);

        // Act
        var all = await ReadAsync(harness, payroll);
        var limited = await ReadAsync(harness, reader);

        // Assert
        Assert.Contains(all, row => row.Field == StaffHistoryFields.Pay && row.To.Contains("27", StringComparison.Ordinal));
        Assert.Contains(limited, row => row.Field == StaffHistoryFields.JobTitle);
        Assert.DoesNotContain(limited, row => row.Field == StaffHistoryFields.Pay);
        Assert.Equal(all.Count(row => row.Field != StaffHistoryFields.Pay), limited.Count);
    }

    [Fact]
    public async Task HistoryOnTheProfile_ShowsThatPayChanged_ButNotWhatTo_ForAReaderWhoMayNotManageStaffPay()
    {
        var entries = new List<StaffHistoryEntry>
        {
            new() { Field = StaffHistoryFields.Pay, From = "Hourly; 25 from 2026-01-01", To = "Hourly; 27 from 2026-07-01", ChangedUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc) },
            new() { Field = StaffHistoryFields.JobTitle, From = "Agent", To = "Lead", ChangedUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc) },
        };

        var forReader = await HistoryAsync(entries, mayManagePay: false);
        var forPayroll = await HistoryAsync(entries, mayManagePay: true);

        var hidden = Assert.Single(forReader, row => row.Field == StaffHistoryFields.Pay);
        Assert.Null(hidden.From);
        Assert.Null(hidden.To);
        Assert.Equal("Lead", Assert.Single(forReader, row => row.Field == StaffHistoryFields.JobTitle).To);
        Assert.Equal("Hourly; 27 from 2026-07-01", Assert.Single(forPayroll, row => row.Field == StaffHistoryFields.Pay).To);
    }

    private static Task<List<(string Field, string To)>> ReadAsync(CalendarTestHarness harness, ClaimsPrincipal user)
        => harness.RunAsync(async services =>
        {
            var source = ActivatorUtilities.CreateInstance<StaffReportDataSource>(services);
            var table = await source.ReadRowsAsync(new DataSourceQuery
            {
                DataSet = StaffHistoryReportDataSet.DataSetName,
                Fields = new HashSet<string>(StringComparer.Ordinal),
                MaxRows = 100,
                Context = new DataSourceContext { User = user },
            }, 100, TestContext.Current.CancellationToken);
            var fields = table.Fields.Select(field => field.Name).ToList();

            return table.Rows.Select(row => (Convert.ToString(row[fields.IndexOf("Field")]), Convert.ToString(row[fields.IndexOf("To")]) ?? string.Empty)).ToList();
        }, commit: false);

    private static async Task<List<CrestApps.OrchardCore.Staff.ViewModels.StaffHistoryRowViewModel>> HistoryAsync(IReadOnlyList<StaffHistoryEntry> entries, bool mayManagePay)
    {
        var history = new Mock<IStaffHistoryService>();
        var directory = new Mock<IStaffDirectory>();
        var users = new Mock<IUserSearchService>();
        var clock = new Mock<ILocalClock>();
        var authorization = new Mock<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "someone")], "test"));

        history.Setup(service => service.ListAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(entries);
        directory.Setup(service => service.GetNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, string>());
        users.Setup(service => service.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        clock.Setup(service => service.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset utc) => utc);
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                mayManagePay && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == StaffPermissions.ManageStaffPay.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var driver = new StaffProfileDisplayDriver(
            Mock.Of<IStaffHierarchy>(),
            history.Object,
            directory.Object,
            Mock.Of<IStaffScopeService>(),
            users.Object,
            new StaffTeamReader(Mock.Of<IServiceProvider>(), Options.Create(new StaffOptions())),
            clock.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } },
            authorization.Object);

        return await driver.HistoryAsync(new StaffMember { ItemId = "lee" });
    }
}
