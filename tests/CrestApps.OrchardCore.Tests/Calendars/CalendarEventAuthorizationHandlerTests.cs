using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// The broad calendar event permissions are granted through the Own ones for the user's own schedule and events, and through
/// calendar sharing for events on calendars the user can read.
/// </summary>
public sealed class CalendarEventAuthorizationHandlerTests
{
    private const string Me = "user-me";
    private const string Other = "user-other";

    private static readonly Calendar _myCalendar = new() { ItemId = "cal-mine", OwnerKind = CalendarOwnerKind.User, OwnerId = Me };
    private static readonly Calendar _otherCalendar = new() { ItemId = "cal-other", OwnerKind = CalendarOwnerKind.User, OwnerId = Other };

    [Fact]
    public async Task ListOwn_GrantsListingOnlyTheUsersOwnSchedule()
    {
        // Arrange
        var handler = CreateHandler([CalendarPermissions.ListOwnCalendarEvents]);
        var own = CreateContext(CalendarPermissions.ListCalendarEvents, new CalendarSchedule(Me));
        var someoneElse = CreateContext(CalendarPermissions.ListCalendarEvents, new CalendarSchedule(Other));
        var everyone = CreateContext(CalendarPermissions.ListCalendarEvents, CalendarSchedule.Everyone);

        // Act
        await handler.HandleAsync(own);
        await handler.HandleAsync(someoneElse);
        await handler.HandleAsync(everyone);

        // Assert
        Assert.True(own.HasSucceeded);
        Assert.False(someoneElse.HasSucceeded);
        Assert.False(everyone.HasSucceeded);
    }

    [Fact]
    public async Task ListOwn_IsNeededForTheOwnSchedule()
    {
        // Arrange
        var context = CreateContext(CalendarPermissions.ListCalendarEvents, new CalendarSchedule(Me));

        // Act
        await CreateHandler([]).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [InlineData("created")]
    [InlineData("attending")]
    [InlineData("my-calendar")]
    public async Task ViewOwn_GrantsTheUsersOwnEvents(string how)
    {
        // Arrange
        var calendarEvent = new CalendarEvent { ItemId = "e1", CalendarId = how == "my-calendar" ? _myCalendar.ItemId : _otherCalendar.ItemId };

        if (how == "created")
        {
            calendarEvent.CreatedBy = Me;
        }
        else if (how == "attending")
        {
            calendarEvent.Attendees.Add(new CalendarAttendee { UserId = Me });
        }

        var context = CreateContext(CalendarPermissions.ViewCalendarEvents, calendarEvent);

        // Act
        await CreateHandler([CalendarPermissions.ViewOwnCalendarEvents]).HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task ViewOwn_DoesNotGrantSomeoneElsesEvent()
    {
        // Arrange
        var context = CreateContext(CalendarPermissions.ViewCalendarEvents, new CalendarEvent { ItemId = "e1", CalendarId = _otherCalendar.ItemId, CreatedBy = Other });

        // Act
        await CreateHandler([CalendarPermissions.ViewOwnCalendarEvents]).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ReadAccessToTheCalendar_GrantsItsEvents_ButNotPrivateOnes()
    {
        // Arrange: the other calendar is shared with the user to read.
        var handler = CreateHandler([], CalendarAccessLevel.Read);
        var shared = CreateContext(CalendarPermissions.ViewCalendarEvents, new CalendarEvent { ItemId = "e1", CalendarId = _otherCalendar.ItemId });
        var hidden = CreateContext(CalendarPermissions.ViewCalendarEvents, new CalendarEvent { ItemId = "e2", CalendarId = _otherCalendar.ItemId, Visibility = CalendarEventVisibility.Private });

        // Act
        await handler.HandleAsync(shared);
        await handler.HandleAsync(hidden);

        // Assert
        Assert.True(shared.HasSucceeded);
        Assert.False(hidden.HasSucceeded);
    }

    [Fact]
    public async Task FreeBusyAccess_DoesNotGrantEventDetails()
    {
        // Arrange
        var context = CreateContext(CalendarPermissions.ViewCalendarEvents, new CalendarEvent { ItemId = "e1", CalendarId = _otherCalendar.ItemId });

        // Act
        await CreateHandler([], CalendarAccessLevel.FreeBusy).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public void OwnPermissionsImplyNothingBroader()
    {
        // Assert: the Own permissions come with using calendars and with the broad permission, never the other way around.
        Assert.Contains(CalendarPermissions.ListCalendarEvents, CalendarPermissions.ListOwnCalendarEvents.ImpliedBy);
        Assert.Contains(CalendarPermissions.AccessCalendars, CalendarPermissions.ListOwnCalendarEvents.ImpliedBy);
        Assert.Contains(CalendarPermissions.ViewCalendarEvents, CalendarPermissions.ViewOwnCalendarEvents.ImpliedBy);
        Assert.Contains(CalendarPermissions.ViewAllCalendars, CalendarPermissions.ListCalendarEvents.ImpliedBy);
        Assert.Contains(CalendarPermissions.ViewAllCalendars, CalendarPermissions.ViewCalendarEvents.ImpliedBy);
        Assert.DoesNotContain(CalendarPermissions.ListOwnCalendarEvents, CalendarPermissions.ListCalendarEvents.ImpliedBy);
    }

    private static CalendarEventAuthorizationHandler CreateHandler(Permission[] granted, CalendarAccessLevel otherCalendarLevel = CalendarAccessLevel.None)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => granted.Any(permission => permission.Name == requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var calendarService = new Mock<ICalendarService>();

        calendarService.Setup(service => service.FindByIdAsync(_myCalendar.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(_myCalendar);
        calendarService.Setup(service => service.FindByIdAsync(_otherCalendar.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(_otherCalendar);

        var accessService = new Mock<ICalendarAccessService>();

        accessService.Setup(service => service.GetAccessLevelAsync(It.IsAny<ClaimsPrincipal>(), _myCalendar)).ReturnsAsync(CalendarAccessLevel.Manage);
        accessService.Setup(service => service.GetAccessLevelAsync(It.IsAny<ClaimsPrincipal>(), _otherCalendar)).ReturnsAsync(otherCalendarLevel);

        return new CalendarEventAuthorizationHandler(
            new Lazy<IAuthorizationService>(() => authorizationService.Object),
            new Lazy<ICalendarService>(() => calendarService.Object),
            new Lazy<ICalendarAccessService>(() => accessService.Object));
    }

    private static AuthorizationHandlerContext CreateContext(Permission permission, object resource)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me)], "test"));

        return new AuthorizationHandlerContext([new PermissionRequirement(permission)], user, resource);
    }
}
