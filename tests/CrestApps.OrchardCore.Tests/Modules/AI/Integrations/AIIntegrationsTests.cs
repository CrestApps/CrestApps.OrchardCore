using System.Net;
using CrestApps.OrchardCore.AI.Core.Integrations;
using CrestApps.OrchardCore.AI.Integrations;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Integrations;

public sealed class AIIntegrationsTests
{
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
        Assert.Contains("drive.readonly", Uri.UnescapeDataString(url), StringComparison.Ordinal);
        Assert.DoesNotContain("calendar", Uri.UnescapeDataString(url), StringComparison.Ordinal);
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
