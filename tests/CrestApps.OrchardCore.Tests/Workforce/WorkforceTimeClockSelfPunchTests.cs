using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The time clock's punches for Time Tracker's <c>clockMyTime</c>: the same checks as the My Work punch buttons (My Work
/// and Clock in and out permissions, a worker), then the time clock's own rules.
/// </summary>
public sealed class WorkforceTimeClockSelfPunchTests
{
    [Fact]
    public async Task Punch_RecordsAPunchForTheCallersOwnWorker()
    {
        var timeClock = new Mock<ITimeClockService>();
        var worker = new StaffMember { ItemId = "w1", UserId = "ana" };

        timeClock.Setup(clock => clock.PunchAsync(It.IsAny<TimePunchRequest>(), "ana", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimePunchRequest request, string _, CancellationToken _) => new PlanningResult<TimePunch> { Value = new TimePunch { WorkerId = request.WorkerId, Kind = request.Kind, Utc = CalendarTestHarness.Now } });
        timeClock.Setup(clock => clock.GetStatusAsync(worker, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimeClockStatus { TimeZoneId = "America/Chicago" });

        var punches = Create(timeClock.Object, worker, allowed: true);

        var result = await punches.PunchAsync(User(), "breakstart", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("BreakStart", result.Punch);
        Assert.Equal("America/Chicago", result.TimeZoneId);
        timeClock.Verify(clock => clock.PunchAsync(It.Is<TimePunchRequest>(request => request.WorkerId == "w1" && request.Kind == TimePunchKind.BreakStart && request.Source == TimePunchSource.Web), "ana", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Punch_IsRefusedWithoutThePermissions_AndForAnUnknownPunch()
    {
        var timeClock = new Mock<ITimeClockService>();
        var worker = new StaffMember { ItemId = "w1", UserId = "ana" };

        var denied = await Create(timeClock.Object, worker, allowed: false).PunchAsync(User(), "In", TestContext.Current.CancellationToken);
        var unknown = await Create(timeClock.Object, worker, allowed: true).PunchAsync(User(), "2", TestContext.Current.CancellationToken);
        var state = await Create(timeClock.Object, null, allowed: true).GetStateAsync(User(), TestContext.Current.CancellationToken);

        Assert.StartsWith("You aren't allowed to clock in and out here", Assert.Single(denied.Errors), StringComparison.Ordinal);
        Assert.StartsWith("Choose a punch:", Assert.Single(unknown.Errors), StringComparison.Ordinal);
        Assert.Equal("You aren't a worker, so you have no time clock to punch.", state.Refusal);
        timeClock.Verify(clock => clock.PunchAsync(It.IsAny<TimePunchRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TimeClockSelfPunch Create(ITimeClockService timeClock, StaffMember worker, bool allowed)
    {
        var authorization = new Mock<IAuthorizationService>();
        var workers = new Mock<IStaffMemberManager>();

        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        workers.Setup(manager => manager.FindByUserIdAsync("ana", It.IsAny<CancellationToken>())).ReturnsAsync(worker);

        return new TimeClockSelfPunch(authorization.Object, workers.Object, timeClock, new PassThroughStringLocalizer<TimeClockSelfPunch>());
    }

    private static ClaimsPrincipal User()
        => CalendarTestHarness.User("ana", WorkforcePermissions.UseWorkforcePortal.Name, WorkforcePermissions.PunchTime.Name);
}
