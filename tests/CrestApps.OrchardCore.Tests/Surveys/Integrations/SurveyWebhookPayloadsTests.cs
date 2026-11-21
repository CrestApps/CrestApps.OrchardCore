using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Webhooks;

namespace CrestApps.OrchardCore.Tests.Surveys.Integrations;

public sealed class SurveyWebhookPayloadsTests
{
    [Theory]
    [InlineData(AnonymityMode.Anonymous, false)]
    [InlineData(AnonymityMode.Identified, true)]
    public void AResponsePayload_CarriesIdentityOnlyForIdentifiedSurveys(AnonymityMode anonymity, bool hasIdentity)
    {
        // Arrange
        var survey = new Survey { ItemId = "s1", Name = "pulse", Access = new SurveyAccessSettings { Anonymity = anonymity }, AnonymitySecret = "never-sent" };
        var response = new SurveyResponse
        {
            ItemId = "r1",
            SurveyId = "s1",
            UserId = "user1",
            Respondent = new PartyRef(PartyKinds.User, "user1"),
            Context = new CrestApps.OrchardCore.Records.RecordRef("Ticket", "t1"),
            IpAddress = "10.0.0.1",
        };

        // Act
        var payload = SurveyWebhookPayloads.Response(survey, response);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        // Assert
        Assert.Equal(hasIdentity, payload["userId"] is not null);
        Assert.Equal(hasIdentity, payload["respondent"] is not null);
        Assert.Equal("Ticket:t1", payload["context"]);
        Assert.DoesNotContain("10.0.0.1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("never-sent", json, StringComparison.Ordinal);
    }
}
