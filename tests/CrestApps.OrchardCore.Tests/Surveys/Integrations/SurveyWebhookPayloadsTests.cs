using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.Webhooks;
using CrestApps.OrchardCore.Surveys.Workflows;
using Moq;

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
        // R8-Mi19: an anonymous response carries the context's kind only, never its id.
        Assert.Equal(hasIdentity ? "Ticket:t1" : "Ticket", payload["context"]);
        Assert.DoesNotContain("10.0.0.1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("never-sent", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAnonymousResponsePayload_CarriesDayOnlyTimesAndNoAttemptNumber()
    {
        // Arrange: R6-L12.
        var survey = new Survey { ItemId = "s1", Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous } };
        var response = new SurveyResponse
        {
            ItemId = "r1",
            SurveyId = "s1",
            AttemptNumber = 2,
            StartedUtc = new DateTime(2026, 11, 10, 10, 31, 2, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 11, 10, 10, 41, 9, DateTimeKind.Utc),
        };

        // Act
        var payload = SurveyWebhookPayloads.Response(survey, response);
        var input = CrestApps.OrchardCore.Surveys.Workflows.SurveyWorkflowInputBuilder.DayOf(response.CompletedUtc);

        // Assert
        Assert.Null(payload["attemptNumber"]);
        Assert.Equal(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), payload["startedUtc"]);
        Assert.Equal(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), payload["completedUtc"]);
        Assert.Equal(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), input);
    }

    [Fact]
    public void AnAnonymousResponsePayload_ReadsTheSitesDay_AndHidesAGroupTooSmallToShow()
    {
        // Arrange: R7-Mi17 and R7-Mi20, a response on a link sent to one person, on a site at UTC+10.
        var survey = new Survey { ItemId = "s1", Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous } };
        var response = new SurveyResponse
        {
            ItemId = "r1",
            SurveyId = "s1",
            CollectorId = "for-bob",
            VersionNumber = 2,
            Context = new CrestApps.OrchardCore.Records.RecordRef("Ticket", "t1"),
            StartedUtc = new DateTime(2026, 11, 10, 15, 31, 2, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 11, 10, 15, 41, 9, DateTimeKind.Utc),
        };
        var zone = TimeZoneInfo.CreateCustomTimeZone("Site", TimeSpan.FromHours(10), "Site", "Site");
        var hidden = new SurveyRowAttributes(true, false, false, false);
        var context = new SurveyResponseEventContext { Survey = survey, Response = response };

        // Act
        var payload = SurveyWebhookPayloads.Response(survey, response, hidden, zone);
        var input = new SurveyWorkflowInputBuilder(new Mock<ISurveyControlRegistry>().Object).ForResponse(context, hidden, zone);

        // Assert: 15:31 UTC is 01:31 on 11 November at the site, whose day starts at 14:00 UTC on the 10th (the UTC day
        // would split it); the collector, version and context of a group too small to show are left out.
        Assert.Equal(new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc), payload["startedUtc"]);
        Assert.Null(payload["collectorId"]);
        Assert.Null(payload["versionNumber"]);
        Assert.Null(payload["context"]);
        Assert.Null(input[SurveyWorkflowInput.CollectorId]);
        Assert.Null(input["VersionNumber"]);
        Assert.Null(input[SurveyWorkflowInput.ContextKind]);
        Assert.Equal(new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc).ToString("O", System.Globalization.CultureInfo.InvariantCulture), input["CompletedUtc"]);
    }

    [Fact]
    public void AnExportedAnonymousResponse_IsCutToTheSitesDay()
    {
        // Arrange: R7-Mi17, the export uses the same day as every other output, on a site at UTC+10.
        var node = new System.Text.Json.Nodes.JsonObject { ["CompletedUtc"] = new DateTime(2026, 11, 10, 15, 41, 9, DateTimeKind.Utc) };

        // Act
        SurveysRecipeExporter.DaysOnly(node, TimeZoneInfo.CreateCustomTimeZone("Site", TimeSpan.FromHours(10), "Site", "Site"));

        // Assert
        Assert.Equal(new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc), node["CompletedUtc"]!.GetValue<DateTime>());
    }
}
