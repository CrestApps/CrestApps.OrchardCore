using System.Net;
using CrestApps.OrchardCore.AI.Core.Integrations;
using CrestApps.OrchardCore.AI.Integrations;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Integrations;

public sealed class AIIntegrationsTests
{
    [Fact]
    public void FindFreeSlots_OffersWeekdayTimesInsideWorkingHoursThatAreNotBusy()
    {
        // Arrange: Monday 2 November 2026, in UTC, working 9 to 12.
        var monday = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        var busy = new[] { new AICalendarBusy(monday.AddHours(9), monday.AddHours(10)) };

        // Act
        var slots = AICalendarSlots.FindFreeSlots(busy, monday.AddHours(8).AddMinutes(10), monday.AddDays(1), TimeSpan.FromMinutes(30), TimeZoneInfo.Utc, 9, 12);

        // Assert
        Assert.Equal(
            [monday.AddHours(10), monday.AddHours(10.5), monday.AddHours(11), monday.AddHours(11.5)],
            slots);
    }

    [Fact]
    public void FindFreeSlots_SkipsTheWeekend()
    {
        var saturday = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

        var slots = AICalendarSlots.FindFreeSlots([], saturday, saturday.AddDays(2).AddHours(10), TimeSpan.FromMinutes(60), TimeZoneInfo.Utc, 9, 17);

        Assert.Equal([saturday.AddDays(2).AddHours(9)], slots);
    }

    [Fact]
    public async Task GoogleCalendar_ReadsFreeBusy_AndBooksWithTheAttendee()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{ "calendars": { "primary": { "busy": [ { "start": "2026-11-02T09:00:00Z", "end": "2026-11-02T10:00:00Z" } ] } } }""")
            .RespondWith(HttpStatusCode.OK, """{ "id": "evt-1", "htmlLink": "https://calendar.google.com/evt-1" }""");
        var calendar = new GoogleCalendarClient(new HttpClient(handler), null);
        var start = new DateTime(2026, 11, 2, 11, 0, 0, DateTimeKind.Utc);

        // Act
        var busy = await calendar.GetBusyAsync(start.Date, start.Date.AddDays(1), TestContext.Current.CancellationToken);
        var booked = await calendar.BookAsync(new AICalendarAppointment { Title = "Consultation", StartUtc = start, EndUtc = start.AddMinutes(30), AttendeeEmail = "jane@example.com" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new AICalendarBusy(start.Date.AddHours(9), start.Date.AddHours(10)), Assert.Single(busy));
        Assert.Equal("https://www.googleapis.com/calendar/v3/freeBusy", handler.Requests[0].Uri.ToString());
        Assert.Contains("\"id\":\"primary\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.StartsWith("https://www.googleapis.com/calendar/v3/calendars/primary/events", handler.Requests[1].Uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"dateTime\":\"2026-11-02T11:00:00Z\"", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"email\":\"jane@example.com\"", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Equal("evt-1", booked.Id);
    }

    [Fact]
    public async Task MicrosoftCalendar_CountsOnlyBusyEvents()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, """
            { "value": [
                { "showAs": "busy", "start": { "dateTime": "2026-11-02T09:00:00.0000000" }, "end": { "dateTime": "2026-11-02T10:00:00.0000000" } },
                { "showAs": "free", "start": { "dateTime": "2026-11-02T12:00:00.0000000" }, "end": { "dateTime": "2026-11-02T13:00:00.0000000" } }
            ] }
            """);
        var calendar = new MicrosoftCalendarClient(new HttpClient(handler), null);

        // Act
        var busy = await calendar.GetBusyAsync(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc), Assert.Single(busy).StartUtc);
        Assert.StartsWith("https://graph.microsoft.com/v1.0/me/calendarView?", handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HubSpot_FindsByPhone_AndLogsTheCallOnTheContact()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{ "results": [ { "id": "101", "properties": { "firstname": "Jane", "lastname": "Doe", "phone": "+15551234567" } } ] }""")
            .RespondWith(HttpStatusCode.Created, """{ "id": "call-9" }""");
        var crm = new HubSpotCrmClient(new HttpClient(handler));

        // Act
        var contact = await crm.FindByPhoneAsync("+15551234567", TestContext.Current.CancellationToken);
        var callId = await crm.LogCallAsync(contact.Id, new AICrmActivity { Title = "AI call", Notes = "Booked", OccurredUtc = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc), Duration = TimeSpan.FromSeconds(95) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Jane", contact.FirstName);
        Assert.Equal("https://api.hubapi.com/crm/v3/objects/contacts/search", handler.Requests[0].Uri.ToString());
        Assert.Contains("15551234567", System.Text.RegularExpressions.Regex.Unescape(handler.Requests[0].Body), StringComparison.Ordinal);
        Assert.Equal("https://api.hubapi.com/crm/v3/objects/calls", handler.Requests[1].Uri.ToString());
        Assert.Contains("\"hs_call_duration\":\"95000\"", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"associationTypeId\":194", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Equal("call-9", callId);
    }

    [Fact]
    public async Task Salesforce_QueriesBySoql_AndCreatesAContactWithALastName()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{ "records": [] }""")
            .RespondWith(HttpStatusCode.Created, """{ "id": "003xx" }""");
        var crm = new SalesforceCrmClient(new HttpClient(handler), "https://example.my.salesforce.com/");

        // Act
        var found = await crm.FindByPhoneAsync("+1555'1234", TestContext.Current.CancellationToken);
        var id = await crm.SaveAsync(new AICrmContact { Phone = "+15551234" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(found);
        Assert.Contains("Phone = '+1555\\'1234'", Uri.UnescapeDataString(handler.Requests[0].Uri.ToString()), StringComparison.Ordinal);
        Assert.Equal("https://example.my.salesforce.com/services/data/v61.0/sobjects/Contact", handler.Requests[1].Uri.ToString());
        Assert.Contains("\"LastName\":\"+15551234\"", System.Text.RegularExpressions.Regex.Unescape(handler.Requests[1].Body), StringComparison.Ordinal);
        Assert.Equal("003xx", id);
    }

    [Fact]
    public void AuthorizeUrl_AsksGoogleForAnOfflineToken()
    {
        var url = AIIntegrationTokens.BuildAuthorizeUrl(new AIIntegrationConnection { Provider = AIIntegrationProviders.Google, ClientId = "client-1" }, "https://site/callback", "state-1");

        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", url, StringComparison.Ordinal);
        Assert.Contains("client_id=client-1", url, StringComparison.Ordinal);
        Assert.Contains("access_type=offline", url, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=https%3A%2F%2Fsite%2Fcallback", url, StringComparison.Ordinal);
        Assert.Contains("calendar", Uri.UnescapeDataString(url), StringComparison.Ordinal);
    }

    [Fact]
    public void LogToCrm_MapsTheNameAndEmailToTheContact_AndTheRestToCrmFields()
    {
        var contact = new AICrmContact();

        LogToCrmTask.ApplyFields(contact, LogToCrmTask.ParseFields("firstname: Jane\nemail: jane@example.com\nlifecyclestage: lead\nempty:\nno colon"));

        Assert.Equal("Jane", contact.FirstName);
        Assert.Equal("jane@example.com", contact.Email);
        Assert.Equal("lead", contact.Fields["lifecyclestage"]);
        Assert.Single(contact.Fields);
    }
}
