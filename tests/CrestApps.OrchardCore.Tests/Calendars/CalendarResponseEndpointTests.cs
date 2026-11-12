using CrestApps.OrchardCore.Calendars.Invitations;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// SEC-P10c: the Accept, Maybe and Decline links in an invitation recorded the answer on GET, so a mail scanner that
/// opened the links accepted or declined for the attendee. GET now only shows a confirmation form; POST records.
/// </summary>
public sealed class CalendarResponseEndpointTests
{
    private const string Token = "token-jane-0123456789";

    [Fact]
    public async Task OpeningTheLink_ShowsAConfirmation_AndLeavesTheResponseUnchanged()
    {
        // Arrange
        var (manager, calendarEvent) = Manager();

        // Act
        var result = await CalendarResponseEndpoint.ConfirmAsync(calendarEvent.ItemId, Token, "Accepted", manager.Object, Localizer());
        var html = await RenderAsync(result);

        // Assert
        manager.Verify(m => m.RespondAsync(It.IsAny<CalendarEvent>(), It.IsAny<string>(), It.IsAny<CalendarAttendeeResponse>(), It.IsAny<CalendarChangeOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("method=\"post\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmingTheForm_RecordsTheAnswer()
    {
        // Arrange
        var (manager, calendarEvent) = Manager();

        // Act
        await CalendarResponseEndpoint.RespondAsync(calendarEvent.ItemId, Token, "Accepted", manager.Object, Localizer());

        // Assert
        manager.Verify(m => m.RespondAsync(calendarEvent, It.IsAny<string>(), CalendarAttendeeResponse.Accepted, It.IsAny<CalendarChangeOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AWrongToken_IsNotFound_OnBoth()
    {
        var (manager, calendarEvent) = Manager();

        var shown = await CalendarResponseEndpoint.ConfirmAsync(calendarEvent.ItemId, "wrong-token-0123456789", "Accepted", manager.Object, Localizer());
        var recorded = await CalendarResponseEndpoint.RespondAsync(calendarEvent.ItemId, "wrong-token-0123456789", "Accepted", manager.Object, Localizer());

        Assert.Equal(StatusCodes.Status404NotFound, await StatusAsync(shown));
        Assert.Equal(StatusCodes.Status404NotFound, await StatusAsync(recorded));
        manager.Verify(m => m.RespondAsync(It.IsAny<CalendarEvent>(), It.IsAny<string>(), It.IsAny<CalendarAttendeeResponse>(), It.IsAny<CalendarChangeOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<ICalendarEventManager> Manager, CalendarEvent Event) Manager()
    {
        var calendarEvent = new CalendarEvent
        {
            ItemId = "event-1",
            Title = "Planning",
            Attendees = [new CalendarAttendee { Email = "jane@contoso.com", ResponseToken = Token }],
        };
        var manager = new Mock<ICalendarEventManager>();
        manager.Setup(m => m.FindByIdAsync(calendarEvent.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(calendarEvent);
        manager.Setup(m => m.RespondAsync(It.IsAny<CalendarEvent>(), It.IsAny<string>(), It.IsAny<CalendarAttendeeResponse>(), It.IsAny<CalendarChangeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarEvent value, string _, CalendarAttendeeResponse _, CalendarChangeOptions _, CancellationToken _) => CalendarResult<CalendarEvent>.Success(value));

        return (manager, calendarEvent);
    }

    private static IStringLocalizer<CalendarInvitationObserver> Localizer()
        => new PassThroughLocalizer();

    private static async Task<string> RenderAsync(IResult result)
    {
        var httpContext = Context();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Position = 0;

        return await new StreamReader(httpContext.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> StatusAsync(IResult result)
    {
        var httpContext = Context();
        await result.ExecuteAsync(httpContext);

        return httpContext.Response.StatusCode;
    }

    private static DefaultHttpContext Context()
        => new()
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };

    // The observer is internal, so its localizer can't be mocked.
    private sealed class PassThroughLocalizer : IStringLocalizer<CalendarInvitationObserver>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
