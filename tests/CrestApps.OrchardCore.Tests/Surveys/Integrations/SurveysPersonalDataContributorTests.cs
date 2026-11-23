using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
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
                RespondentKey = "u:user-learner1",
                Status = SurveyResponseStatus.InProgress,
                Answers = { ["q1"] = new SurveyAnswer { Values = ["c1"] } },
            });
            await responses.CreateAsync(new SurveyResponse { ItemId = otherId, SurveyId = "s1", UserId = "user-other" });
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation { ItemId = IdGenerator.GenerateId(), SurveyId = "s1", UserId = "user-learner1", Email = "learner@example.com" });
            await services.GetRequiredService<SurveyParticipationStore>().CreateAsync(new SurveyParticipation { ItemId = IdGenerator.GenerateId(), SurveyId = "s1", RespondentKey = "u:user-learner1", Day = new DateOnly(2026, 11, 1), StartedCount = 1 });
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
        Assert.Null(response.RespondentKey);
        Assert.Equal(["c1"], response.Answers["q1"].Values);
        Assert.Equal("user-other", other.UserId);
        Assert.Empty(invitations);
        Assert.Empty(participation);
    }

    [Fact]
    public async Task Erasure_FindsParticipationByEveryRealKeyFormat_AndClearsContextVariablesAndProperties()
    {
        // Arrange
        const string userId = "user-learner1";
        var responseId = IdGenerator.GenerateId();
        var invitation = new SurveyInvitation { ItemId = IdGenerator.GenerateId(), SurveyId = "anon", UserId = userId, Email = "learner@example.com" };
        string anonymousKey = null;
        string invitationKey = null;
        string userKey = null;
        await using var harness = await IntegrationsTestHarness.CreateAsync(services => services.AddScoped<SurveysPersonalDataContributor>());
        await harness.RunAsync(async services =>
        {
            var surveys = services.GetRequiredService<SurveyStore>();
            var secrets = services.GetRequiredService<SurveyAnonymitySecrets>();
            var anonymous = IntegrationsTestHarness.NewSurvey("anon", "anon", AnonymityMode.Anonymous);
            secrets.Assign(anonymous);
            await surveys.CreateAsync(anonymous);

            // The keys participation is really stored under: the HMAC of the user on an anonymous survey, the HMAC of
            // the person's invitation, and the plain key of identified surveys.
            anonymousKey = await secrets.RespondentKeyAsync(anonymous, new SurveyRespondentIdentity { UserId = userId }, null);
            invitationKey = await secrets.RespondentKeyAsync(anonymous, new SurveyRespondentIdentity(), invitation);
            userKey = SurveyRespondentKeys.For(IntegrationsTestHarness.NewSurvey("open", "open"), null, new SurveyRespondentIdentity { UserId = userId }, null);

            var participation = services.GetRequiredService<SurveyParticipationStore>();
            await participation.CreateAsync(new SurveyParticipation { ItemId = IdGenerator.GenerateId(), SurveyId = "anon", RespondentKey = anonymousKey, Day = new DateOnly(2026, 11, 1), StartedCount = 1 });
            await participation.CreateAsync(new SurveyParticipation { ItemId = IdGenerator.GenerateId(), SurveyId = "anon", RespondentKey = invitationKey, Day = new DateOnly(2026, 11, 1), StartedCount = 1 });
            await participation.CreateAsync(new SurveyParticipation { ItemId = IdGenerator.GenerateId(), SurveyId = "open", RespondentKey = userKey, Day = new DateOnly(2026, 11, 1), StartedCount = 1 });
            await participation.CreateAsync(new SurveyParticipation { ItemId = IdGenerator.GenerateId(), SurveyId = "open", RespondentKey = "u:user-other", Day = new DateOnly(2026, 11, 1), StartedCount = 1 });
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(invitation);

            var response = new SurveyResponse
            {
                ItemId = responseId,
                SurveyId = "open",
                Number = "R1",
                UserId = userId,
                Context = new CrestApps.OrchardCore.Records.RecordRef("LearningEnrollment", "enrollment-1"),
                Status = SurveyResponseStatus.Completed,
            };
            response.Variables["email"] = "learner@example.com";
            response.Properties["crm"] = "contact-1";
            await services.GetRequiredService<SurveyResponseStore>().CreateAsync(response);
        });

        // Act
        var erased = await harness.RunAsync(async services =>
        {
            var result = new PersonalDataAnonymizeResult();
            await services.GetRequiredService<SurveysPersonalDataContributor>().AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject(userId), "[removed]", result));

            return result;
        });

        // Assert
        Assert.StartsWith("a:", anonymousKey, StringComparison.Ordinal);
        Assert.StartsWith("a:", invitationKey, StringComparison.Ordinal);
        Assert.Equal("u:" + userId, userKey);
        Assert.Equal(3, erased.Records["SurveyParticipation"]);
        var (left, response) = await harness.RunAsync(async services => (
            await services.GetRequiredService<SurveyParticipationStore>().GetAllAsync(),
            await services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(responseId)));
        Assert.Equal("u:user-other", Assert.Single(left).RespondentKey);
        Assert.Null(response.Context);
        Assert.Empty(response.Variables);
        Assert.Empty(response.Properties);
    }

    [Fact]
    public async Task ExportAndErasure_FindGuestEmailInvitations_TheirParticipationKeysAndLinkedResponses()
    {
        // Arrange: a guest (no user account) was invited by e-mail and answered an identified survey through the link.
        const string email = "Guest@Example.com";
        var responseId = IdGenerator.GenerateId();
        var otherInvitationId = IdGenerator.GenerateId();
        var invitation = new SurveyInvitation { ItemId = IdGenerator.GenerateId(), SurveyId = "open", Email = email, Status = SurveyInvitationStatus.Completed };
        string invitationKey = null;
        await using var harness = await IntegrationsTestHarness.CreateAsync(services => services.AddScoped<SurveysPersonalDataContributor>());
        await harness.RunAsync(async services =>
        {
            var survey = IntegrationsTestHarness.NewSurvey("open", "open");
            await services.GetRequiredService<SurveyStore>().CreateAsync(survey);
            invitationKey = SurveyRespondentKeys.For(survey, null, new SurveyRespondentIdentity(), invitation);

            var invitations = services.GetRequiredService<SurveyInvitationStore>();
            await invitations.CreateAsync(invitation);
            await invitations.CreateAsync(new SurveyInvitation { ItemId = otherInvitationId, SurveyId = "open", Email = "someone@example.com" });

            var participation = services.GetRequiredService<SurveyParticipationStore>();
            await participation.CreateAsync(new SurveyParticipation { ItemId = IdGenerator.GenerateId(), SurveyId = "open", RespondentKey = invitationKey, Day = new DateOnly(2026, 11, 1), CompletedCount = 1 });

            var response = new SurveyResponse
            {
                ItemId = responseId,
                SurveyId = "open",
                Number = "R1",
                InvitationId = invitation.ItemId,
                IpAddress = "10.0.0.2",
                Status = SurveyResponseStatus.Completed,
                Answers = { ["q1"] = new SurveyAnswer { Values = ["c1"] } },
            };
            response.Variables["email"] = email;
            await services.GetRequiredService<SurveyResponseStore>().CreateAsync(response);
        });

        // Act: the person is known by their e-mail address only, typed in another case.
        var export = await harness.RunAsync(async services =>
        {
            var result = new PersonalDataExport();
            await services.GetRequiredService<SurveysPersonalDataContributor>().ExportAsync(new PersonalDataExportContext(new PersonalDataSubject(email: "guest@example.com"), result));

            return result;
        });

        var erased = await harness.RunAsync(async services =>
        {
            var result = new PersonalDataAnonymizeResult();
            await services.GetRequiredService<SurveysPersonalDataContributor>().AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject(email: "guest@example.com"), "[removed]", result));

            return result;
        });

        // Assert
        Assert.Contains(export.Records, record => record.Kind == "SurveyInvitation" && record.Id == invitation.ItemId);
        Assert.Contains(export.Records, record => record.Kind == "SurveyResponse" && record.Id == responseId);
        Assert.DoesNotContain(export.Records, record => record.Id == otherInvitationId);
        Assert.Equal(1, erased.Records["SurveyInvitation"]);
        Assert.Equal(1, erased.Records["SurveyResponse"]);
        Assert.Equal(1, erased.Records["SurveyParticipation"]);
        var (response, invitationsLeft, participationLeft) = await harness.RunAsync(async services => (
            await services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(responseId),
            await services.GetRequiredService<SurveyInvitationStore>().GetAllAsync(),
            await services.GetRequiredService<SurveyParticipationStore>().GetAllAsync()));
        Assert.Null(response.InvitationId);
        Assert.Null(response.IpAddress);
        Assert.Empty(response.Variables);
        Assert.Equal(["c1"], response.Answers["q1"].Values);
        Assert.Equal(otherInvitationId, Assert.Single(invitationsLeft).ItemId);
        Assert.Empty(participationLeft);
    }

    [Fact]
    public async Task ExportAndErasure_WithInvitationUniqueness_FindTheSignedInInviteesParticipationThroughTheirResponse()
    {
        // Arrange: Uniqueness is Invitation, so a signed-in invitee counts by i:{invitation}. The invitation went to an
        // address that is not on the person's account, so only their response leads to it.
        const string userId = "user-learner1";
        var responseId = IdGenerator.GenerateId();
        var invitation = new SurveyInvitation { ItemId = IdGenerator.GenerateId(), SurveyId = "byinvite", Email = "team-inbox@example.com", Status = SurveyInvitationStatus.Completed };
        string invitationKey = null;
        await using var harness = await IntegrationsTestHarness.CreateAsync(services => services.AddScoped<SurveysPersonalDataContributor>());
        await harness.RunAsync(async services =>
        {
            var survey = IntegrationsTestHarness.NewSurvey("byinvite", "byinvite");
            survey.Access.Limits = new RespondentLimits { Uniqueness = UniquenessMode.Invitation };
            await services.GetRequiredService<SurveyStore>().CreateAsync(survey);
            invitationKey = SurveyRespondentKeys.For(survey, null, new SurveyRespondentIdentity { UserId = userId }, invitation);

            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(invitation);
            await services.GetRequiredService<SurveyParticipationStore>().CreateAsync(new SurveyParticipation { ItemId = IdGenerator.GenerateId(), SurveyId = "byinvite", RespondentKey = invitationKey, Day = new DateOnly(2026, 11, 1), CompletedCount = 1 });
            await services.GetRequiredService<SurveyResponseStore>().CreateAsync(new SurveyResponse
            {
                ItemId = responseId,
                SurveyId = "byinvite",
                Number = "R1",
                UserId = userId,
                InvitationId = invitation.ItemId,
                Status = SurveyResponseStatus.Completed,
            });
        });

        // Act
        var export = await harness.RunAsync(async services =>
        {
            var result = new PersonalDataExport();
            await services.GetRequiredService<SurveysPersonalDataContributor>().ExportAsync(new PersonalDataExportContext(new PersonalDataSubject(userId), result));

            return result;
        });

        var erased = await harness.RunAsync(async services =>
        {
            var result = new PersonalDataAnonymizeResult();
            await services.GetRequiredService<SurveysPersonalDataContributor>().AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject(userId), "[removed]", result));

            return result;
        });

        // Assert: the participation is exported and erased; the invitation, addressed to someone else, is neither.
        Assert.Equal("i:" + invitation.ItemId, invitationKey);
        var record = Assert.Single(export.Records, item => item.Kind == "SurveyParticipation");
        Assert.Equal("byinvite", record.Fields["Survey"]);
        Assert.Equal("1", record.Fields["Completed"]);
        Assert.DoesNotContain(export.Records, item => item.Kind == "SurveyInvitation");
        Assert.Equal(1, erased.Records["SurveyParticipation"]);
        Assert.False(erased.Records.ContainsKey("SurveyInvitation"));
        var (response, participationLeft) = await harness.RunAsync(async services => (
            await services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(responseId),
            await services.GetRequiredService<SurveyParticipationStore>().GetAllAsync()));
        Assert.Null(response.UserId);
        Assert.Null(response.InvitationId);
        Assert.Empty(participationLeft);
    }

    [Fact]
    public async Task Erasure_OfAResponseInProgress_ClearsItsKey_SoTheDeadlineCompletionWritesNoParticipationBack()
    {
        // Arrange: a timed survey the person started while signed in; the start is counted under u:{id}.
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddScoped<SurveysPersonalDataContributor>());
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));
        definition.Timing.TimeLimitMinutes = 1;

        var survey = await harness.PublishAsync(definition);
        var started = (await harness.StartAsync(survey, "user-learner1")).Response;
        var keyBefore = (await harness.FindAsync(started.ItemId)).RespondentKey;

        // Act: the person's data is erased, then the deadline passes and upkeep submits the response.
        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveysPersonalDataContributor>().AnonymizeAsync(new PersonalDataAnonymizeContext(new PersonalDataSubject("user-learner1"), "[removed]", new PersonalDataAnonymizeResult()));

            return true;
        });

        var erased = await harness.FindAsync(started.ItemId);

        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddMinutes(10);
        await harness.RunInShellAsync(async services =>
        {
            await services.GetRequiredService<SurveyMaintenanceService>().RunAsync(SurveysConstants.PageSize, TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        var (completed, participation) = await harness.RunAsync(async services => (
            await services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(started.ItemId),
            await services.GetRequiredService<SurveyParticipationStore>().GetAllAsync()));

        Assert.Equal("u:user-learner1", keyBefore);
        Assert.Null(erased.RespondentKey);
        Assert.Null(erased.UserId);
        Assert.Equal(SurveyResponseStatus.Completed, completed.Status);
        Assert.Empty(participation);
    }

    [Fact]
    public async Task ExportAndErasure_CoverThePersonsAccommodationsWithTheirReason()
    {
        // Arrange
        var mine = IdGenerator.GenerateId();
        var theirs = IdGenerator.GenerateId();
        await using var harness = await IntegrationsTestHarness.CreateAsync(services => services.AddScoped<SurveysPersonalDataContributor>());
        await harness.RunAsync(async services =>
        {
            var overrides = services.GetRequiredService<SurveyOverrideStore>();

            await overrides.CreateAsync(new SurveyOverride { ItemId = mine, SurveyId = "s1", UserId = "user-learner1", ExtraMinutes = 30, Reason = "Reading accommodation", Actor = "user-staff" });
            await overrides.CreateAsync(new SurveyOverride { ItemId = theirs, SurveyId = "s1", UserId = "user-other", ExtraMinutes = 15, Reason = "Other", Actor = "user-learner1" });
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
        var record = Assert.Single(export.Records, item => item.Kind == "SurveyAccommodation");
        Assert.Equal(mine, record.Id);
        Assert.Equal("Reading accommodation", record.Fields["Reason"]);
        Assert.Equal("30", record.Fields["ExtraMinutes"]);
        Assert.Equal(1, erased.Records["SurveyAccommodation"]);

        // An accommodation the person granted as staff is staff work and stays.
        var left = await harness.RunAsync(async services => await services.GetRequiredService<SurveyOverrideStore>().GetAllAsync());
        Assert.Equal([theirs], left.Select(entry => entry.ItemId));
    }
}
