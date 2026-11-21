using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.PersonalData;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;

namespace CrestApps.OrchardCore.Tests.Surveys.Integrations;

public sealed class SurveysPersonalDataContributorTests
{
    [Fact]
    public async Task Export_ListsThePersonsResponsesAndInvitations_AndErasureRemovesWhoGaveThem()
    {
        // Arrange
        var responseId = IdGenerator.GenerateId();
        var otherId = IdGenerator.GenerateId();
        await using var harness = await IntegrationsTestHarness.CreateAsync(services => services.AddScoped<SurveysPersonalDataContributor>());
        await harness.RunAsync(async services =>
        {
            var responses = services.GetRequiredService<SurveyResponseStore>();

            await responses.CreateAsync(new SurveyResponse
            {
                ItemId = responseId,
                SurveyId = "s1",
                Number = "R1",
                UserId = "user-learner1",
                Respondent = new PartyRef(PartyKinds.User, "user-learner1"),
                IpAddress = "10.0.0.1",
                ResumeTokenHash = "hash",
                Status = SurveyResponseStatus.Completed,
                Answers = { ["q1"] = new SurveyAnswer { Values = ["c1"] } },
            });
            await responses.CreateAsync(new SurveyResponse { ItemId = otherId, SurveyId = "s1", UserId = "user-other" });
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation { ItemId = IdGenerator.GenerateId(), SurveyId = "s1", UserId = "user-learner1", Email = "learner@example.com" });
            await services.GetRequiredService<SurveyParticipationStore>().CreateAsync(new SurveyParticipation { ItemId = IdGenerator.GenerateId(), SurveyId = "s1", RespondentKey = "user-learner1", Day = new DateOnly(2026, 11, 1), StartedCount = 1 });
        });

        // Act
        var export = await harness.RunAsync(async services =>
        {
            var result = new PersonalDataExport();
            await services.GetRequiredService<SurveysPersonalDataContributor>().ExportAsync(new PersonalDataExportContext(new PersonalDataSubject("user-learner1"), result));

            return result;
        });

        var erased = await harness.RunAsync(async services =>
        {
            var result = new PersonalDataAnonymizeResult();
            await services.GetRequiredService<SurveysPersonalDataContributor>().AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject("user-learner1"), "[removed]", result));

            return result;
        });

        // Assert
        Assert.Contains(export.Records, record => record.Kind == "SurveyResponse" && record.Id == responseId);
        Assert.Contains(export.Records, record => record.Kind == "SurveyInvitation");
        Assert.DoesNotContain(export.Records, record => record.Id == otherId);
        Assert.Equal(1, erased.Records["SurveyResponse"]);
        Assert.Equal(1, erased.Records["SurveyInvitation"]);
        Assert.Equal(1, erased.Records["SurveyParticipation"]);
        var (response, other, invitations, participation) = await harness.RunAsync(async services => (
            await services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(responseId),
            await services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(otherId),
            await services.GetRequiredService<SurveyInvitationStore>().GetAllAsync(),
            await services.GetRequiredService<SurveyParticipationStore>().GetAllAsync()));
        Assert.Null(response.UserId);
        Assert.Null(response.Respondent);
        Assert.Null(response.IpAddress);
        Assert.Null(response.ResumeTokenHash);
        Assert.Equal(["c1"], response.Answers["q1"].Values);
        Assert.Equal("user-other", other.UserId);
        Assert.Empty(invitations);
        Assert.Empty(participation);
    }
}
