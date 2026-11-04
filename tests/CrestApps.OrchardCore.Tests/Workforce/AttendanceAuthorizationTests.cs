using System.Security.Claims;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// A manager's decision on an attendance exception is checked against the exception itself, not against the first page of
/// a list, so exceptions past the first thousand can still be decided, and ones outside the manager's teams can't.
/// </summary>
public sealed class AttendanceAuthorizationTests
{
    private const string ManagedTeam = "team-managed";
    private const string OtherTeam = "team-other";

    [Fact]
    public async Task Decide_AnExceptionPastTheFirstThousand_InTheManagersTeam_IsConfirmed()
    {
        // Arrange: the first page holds a thousand other exceptions of the team; the one decided isn't on it.
        var target = new AttendanceException { ItemId = "exception-1001", TeamId = ManagedTeam };
        var attendance = CreateAttendance(target);
        var controller = CreateController(attendance.Object);

        // Act
        var result = await controller.Decide(target.ItemId, "Confirm", null, null);

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        attendance.Verify(service => service.ConfirmAsync(target.ItemId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Decide_AnExceptionOfATeamTheManagerDoesNotManage_IsNotFound()
    {
        // Arrange
        var target = new AttendanceException { ItemId = "exception-elsewhere", TeamId = OtherTeam };
        var attendance = CreateAttendance(target);
        var controller = CreateController(attendance.Object);

        // Act
        var result = await controller.Decide(target.ItemId, "Confirm", null, null);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        attendance.Verify(service => service.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BulkExcuse_DecidesEveryTickedExceptionInTheManagersTeams_WhereverItIsInTheList()
    {
        // Arrange
        var target = new AttendanceException { ItemId = "exception-1001", TeamId = ManagedTeam };
        var elsewhere = new AttendanceException { ItemId = "exception-elsewhere", TeamId = OtherTeam };
        var attendance = CreateAttendance(target, elsewhere);
        var controller = CreateController(attendance.Object);

        // Act
        await controller.IndexPost(new AttendanceListViewModel(), "Excuse", [target.ItemId, elsewhere.ItemId, "missing"]);

        // Assert
        attendance.Verify(service => service.ExcuseAsync(target.ItemId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        attendance.Verify(service => service.ExcuseAsync(elsewhere.ItemId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        attendance.Verify(service => service.ExcuseAsync("missing", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IAttendanceService> CreateAttendance(params AttendanceException[] exceptions)
    {
        var attendance = new Mock<IAttendanceService>();
        var firstPage = Enumerable.Range(1, 1000)
            .Select(index => new AttendanceException { ItemId = "exception-" + index, TeamId = ManagedTeam })
            .ToArray();

        attendance
            .Setup(service => service.PageAsync(It.IsAny<AttendanceQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((1000 + exceptions.Length, firstPage));

        foreach (var exception in exceptions)
        {
            attendance.Setup(service => service.FindByIdAsync(exception.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(exception);
        }

        attendance
            .Setup(service => service.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanningResult<AttendanceException>());
        attendance
            .Setup(service => service.ExcuseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanningResult<AttendanceException>());

        return attendance;
    }

    private static AttendanceController CreateController(IAttendanceService attendance)
    {
        var authorization = new Mock<IAuthorizationService>();

        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var scopes = new Mock<IWorkforceScopeService>();

        scopes
            .Setup(service => service.GetManagedScopeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkforceScope(false, new HashSet<string>([ManagedTeam], StringComparer.Ordinal)));

        var htmlLocalizer = new Mock<IHtmlLocalizer<AttendanceController>>();

        htmlLocalizer.Setup(localizer => localizer[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
        htmlLocalizer.Setup(localizer => localizer[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedHtmlString(name, name));

        var controller = new AttendanceController(
            authorization.Object,
            attendance,
            Mock.Of<IWorkerManager>(),
            Mock.Of<ITeamManager>(),
            scopes.Object,
            Mock.Of<INotifier>(),
            htmlLocalizer.Object,
            Mock.Of<IStringLocalizer<AttendanceController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CalendarTestHarnessUser() },
            },
            Url = Mock.Of<IUrlHelper>(),
        };

        return controller;
    }

    private static ClaimsPrincipal CalendarTestHarnessUser()
    {
        return Calendars.CalendarTestHarness.User("manager");
    }
}
