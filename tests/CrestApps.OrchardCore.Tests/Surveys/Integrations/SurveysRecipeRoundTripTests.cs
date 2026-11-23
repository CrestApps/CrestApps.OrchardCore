using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;

namespace CrestApps.OrchardCore.Tests.Surveys.Integrations;

public sealed class SurveysRecipeRoundTripTests
{
    private static readonly string SurveyId = IdGenerator.GenerateId();
    private static readonly string Version1Id = IdGenerator.GenerateId();
    private static readonly string Version2Id = IdGenerator.GenerateId();
    private static readonly string CollectorId = IdGenerator.GenerateId();
    private static readonly string LocalBankQuestionId = IdGenerator.GenerateId();
    private static readonly string BankQuestionId = IdGenerator.GenerateId();
    private static readonly string BankId = IdGenerator.GenerateId();
    private static readonly string InvitationId = IdGenerator.GenerateId();
    private static readonly string OverrideId = IdGenerator.GenerateId();
    private static readonly string AnonymousResponseId = IdGenerator.GenerateId();
    private static readonly string GhostResponseId = IdGenerator.GenerateId();
    private static readonly string ResponseId = IdGenerator.GenerateId();

    [Fact]
    public async Task ExportThenImport_IntoAnEmptySite_GivesTheSameSurveyVersionsAndCollectors()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        await SeedSurveyAsync(source);
        var step = await ExportSurveysAsync(source, includeSecrets: false);
        await using var target = await IntegrationsTestHarness.CreateAsync();

        // Act
        var errors = await target.ImportAsync<SurveysRecipeStep>(step);

        // Assert
        Assert.Empty(errors);
        var (survey, versions, collectors) = await target.RunAsync(async services => (
            await services.GetRequiredService<SurveyStore>().FindByNameAsync("csat"),
            await services.GetRequiredService<SurveyVersionStore>().ListBySurveyAsync(SurveyId),
            await services.GetRequiredService<SurveyCollectorStore>().ListBySurveyAsync(SurveyId)));
        Assert.NotNull(survey);
        Assert.Equal(SurveyId, survey.ItemId);
        Assert.Equal("Title of csat", survey.Title);
        Assert.Equal("Draft of csat", survey.Draft.Title);
        Assert.Equal(2, survey.PublishedVersionNumber);
        Assert.Equal(Version2Id, survey.PublishedVersionId);
        Assert.Equal([2, 1], versions.Select(version => version.Number));
        Assert.Equal("Version 1", versions.Single(version => version.Number == 1).Definition.Title);
        Assert.Equal("q1", versions.Single(version => version.Number == 2).Definition.Pages[0].Questions[0].Id);
        var collector = Assert.Single(collectors);
        Assert.Equal("Main link", collector.Name);
        Assert.NotEqual("tokentokentokentoken12", collector.Token);
        Assert.Equal(22, collector.Token.Length);
        Assert.Null(collector.PasswordHash);
        Assert.False(string.IsNullOrEmpty(survey.AnonymitySecret));
        Assert.NotEqual("secret-csat", survey.AnonymitySecret);
    }

    [Fact]
    public async Task Export_WithoutSecrets_LeavesOutTokensPasswordsAndTheAnonymitySecret()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        await SeedSurveyAsync(source);

        // Act
        var step = await ExportSurveysAsync(source, includeSecrets: false);

        // Assert
        var json = step.ToJsonString();
        Assert.DoesNotContain("secret-csat", json, StringComparison.Ordinal);
        Assert.DoesNotContain("tokentokentokentoken12", json, StringComparison.Ordinal);
        Assert.DoesNotContain("hash-of-password", json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Survey.AnonymitySecret), json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_WithSecrets_KeepsTheLinkTokenAndPasswordOnImport()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        await SeedSurveyAsync(source);
        var step = await ExportSurveysAsync(source, includeSecrets: true);
        await using var target = await IntegrationsTestHarness.CreateAsync();

        // Act
        await target.ImportAsync<SurveysRecipeStep>(step);

        // Assert
        var collector = Assert.Single(await target.RunAsync(async services => await services.GetRequiredService<SurveyCollectorStore>().ListBySurveyAsync(SurveyId)));
        Assert.Equal("tokentokentokentoken12", collector.Token);
        Assert.Equal("hash-of-password", collector.PasswordHash);
        Assert.DoesNotContain("secret-csat", step.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportingTwice_KeepsOneSurvey_AndNeverDuplicatesOrChangesVersions()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        await SeedSurveyAsync(source);
        var step = await ExportSurveysAsync(source, includeSecrets: false);
        await using var target = await IntegrationsTestHarness.CreateAsync();
        await target.ImportAsync<SurveysRecipeStep>(step);

        // A version the site already has is immutable: an edited copy in the file must not change it.
        var edited = step.DeepClone().AsObject();
        edited["Surveys"]![0]!["Versions"]![0]!["Definition"]!["Title"] = "Changed";
        edited["Surveys"]![0]!["Title"] = "New title";

        // Act
        var errors = await target.ImportAsync<SurveysRecipeStep>(edited);

        // Assert
        Assert.Empty(errors);
        var (surveys, versions, collectors) = await target.RunAsync(async services => (
            await services.GetRequiredService<SurveyStore>().GetAllAsync(),
            await services.GetRequiredService<SurveyVersionStore>().ListBySurveyAsync(SurveyId),
            await services.GetRequiredService<SurveyCollectorStore>().ListBySurveyAsync(SurveyId)));
        Assert.Equal("New title", Assert.Single(surveys).Title);
        Assert.Equal(2, versions.Count);
        Assert.Equal("Version 1", versions.Single(version => version.Number == 1).Definition.Title);
        Assert.Single(collectors);
    }

    [Fact]
    public async Task ImportingANewerVersion_AddsIt_AndPublishesIt()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        await SeedSurveyAsync(source);
        var step = await ExportSurveysAsync(source, includeSecrets: false);
        await using var target = await IntegrationsTestHarness.CreateAsync();
        await target.ImportAsync<SurveysRecipeStep>(step);
        var newer = step.DeepClone().AsObject();
        var survey = newer["Surveys"]![0]!.AsObject();
        survey["PublishedVersionNumber"] = 3;
        survey["Versions"]!.AsArray().Add(new JsonObject
        {
            ["Number"] = 3,
            ["Definition"] = new JsonObject { ["Title"] = "Version 3" },
        });

        // Act
        await target.ImportAsync<SurveysRecipeStep>(newer);

        // Assert
        var (stored, versions) = await target.RunAsync(async services => (
            await services.GetRequiredService<SurveyStore>().FindByNameAsync("csat"),
            await services.GetRequiredService<SurveyVersionStore>().ListBySurveyAsync(SurveyId)));
        Assert.Equal(3, versions.Count);
        Assert.Equal(3, stored.PublishedVersionNumber);
        Assert.Equal(versions.Single(version => version.Number == 3).ItemId, stored.PublishedVersionId);
    }

    [Fact]
    public async Task ASurveyWithoutName_IsReportedAndSkipped()
    {
        // Arrange
        await using var target = await IntegrationsTestHarness.CreateAsync();
        var step = new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Surveys,
            ["Surveys"] = new JsonArray(new JsonObject { ["Title"] = "No name" }),
        };

        // Act
        var errors = await target.ImportAsync<SurveysRecipeStep>(step);

        // Assert
        Assert.Single(errors);
        Assert.Empty(await target.RunAsync(async services => await services.GetRequiredService<SurveyStore>().GetAllAsync()));
    }

    [Fact]
    public async Task QuestionBanks_RoundTrip_MatchQuestionsBySourceIdentifier()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        await source.RunAsync(async services =>
        {
            await services.GetRequiredService<QuestionBankStore>().CreateAsync(new QuestionBank { ItemId = BankId, Name = "geography", Title = "Geography" });
            await services.GetRequiredService<BankQuestionStore>().CreateAsync(new BankQuestion
            {
                ItemId = BankQuestionId,
                BankId = BankId,
                SourceIdentifier = "geo-1",
                Tags = ["capitals"],
                Difficulty = 2,
                Revision = 4,
                Question = new SurveyQuestion
                {
                    Id = BankQuestionId,
                    Name = "q_capital",
                    ControlType = SurveyControlNames.ShortText,
                    Title = "Capital of France?",
                    Scoring = new SurveyQuestionScoring { AcceptedAnswers = ["Paris"] },
                },
            });
        });

        var step = await source.RunAsync(async services =>
        {
            var exporter = services.GetRequiredService<SurveysRecipeExporter>();
            var bank = await services.GetRequiredService<QuestionBankStore>().FindByNameAsync("geography");

            return new JsonObject
            {
                ["name"] = SurveysRecipeSteps.QuestionBanks,
                ["QuestionBanks"] = new JsonArray(await exporter.ExportBankAsync(bank)),
            };
        });

        await using var target = await IntegrationsTestHarness.CreateAsync();

        // A question already on the site under another id is matched by its source identifier.
        await target.RunAsync(async services =>
        {
            await services.GetRequiredService<QuestionBankStore>().CreateAsync(new QuestionBank { ItemId = BankId, Name = "geography" });
            await services.GetRequiredService<BankQuestionStore>().CreateAsync(new BankQuestion { ItemId = LocalBankQuestionId, BankId = BankId, SourceIdentifier = "geo-1", Revision = 7, Question = new SurveyQuestion { Title = "Old" } });
        });

        // Act
        var errors = await target.ImportAsync<SurveyQuestionBanksRecipeStep>(step);

        // Assert
        Assert.Empty(errors);
        var questions = await target.RunAsync(async services => await services.GetRequiredService<BankQuestionStore>().ListByBankAsync(BankId, includeRetired: true));
        var question = Assert.Single(questions);
        Assert.Equal(LocalBankQuestionId, question.ItemId);
        Assert.Equal("Capital of France?", question.Question.Title);
        Assert.Equal(LocalBankQuestionId, question.Question.Id);
        Assert.Equal(["Paris"], question.Question.Scoring.AcceptedAnswers);
        Assert.Equal(8, question.Revision);
    }

    [Fact]
    public async Task Responses_RoundTrip_ByUserName_AndUnknownPeopleAreErrors()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        await SeedSurveyAsync(source);
        await source.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyResponseStore>();

            await store.CreateAsync(new SurveyResponse
            {
                ItemId = ResponseId,
                SurveyId = SurveyId,
                SurveyVersionId = Version2Id,
                VersionNumber = 2,
                Number = "R1",
                Status = SurveyResponseStatus.Completed,
                UserId = "user-learner1",
                Respondent = new PartyRef(PartyKinds.User, "user-learner1"),
                GradedBy = "user-grader1",
                ResumeTokenHash = "do-not-export",
                RespondentKey = "u:do-not-export-key",
                Answers = { ["q1"] = new SurveyAnswer { Values = ["c2"] } },
            });
        });

        var step = await source.RunAsync(async services =>
        {
            var responses = await services.GetRequiredService<SurveyResponseStore>().GetAllAsync();

            return new JsonObject
            {
                ["name"] = SurveysRecipeSteps.Responses,
                ["Responses"] = await services.GetRequiredService<SurveysRecipeExporter>().ExportResponsesAsync(responses.ToList()),
            };
        });

        await using var target = await IntegrationsTestHarness.CreateAsync();
        await target.ImportAsync<SurveysRecipeStep>(await ExportSurveysAsync(source, includeSecrets: false));
        var ghost = step.DeepClone().AsObject();
        ghost["Responses"]![0]!["UserName"] = "ghost";
        ghost["Responses"]![0]!["ItemId"] = GhostResponseId;

        // Act
        var errors = await target.ImportAsync<SurveyResponsesRecipeStep>(step);
        var ghostErrors = await target.ImportAsync<SurveyResponsesRecipeStep>(ghost);

        // Assert
        Assert.DoesNotContain("do-not-export", step.ToJsonString(), StringComparison.Ordinal);
        Assert.False(step["Responses"]![0]!.AsObject().ContainsKey("RespondentKey"));
        Assert.Equal("learner1", step["Responses"]![0]!["UserName"]!.GetValue<string>());
        Assert.Empty(errors);
        Assert.Single(ghostErrors);
        var response = Assert.Single(await target.RunAsync(async services => await services.GetRequiredService<SurveyResponseStore>().GetAllAsync()));
        Assert.Equal("user-learner1", response.UserId);
        Assert.Equal(new PartyRef(PartyKinds.User, "user-learner1"), response.Respondent);
        Assert.Equal("user-grader1", response.GradedBy);
        Assert.Equal(Version2Id, response.SurveyVersionId);
        Assert.Equal(["c2"], response.Answers["q1"].Values);
    }

    [Fact]
    public async Task Responses_OfAnAnonymousSurvey_AreImportedWithoutIdentity()
    {
        // Arrange
        await using var target = await IntegrationsTestHarness.CreateAsync();
        await target.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyStore>().CreateAsync(IntegrationsTestHarness.NewSurvey(SurveyId, "pulse", AnonymityMode.Anonymous));
            await services.GetRequiredService<SurveyVersionStore>().CreateAsync(new SurveyVersion { ItemId = Version1Id, SurveyId = SurveyId, Number = 1, Definition = new SurveyDefinition() });
        });

        var step = new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Responses,
            ["Responses"] = new JsonArray(new JsonObject
            {
                ["ItemId"] = AnonymousResponseId,
                ["SurveyName"] = "pulse",
                ["VersionNumber"] = 1,
                ["UserName"] = "learner1",
                ["IpAddress"] = "10.0.0.1",
                ["Status"] = "Completed",
            }),
        };

        // Act
        var errors = await target.ImportAsync<SurveyResponsesRecipeStep>(step);

        // Assert
        Assert.Empty(errors);
        var response = Assert.Single(await target.RunAsync(async services => await services.GetRequiredService<SurveyResponseStore>().GetAllAsync()));
        Assert.Null(response.UserId);
        Assert.Null(response.Respondent);
        Assert.Null(response.IpAddress);
    }

    [Fact]
    public async Task Responses_Import_IgnoresTheRespondentKeyInTheFile()
    {
        // Arrange: a hand-written in-progress response that carries a respondent key naming a person.
        await using var target = await IntegrationsTestHarness.CreateAsync();
        await target.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyStore>().CreateAsync(IntegrationsTestHarness.NewSurvey(SurveyId, "pulse"));
            await services.GetRequiredService<SurveyVersionStore>().CreateAsync(new SurveyVersion { ItemId = Version1Id, SurveyId = SurveyId, Number = 1, Definition = new SurveyDefinition() });
        });

        var step = new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Responses,
            ["Responses"] = new JsonArray(new JsonObject
            {
                ["ItemId"] = ResponseId,
                ["SurveyName"] = "pulse",
                ["VersionNumber"] = 1,
                ["Status"] = "InProgress",
                ["RespondentKey"] = "u:user-learner1",
            }),
        };

        // Act
        var errors = await target.ImportAsync<SurveyResponsesRecipeStep>(step);

        // Assert
        Assert.Empty(errors);
        var response = Assert.Single(await target.RunAsync(async services => await services.GetRequiredService<SurveyResponseStore>().GetAllAsync()));
        Assert.Null(response.RespondentKey);
    }

    [Fact]
    public async Task OverridesAndInvitations_RoundTrip_ByUserName()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        await SeedSurveyAsync(source);
        await source.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyOverrideStore>().CreateAsync(new SurveyOverride { ItemId = OverrideId, SurveyId = SurveyId, UserId = "user-learner1", ExtraMinutes = 15, Reason = "Reading support", Actor = "user-manager1" });
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation { ItemId = InvitationId, SurveyId = SurveyId, UserId = "user-learner1", TokenHash = "abc123", Status = SurveyInvitationStatus.Sent });
        });

        var (overrides, invitations) = await source.RunAsync(async services =>
        {
            var exporter = services.GetRequiredService<SurveysRecipeExporter>();

            return (
                new JsonObject { ["name"] = SurveysRecipeSteps.Overrides, ["Overrides"] = await exporter.ExportOverridesAsync((await services.GetRequiredService<SurveyOverrideStore>().GetAllAsync()).ToList()) },
                new JsonObject { ["name"] = SurveysRecipeSteps.Invitations, ["Invitations"] = await exporter.ExportInvitationsAsync((await services.GetRequiredService<SurveyInvitationStore>().GetAllAsync()).ToList()) });
        });

        await using var target = await IntegrationsTestHarness.CreateAsync();
        await target.ImportAsync<SurveysRecipeStep>(await ExportSurveysAsync(source, includeSecrets: false));

        // Act
        var overrideErrors = await target.ImportAsync<SurveyOverridesRecipeStep>(overrides);
        var invitationErrors = await target.ImportAsync<SurveyInvitationsRecipeStep>(invitations);
        await target.ImportAsync<SurveyOverridesRecipeStep>(overrides);

        // Assert
        Assert.Empty(overrideErrors);
        Assert.Empty(invitationErrors);
        var (storedOverrides, storedInvitations) = await target.RunAsync(async services => (
            await services.GetRequiredService<SurveyOverrideStore>().GetAllAsync(),
            await services.GetRequiredService<SurveyInvitationStore>().GetAllAsync()));
        var entry = Assert.Single(storedOverrides);
        Assert.Equal("user-learner1", entry.UserId);
        Assert.Equal("user-manager1", entry.Actor);
        Assert.Equal(15, entry.ExtraMinutes);
        var invitation = Assert.Single(storedInvitations);
        Assert.Equal("abc123", invitation.TokenHash);
        Assert.Equal("user-learner1", invitation.UserId);
    }

    internal static Task SeedSurveyAsync(IntegrationsTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var survey = IntegrationsTestHarness.NewSurvey(SurveyId, "csat");
            survey.PublishedVersionId = Version2Id;
            survey.PublishedVersionNumber = 2;

            await services.GetRequiredService<SurveyStore>().CreateAsync(survey);
            await services.GetRequiredService<SurveyVersionStore>().CreateAsync(new SurveyVersion { ItemId = Version1Id, SurveyId = SurveyId, Number = 1, Definition = IntegrationsTestHarness.Definition("Version 1"), PublishedBy = "user-manager1", PublishedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
            await services.GetRequiredService<SurveyVersionStore>().CreateAsync(new SurveyVersion { ItemId = Version2Id, SurveyId = SurveyId, Number = 2, Definition = IntegrationsTestHarness.Definition("Version 2"), PublishedUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) });
            await services.GetRequiredService<SurveyCollectorStore>().CreateAsync(new SurveyCollector { ItemId = CollectorId, SurveyId = SurveyId, Name = "Main link", Token = "tokentokentokentoken12", PasswordHash = "hash-of-password", IsDefault = true });
        });

    internal static Task<JsonObject> ExportSurveysAsync(IntegrationsTestHarness harness, bool includeSecrets)
        => harness.RunAsync(async services =>
        {
            var exporter = services.GetRequiredService<SurveysRecipeExporter>();
            var surveys = new JsonArray();

            foreach (var survey in await services.GetRequiredService<SurveyStore>().GetAllAsync())
            {
                surveys.Add(await exporter.ExportSurveyAsync(survey, includeSecrets));
            }

            return new JsonObject
            {
                ["name"] = SurveysRecipeSteps.Surveys,
                ["Surveys"] = surveys,
            };
        });
}
