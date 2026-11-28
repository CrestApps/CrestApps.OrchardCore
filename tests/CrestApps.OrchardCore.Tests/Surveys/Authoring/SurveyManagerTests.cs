using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

public sealed class SurveyManagerTests : IAsyncLifetime
{
    private AuthoringSurveyHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await AuthoringSurveyHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task CreateSurveyAsync_FromTemplate_SetsDefaultsAndUniqueName()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);

        // Act
        var first = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Customer Feedback", Template = SurveyTemplateNames.CustomerSatisfaction }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var second = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Customer Feedback" }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.Equal("customer-feedback", first.Name);
        Assert.Equal("customer-feedback-2", second.Name);
        Assert.Equal(SurveyStatus.Draft, first.Status);
        Assert.False(string.IsNullOrEmpty(first.AnonymitySecret));
        // The secret is stored only as Data Protection ciphertext, never in plain text.
        Assert.True(CrestApps.OrchardCore.Surveys.Core.Access.SurveyAnonymitySecrets.IsProtected(first.AnonymitySecret));
        Assert.Equal(72, first.Access.IncompleteExpiryHours);
        Assert.Equal(7, first.Access.MinimumGroupSize);
        Assert.Single(first.Draft.Endings, ending => ending.IsDefault);
        Assert.NotEmpty(first.Draft.Pages);
    }

    [Fact]
    public async Task SaveDraftAsync_WhenSomeoneElseSaved_ReturnsBusy()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Draft" }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);
        var loaded = survey.DraftModifiedUtc;

        _harness.Now = _harness.Now.AddMinutes(5);
        Assert.Equal(SurveyErrorCode.None, await services.Manager.SaveDraftAsync(survey, AuthoringDefinitions.TwoPages(), loaded, cancellationToken));

        // Act
        var result = await services.Manager.SaveDraftAsync(survey, AuthoringDefinitions.TwoPages(), loaded, cancellationToken);

        // Assert
        Assert.Equal(SurveyErrorCode.Busy, result);
        Assert.True(survey.HasUnpublishedChanges);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(0)]
    public async Task R6L22_SaveDraftAsync_WhenSomeoneElseSavedWithinTheSameSecond_ReturnsBusy(int milliseconds)
    {
        // Arrange: another author saves less than a second after the draft was loaded, or on the same clock tick.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Draft" }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);
        var loaded = survey.DraftModifiedUtc;

        _harness.Now = _harness.Now.AddMilliseconds(milliseconds);
        Assert.Equal(SurveyErrorCode.None, await services.Manager.SaveDraftAsync(survey, AuthoringDefinitions.TwoPages(), loaded, cancellationToken));
        var saved = survey.DraftModifiedUtc;

        // Act
        var result = await services.Manager.SaveDraftAsync(survey, AuthoringDefinitions.TwoPages(), loaded, cancellationToken);
        var current = await services.Manager.SaveDraftAsync(survey, AuthoringDefinitions.TwoPages(), saved, cancellationToken);

        // Assert
        Assert.Equal(SurveyErrorCode.Busy, result);
        Assert.Equal(SurveyErrorCode.None, current);
        Assert.True(saved > loaded);
        Assert.True(survey.DraftModifiedUtc > saved);
    }

    [Fact]
    public async Task CreateSurveyAsync_WithAKindThatIsNotAPlainName_StoresASurvey()
    {
        // Arrange: R11-M1. A template's kind, a copied survey's kind and the AI tool's kind all come through here, and the
        // Surveys list shows the kind.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);

        // Act
        var markup = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Markup", Kind = "<img src=x onerror=alert(document.domain)>" }, cancellationToken);
        var custom = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Custom", Kind = " Assessment " }, cancellationToken);

        // Assert: a module's own kind is kept; markup never reaches the record.
        Assert.Equal(SurveyKinds.Survey, markup.Kind);
        Assert.Equal("Assessment", custom.Kind);
    }

    [Theory]
    [InlineData("Survey", true)]
    [InlineData("learning-quiz_2", true)]
    [InlineData("", false)]
    [InlineData("2fast", false)]
    [InlineData("a b", false)]
    [InlineData("<b>x</b>", false)]
    [InlineData("Ünicode", false)]
    public void SurveyKinds_IsValid_AcceptsOnlyPlainNames(string kind, bool expected)
    {
        // Act
        var result = SurveyKinds.IsValid(kind);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SurveyKinds_IsValid_RefusesAKindLongerThanTheIndexColumn()
    {
        // Act
        var fits = SurveyKinds.IsValid(new string('a', SurveyKinds.MaxLength));
        var tooLong = SurveyKinds.IsValid(new string('a', SurveyKinds.MaxLength + 1));

        // Assert
        Assert.True(fits);
        Assert.False(tooLong);
    }

    [Fact]
    public async Task SaveDraftAsync_TheStampASaveReturns_MatchesTheStampReadBackFromADocumentStoredToTheSecond()
    {
        // Arrange: a clock with sub-second ticks. Orchard Core stores a DateTime in a document to the second, so the stamp
        // the designer reads back after a save is the save's stamp cut to the second.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        _harness.Now = new DateTime(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc).AddTicks(2_249_333);
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Draft" }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);

        // Act: a save (the designer adding a question), then the next save with the stamp as the database keeps it.
        _harness.Now = _harness.Now.AddMilliseconds(400);
        var first = await services.Manager.SaveDraftAsync(survey, AuthoringDefinitions.TwoPages(), survey.DraftModifiedUtc, cancellationToken);
        var returned = survey.DraftModifiedUtc.Value;
        var stored = new DateTime(returned.Ticks - (returned.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
        var second = await services.Manager.SaveDraftAsync(survey, AuthoringDefinitions.TwoPages(), stored, cancellationToken);

        // Assert: stamps are whole seconds, so the stamp the save returned is the one stored, and the next save goes through
        // (with ticks in the stamp, it was refused as another author's change).
        Assert.Equal(SurveyErrorCode.None, first);
        Assert.Equal(0, returned.Ticks % TimeSpan.TicksPerSecond);
        Assert.Equal(SurveyErrorCode.None, second);
    }

    [Fact]
    public async Task DuplicateAsync_GivesNewIdsAndKeepsReferencesCodesAndTranslations()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var definition = AuthoringDefinitions.TwoPages();
        definition.ScoreCategories = [new ScoreCategory { Id = "cat1", Name = "skill", Title = "Skill" }];
        definition.Pages[1].Questions[0].Visibility = new SurveyVisibility
        {
            Mode = VisibilityMode.ShowWhen,
            Rule = new() { Conditions = [new() { Source = "q1", Operator = CrestApps.OrchardCore.Conditions.ConditionOperator.Is, Values = ["red"] }] },
        };
        definition.Pages[0].Questions[1].Visibility = new SurveyVisibility
        {
            Mode = VisibilityMode.ShowWhen,
            Rule = new() { Conditions = [new() { Source = "score:cat1", Operator = CrestApps.OrchardCore.Conditions.ConditionOperator.Is, Values = ["1"] }] },
        };
        definition.Translations["fr"] = new Dictionary<string, string>
        {
            ["survey:title"] = "Essai",
            ["title:q3"] = "Pourquoi {color} ?",
            ["choice:q1:red"] = "rouge",
        };
        var source = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Source", Definition = definition }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        var copy = await services.Manager.DuplicateAsync(source.ItemId, cancellationToken: cancellationToken);

        // Assert
        var sourceIds = SurveyDefinitions.Questions(source.Draft).Select(item => item.Question.Id).ToHashSet();
        var copyQuestions = SurveyDefinitions.Questions(copy.Draft).Select(item => item.Question).ToList();
        Assert.DoesNotContain(copyQuestions, question => sourceIds.Contains(question.Id));

        // Codes are per survey: the copy keeps them, so piping such as {color} still points at a question.
        Assert.Equal(["color", "comment", "why"], copyQuestions.Select(question => question.Name));

        var color = copyQuestions.Single(question => question.Name == "color");
        var why = copyQuestions.Single(question => question.Name == "why");
        var comment = copyQuestions.Single(question => question.Name == "comment");
        var condition = why.Visibility.Rule.Conditions.Single();
        Assert.Equal(color.Id, condition.Source);
        Assert.Contains(condition.Values.Single(), color.Choices.Select(choice => choice.Id));
        Assert.Equal("Why {color}?", why.Title);
        Assert.Equal("score:" + copy.Draft.ScoreCategories.Single().Id, comment.Visibility.Rule.Conditions.Single().Source);
        Assert.Equal("source-copy", copy.Name);

        // Every translation follows its item to the new ids.
        var red = color.Choices.Single(choice => choice.Value == "red");
        var french = copy.Draft.Translations["fr"];
        Assert.Equal("Essai", french["survey:title"]);
        Assert.Equal("Pourquoi {color} ?", french["title:" + why.Id]);
        Assert.Equal("rouge", french[$"choice:{color.Id}:{red.Id}"]);
        Assert.Empty(CrestApps.OrchardCore.Surveys.Core.Translations.SurveyTranslations.OutdatedKeys(copy.Draft, "fr"));
        Assert.All(french.Keys, key => Assert.Contains(key, CrestApps.OrchardCore.Surveys.Core.Translations.SurveyTextCatalog.KnownKeys(copy.Draft)));
    }

    [Fact]
    public async Task CreateSurveyAsync_FromADefinitionWithTranslations_KeepsThemOnTheNewIds()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var definition = AuthoringDefinitions.TwoPages();
        definition.Translations["fr"] = new Dictionary<string, string> { ["title:q1"] = "Couleur", ["page-title:p2"] = "Deux" };
        definition.Pages[1].Title = "Two";

        // Act
        var survey = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "From template", Definition = definition }, cancellationToken);

        // Assert
        var question = SurveyDefinitions.Questions(survey.Draft).Single(item => item.Question.Name == "color").Question;
        Assert.NotEqual("q1", question.Id);
        Assert.Equal("Couleur", survey.Draft.Translations["fr"]["title:" + question.Id]);
        Assert.Equal("Deux", survey.Draft.Translations["fr"]["page-title:" + survey.Draft.Pages[1].Id]);
    }

    [Theory]
    [InlineData(SurveyStatus.Open, SurveyStatus.Closed, true)]
    [InlineData(SurveyStatus.Closed, SurveyStatus.Archived, true)]
    [InlineData(SurveyStatus.Archived, SurveyStatus.Closed, true)]
    [InlineData(SurveyStatus.Draft, SurveyStatus.Open, false)]
    [InlineData(SurveyStatus.Open, SurveyStatus.Archived, false)]
    public async Task ChangeStatusAsync_FollowsTheAllowedMoves(SurveyStatus from, SurveyStatus to, bool allowed)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Status" }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);
        survey.Status = from;
        await services.Manager.UpdateAsync(survey, null, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        var result = await services.Manager.ChangeStatusAsync(survey.ItemId, to, cancellationToken);

        // Assert
        Assert.Equal(allowed ? SurveyErrorCode.None : SurveyErrorCode.InvalidState, result);
        Assert.Equal(allowed ? to : from, (await services.Manager.FindByIdAsync(created.ItemId, cancellationToken)).Status);
    }

    [Fact]
    public async Task DeleteSurveyAsync_WithResponses_IsRefused()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var survey = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Has answers" }, cancellationToken);
        await session.SaveAsync(new SurveyResponse { ItemId = "r1", SurveyId = survey.ItemId, StartedUtc = _harness.Now }, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        var result = await services.Manager.DeleteSurveyAsync(survey.ItemId, cancellationToken);

        // Assert
        Assert.Equal(SurveyErrorCode.HasResponses, result);
        Assert.NotNull(await services.Manager.FindByIdAsync(survey.ItemId, cancellationToken));
    }

    [Fact]
    public async Task DeleteSurveyAsync_WithoutResponses_RemovesTheSurvey()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var session = _harness.Store.CreateSession())
        {
            var services = _harness.CreateServices(session);
            await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Name = "remove-me", Title = "Remove" }, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        await using (var session = _harness.Store.CreateSession())
        {
            var services = _harness.CreateServices(session);
            var survey = await services.Manager.FindByNameAsync("remove-me", cancellationToken);

            // Act
            var result = await services.Manager.DeleteSurveyAsync(survey.ItemId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            // Assert
            Assert.Equal(SurveyErrorCode.None, result);
        }

        await using (var session = _harness.Store.CreateSession())
        {
            Assert.Null(await _harness.CreateServices(session).Manager.FindByNameAsync("remove-me", cancellationToken));
        }
    }

    [Fact]
    public async Task DeleteSurveyAsync_RemovesItsInvitationsAccommodationsParticipationCountersAndTestResponsesWithFiles()
    {
        // Arrange: more invitations than one purge batch, a test response with a file, and another survey's records.
        var cancellationToken = TestContext.Current.CancellationToken;
        string surveyId;
        string otherId;
        CrestApps.OrchardCore.Attachments.Models.Attachment file;

        await using (var session = _harness.Store.CreateSession())
        {
            var services = _harness.CreateServices(session);
            surveyId = (await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Name = "purge-me", Title = "Purge" }, cancellationToken)).ItemId;
            otherId = (await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Name = "keep-me", Title = "Keep" }, cancellationToken)).ItemId;

            for (var i = 0; i < SurveyDataPurger.BatchSize + 50; i++)
            {
                await session.SaveAsync(new SurveyInvitation { ItemId = $"inv{i:D4}", SurveyId = surveyId, Email = $"person{i}@example.com" }, false, SurveysConstants.CollectionName, cancellationToken);
            }

            await session.SaveAsync(new SurveyInvitation { ItemId = "invother", SurveyId = otherId, Email = "other@example.com" }, false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new SurveyOverride { ItemId = "ov1", SurveyId = surveyId, UserId = "u1", Reason = "Accommodation" }, false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new SurveyParticipation { ItemId = "pa1", SurveyId = surveyId, RespondentKey = "u:u1", Day = new DateOnly(2026, 11, 20) }, false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new CrestApps.OrchardCore.Surveys.Core.Runtime.SurveyCounter { ItemId = "co1", SurveyId = surveyId, Kind = "quota" }, false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new SurveyResponse { ItemId = "test1", SurveyId = surveyId, IsTest = true, StartedUtc = _harness.Now }, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            file = _harness.Attachments.Seed(CrestApps.OrchardCore.Surveys.Core.Runtime.SurveyResponseFiles.OwnerOf("test1"), _harness.Now);
        }

        // Act
        await using (var session = _harness.Store.CreateSession())
        {
            var result = await _harness.CreateServices(session).Manager.DeleteSurveyAsync(surveyId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            Assert.Equal(SurveyErrorCode.None, result);
        }

        // Assert
        await using (var session = _harness.Store.CreateSession())
        {
            Assert.Equal(0, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyInvitationIndex>(index => index.SurveyId == surveyId, SurveysConstants.CollectionName).CountAsync(cancellationToken));
            Assert.Equal(1, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyInvitationIndex>(index => index.SurveyId == otherId, SurveysConstants.CollectionName).CountAsync(cancellationToken));
            Assert.Equal(0, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyOverrideIndex>(index => index.SurveyId == surveyId, SurveysConstants.CollectionName).CountAsync(cancellationToken));
            Assert.Equal(0, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyParticipationIndex>(index => index.SurveyId == surveyId, SurveysConstants.CollectionName).CountAsync(cancellationToken));
            Assert.Equal(0, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyCounterIndex>(index => index.SurveyId == surveyId, SurveysConstants.CollectionName).CountAsync(cancellationToken));
            Assert.Equal(0, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyResponseIndex>(index => index.SurveyId == surveyId, SurveysConstants.ResponsesCollectionName).CountAsync(cancellationToken));
            Assert.Null(await _harness.CreateServices(session).Manager.FindByNameAsync("purge-me", cancellationToken));
            Assert.False(_harness.Attachments.Stored.ContainsKey(file.ItemId));
        }
    }

    [Fact]
    public async Task ChangeStatusAsync_RecordsWhenTheSurveyClosed_AndReopeningClearsIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Closing" }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);
        survey.Status = SurveyStatus.Open;
        await services.Manager.UpdateAsync(survey, null, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        await services.Manager.ChangeStatusAsync(created.ItemId, SurveyStatus.Closed, cancellationToken);
        var closed = (await services.Manager.FindByIdAsync(created.ItemId, cancellationToken)).ClosedUtc;
        await services.Manager.ChangeStatusAsync(created.ItemId, SurveyStatus.Open, cancellationToken);
        var reopened = (await services.Manager.FindByIdAsync(created.ItemId, cancellationToken)).ClosedUtc;

        // Assert
        Assert.Equal(_harness.Now, closed);
        Assert.Null(reopened);
    }

    [Fact]
    public async Task DeleteSurveyAsync_LeavesANoteOfTheDelete_ForTheUpkeepsFollowUp()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        string surveyId;

        await using (var session = _harness.Store.CreateSession())
        {
            surveyId = (await _harness.CreateServices(session).Manager.CreateSurveyAsync(new SurveyCreateRequest { Name = "noted", Title = "Noted" }, cancellationToken)).ItemId;
            await session.SaveChangesAsync(cancellationToken);
        }

        // Act
        await using (var session = _harness.Store.CreateSession())
        {
            Assert.Equal(SurveyErrorCode.None, await _harness.CreateServices(session).Manager.DeleteSurveyAsync(surveyId, cancellationToken));
            await session.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using (var session = _harness.Store.CreateSession())
        {
            var notes = await session.Query<SurveyDeletion>(SurveysConstants.CollectionName).ListAsync(cancellationToken);

            Assert.Contains(notes, note => note.SurveyId == surveyId && note.StartedUtc == _harness.Now);
            Assert.Null(await _harness.CreateServices(session).Manager.FindByIdAsync(surveyId, cancellationToken));
        }
    }

    [Fact]
    public async Task DeleteSurveyAsync_OfASurveyMarkedDeleting_ThatGotAResponse_TakesTheMarkOff_AndKeepsTheSurvey()
    {
        // Arrange: an earlier delete stopped half way, and a real response came in before the mark.
        var cancellationToken = TestContext.Current.CancellationToken;
        string surveyId;

        await using (var session = _harness.Store.CreateSession())
        {
            var services = _harness.CreateServices(session);
            var survey = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Half deleted" }, cancellationToken);
            surveyId = survey.ItemId;
            survey.DeletingUtc = _harness.Now;
            await services.SurveyStore.UpdateAsync(survey, cancellationToken);
            await session.SaveAsync(new SurveyDeletion { SurveyId = surveyId, StartedUtc = _harness.Now }, false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new SurveyResponse { ItemId = "late1", SurveyId = surveyId, StartedUtc = _harness.Now }, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        // Act
        SurveyErrorCode result;

        await using (var session = _harness.Store.CreateSession())
        {
            result = await _harness.CreateServices(session).Manager.DeleteSurveyAsync(surveyId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using (var session = _harness.Store.CreateSession())
        {
            var survey = await _harness.CreateServices(session).Manager.FindByIdAsync(surveyId, cancellationToken);

            Assert.Equal(SurveyErrorCode.HasResponses, result);
            Assert.NotNull(survey);
            Assert.Null(survey.DeletingUtc);
            Assert.DoesNotContain(await session.Query<SurveyDeletion>(SurveysConstants.CollectionName).ListAsync(cancellationToken), note => note.SurveyId == surveyId);
        }
    }

    [Fact]
    public async Task DeleteSurveyAsync_AResponseThatArrivesBeforeTheMark_StopsTheDelete_BeforeAnythingIsRemoved()
    {
        // Arrange: a survey with an invitation, an accommodation and a participation record; a real response comes in
        // right after the delete's first count, before the survey is marked (R6-L11).
        var cancellationToken = TestContext.Current.CancellationToken;
        string surveyId;

        await using (var session = _harness.Store.CreateSession())
        {
            surveyId = (await _harness.CreateServices(session).Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Raced" }, cancellationToken)).ItemId;
            await session.SaveAsync(new SurveyInvitation { ItemId = "invraced", SurveyId = surveyId, Email = "raced@example.com" }, false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new SurveyOverride { ItemId = "ovraced", SurveyId = surveyId, UserId = "u1", Reason = "Accommodation" }, false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(new SurveyParticipation { ItemId = "paraced", SurveyId = surveyId, RespondentKey = "u:u1", Day = new DateOnly(2026, 11, 20) }, false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        // Act
        SurveyErrorCode result;

        await using (var session = _harness.Store.CreateSession())
        {
            var services = _harness.CreateServices(session, responseQueries: inner => new ResponseArrivesAfterFirstCount(inner, session, surveyId));

            result = await services.Manager.DeleteSurveyAsync(surveyId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        // Assert: the delete stopped, and nothing of the survey was removed.
        await using (var session = _harness.Store.CreateSession())
        {
            var survey = await _harness.CreateServices(session).Manager.FindByIdAsync(surveyId, cancellationToken);

            Assert.Equal(SurveyErrorCode.HasResponses, result);
            Assert.NotNull(survey);
            Assert.Null(survey.DeletingUtc);
            Assert.Equal(1, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyInvitationIndex>(index => index.SurveyId == surveyId, SurveysConstants.CollectionName).CountAsync(cancellationToken));
            Assert.Equal(1, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyOverrideIndex>(index => index.SurveyId == surveyId, SurveysConstants.CollectionName).CountAsync(cancellationToken));
            Assert.Equal(1, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyParticipationIndex>(index => index.SurveyId == surveyId, SurveysConstants.CollectionName).CountAsync(cancellationToken));
        }
    }

    [Fact]
    public async Task DeleteSurveyAsync_WithResponses_KeepsItsInvitations()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var survey = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Answered" }, cancellationToken);
        await session.SaveAsync(new SurveyResponse { ItemId = "real1", SurveyId = survey.ItemId, StartedUtc = _harness.Now }, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        await session.SaveAsync(new SurveyInvitation { ItemId = "invkeep", SurveyId = survey.ItemId, Email = "keep@example.com" }, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        var result = await services.Manager.DeleteSurveyAsync(survey.ItemId, cancellationToken);

        // Assert
        Assert.Equal(SurveyErrorCode.HasResponses, result);
        Assert.Equal(1, await session.QueryIndex<CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyInvitationIndex>(index => index.SurveyId == survey.ItemId, SurveysConstants.CollectionName).CountAsync(cancellationToken));
    }

    // Counts no response the first time, as a delete's first count did, and a real response is saved right then, so it is
    // committed with the delete's mark.
    private sealed class ResponseArrivesAfterFirstCount : ISurveyResponseQueries
    {
        private readonly ISurveyResponseQueries _inner;
        private readonly ISession _session;
        private readonly string _surveyId;
        private bool _arrived;

        public ResponseArrivesAfterFirstCount(ISurveyResponseQueries inner, ISession session, string surveyId)
        {
            _inner = inner;
            _session = session;
            _surveyId = surveyId;
        }

        public Task<SurveyResponse> FindByIdAsync(string responseId, CancellationToken cancellationToken = default)
            => _inner.FindByIdAsync(responseId, cancellationToken);

        public Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(SurveyResponseQuery query, CancellationToken cancellationToken = default)
            => _inner.ListAsync(query, cancellationToken);

        public async Task<int> CountForSurveyAsync(string surveyId, CancellationToken cancellationToken = default)
        {
            var count = await _inner.CountForSurveyAsync(surveyId, cancellationToken);

            if (!_arrived && surveyId == _surveyId)
            {
                _arrived = true;
                await _session.SaveAsync(new SurveyResponse { ItemId = "arrived", SurveyId = surveyId, StartedUtc = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc) }, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            return count;
        }
    }
}
