using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Classes;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Learning.Classes;

/// <summary>
/// Learning plan Phase 6, the session pages: meetings are entered in local time and stored in UTC, and only people who
/// manage classes, or the instructors and assistants of the class's course, manage its sessions.
/// </summary>
public sealed class ClassSessionEditorTests
{
    private const string TimeZoneId = "America/New_York";

    [Fact]
    public async Task Meetings_EnteredInLocalTime_AreStoredInUtc_SoonestFirst()
    {
        // Arrange
        var rows = new List<ClassMeetingRow>
        {
            new() { Date = new DateTime(2026, 12, 8), Start = new TimeSpan(13, 0, 0), End = new TimeSpan(15, 0, 0) },
            new() { Date = new DateTime(2026, 12, 1), Start = new TimeSpan(9, 0, 0), End = new TimeSpan(11, 30, 0) },
            new() { Date = null, Start = new TimeSpan(9, 0, 0), End = new TimeSpan(10, 0, 0) },
        };

        // Act
        var result = await ClassSessionMeetings.ToUtcAsync(LocalClock(), rows);

        // Assert
        Assert.Empty(result.InvalidRows);
        Assert.Collection(
            result.Meetings,
            meeting =>
            {
                Assert.Equal(new DateTime(2026, 12, 1, 14, 0, 0, DateTimeKind.Utc), meeting.StartUtc);
                Assert.Equal(new DateTime(2026, 12, 1, 16, 30, 0, DateTimeKind.Utc), meeting.EndUtc);
            },
            meeting =>
            {
                Assert.Equal(new DateTime(2026, 12, 8, 18, 0, 0, DateTimeKind.Utc), meeting.StartUtc);
                Assert.Equal(new DateTime(2026, 12, 8, 20, 0, 0, DateTimeKind.Utc), meeting.EndUtc);
            });
    }

    [Fact]
    public async Task Meetings_InSummerTime_UseTheSummerOffset()
    {
        // Arrange
        var rows = new List<ClassMeetingRow>
        {
            new() { Date = new DateTime(2026, 7, 1), Start = new TimeSpan(9, 0, 0), End = new TimeSpan(10, 0, 0) },
        };

        // Act
        var result = await ClassSessionMeetings.ToUtcAsync(LocalClock(), rows);

        // Assert
        Assert.Equal(new DateTime(2026, 7, 1, 13, 0, 0, DateTimeKind.Utc), Assert.Single(result.Meetings).StartUtc);
    }

    [Fact]
    public async Task Meetings_ARowThatEndsBeforeItStartsOrHasNoTimes_IsInvalid()
    {
        // Arrange
        var rows = new List<ClassMeetingRow>
        {
            new() { Date = new DateTime(2026, 12, 1), Start = new TimeSpan(9, 0, 0), End = new TimeSpan(10, 0, 0) },
            new() { Date = new DateTime(2026, 12, 2), Start = new TimeSpan(11, 0, 0), End = new TimeSpan(10, 0, 0) },
            new() { Date = new DateTime(2026, 12, 3), Start = null, End = new TimeSpan(10, 0, 0) },
        };

        // Act
        var result = await ClassSessionMeetings.ToUtcAsync(LocalClock(), rows);

        // Assert
        Assert.Equal([1, 2], result.InvalidRows);
        Assert.Single(result.Meetings);
    }

    [Fact]
    public async Task Meetings_StoredInUtc_ShowInLocalTime()
    {
        // Arrange
        var meetings = new[]
        {
            new SchedulingSessionMeeting
            {
                StartUtc = new DateTime(2026, 12, 1, 14, 0, 0, DateTimeKind.Unspecified),
                EndUtc = new DateTime(2026, 12, 1, 16, 30, 0, DateTimeKind.Unspecified),
            },
        };

        // Act
        var rows = await ClassSessionMeetings.ToRowsAsync(LocalClock(), meetings);

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal(new DateTime(2026, 12, 1), row.Date);
        Assert.Equal(new TimeSpan(9, 0, 0), row.Start);
        Assert.Equal(new TimeSpan(11, 30, 0), row.End);
    }

    [Fact]
    public async Task AMeetingThatEndsAfterLocalMidnight_KeepsItsEnd_WhenSavedAgain()
    {
        // Arrange: 22:00 to 01:00 in New York on 1 December.
        var meetings = new[]
        {
            new SchedulingSessionMeeting
            {
                StartUtc = new DateTime(2026, 12, 2, 3, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 12, 2, 6, 0, 0, DateTimeKind.Utc),
            },
        };

        // Act
        var rows = await ClassSessionMeetings.ToRowsAsync(LocalClock(), meetings);
        var saved = await ClassSessionMeetings.ToUtcAsync(LocalClock(), rows);

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal((new DateTime(2026, 12, 1), new TimeSpan(22, 0, 0)), (row.Date, row.Start));
        Assert.Equal((new DateTime(2026, 12, 2), new TimeSpan(1, 0, 0)), (row.EndDate, row.End));
        Assert.Empty(saved.InvalidRows);
        var meeting = Assert.Single(saved.Meetings);
        Assert.Equal((meetings[0].StartUtc, meetings[0].EndUtc), (meeting.StartUtc, meeting.EndUtc));
    }

    [Fact]
    public async Task Scope_ManageLearningClasses_ManagesEveryClass()
    {
        // Arrange
        var courseStructure = new Mock<ICourseStructureService>();
        var scope = new ClassesAdminScope(Authorization(LearningClassesPermissions.ManageLearningClasses.Name), courseStructure.Object);

        // Act
        var allowed = await scope.CanManageAsync(User("ana"), ClassItem());

        // Assert
        Assert.True(allowed);
        courseStructure.Verify(service => service.IsCourseStaffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task Scope_AnInstructorOfTheCourse_ManagesItsClasses()
    {
        // Arrange
        var courseStructure = CourseStructure(staff: true);
        var scope = new ClassesAdminScope(Authorization(LearningPermissions.ManageOwnCourses.Name), courseStructure.Object);

        // Act
        var allowed = await scope.CanManageAsync(User("ana"), ClassItem());

        // Assert
        Assert.True(allowed);
        courseStructure.Verify(
            service => service.IsCourseStaffAsync(
                "ana",
                "course1",
                It.Is<string[]>(roles => roles.Contains(LearningConstants.CourseStaffRoles.Instructor) && roles.Contains(LearningConstants.CourseStaffRoles.Assistant))),
            Times.Once);
    }

    [Fact]
    public async Task Scope_SomeoneNotOnTheStaffOfTheCourse_IsRefused()
    {
        // Arrange
        var scope = new ClassesAdminScope(Authorization(LearningPermissions.ManageOwnCourses.Name), CourseStructure(staff: false).Object);

        // Act
        var allowed = await scope.CanManageAsync(User("ana"), ClassItem());

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task Scope_CourseStaffWithoutACoursePermission_IsRefused()
    {
        // Arrange
        var scope = new ClassesAdminScope(Authorization(), CourseStructure(staff: true).Object);

        // Act
        var allowed = await scope.CanManageAsync(User("ana"), ClassItem());

        // Assert
        Assert.False(allowed);
    }

    private static ILocalClock LocalClock()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var timeZone = new Mock<ITimeZone>();
        timeZone.SetupGet(value => value.TimeZoneId).Returns(TimeZoneId);

        var localClock = new Mock<ILocalClock>();
        localClock
            .Setup(clock => clock.GetLocalTimeZoneAsync())
            .ReturnsAsync(timeZone.Object);
        localClock
            .Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, zone));

        return localClock.Object;
    }

    private static Mock<ICourseStructureService> CourseStructure(bool staff)
    {
        var courseStructure = new Mock<ICourseStructureService>();
        courseStructure
            .Setup(service => service.GetCourseId(It.IsAny<ContentItem>()))
            .Returns("course1");
        courseStructure
            .Setup(service => service.IsCourseStaffAsync(It.IsAny<string>(), "course1", It.IsAny<string[]>()))
            .ReturnsAsync(staff);

        return courseStructure;
    }

    private static IAuthorizationService Authorization(params string[] grants)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorization.Object;
    }

    private static ClaimsPrincipal User(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

    private static ContentItem ClassItem()
        => new()
        {
            ContentItemId = "class1",
            ContentType = ClassesConstants.ContentTypes.Class,
        };
}
