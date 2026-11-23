using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

public sealed class SurveyPublisherTests : IAsyncLifetime
{
    private AuthoringSurveyHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await AuthoringSurveyHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task PublishAsync_FirstPublish_CreatesVersionOneOpensAndAddsDefaultLink()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Pub", Definition = AuthoringDefinitions.TwoPages() }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);

        // Act
        var result = await services.Publisher.PublishAsync(survey.ItemId, "first", "admin-id", cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Assert
        survey = await services.Manager.FindByIdAsync(survey.ItemId, cancellationToken);
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Version.Number);
        Assert.Equal(SurveyStatus.Open, survey.Status);
        Assert.Equal(result.Version.ItemId, survey.PublishedVersionId);
        Assert.False(survey.HasUnpublishedChanges);
        Assert.Equal(64, result.Version.ContentHash.Length);

        var link = await services.CollectorStore.FindDefaultAsync(survey.ItemId, cancellationToken);
        Assert.NotNull(link);
        Assert.Equal(22, link.Token.Length);
        Assert.Equal(SurveyCollectorKinds.WebLink, link.Kind);
    }

    [Fact]
    public async Task PublishAsync_Twice_IncrementsAndKeepsTheOldVersionUnchanged()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Pub", Definition = AuthoringDefinitions.TwoPages() }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);
        var first = await services.Publisher.PublishAsync(survey.ItemId, null, "admin-id", cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        survey = await services.Manager.FindByIdAsync(survey.ItemId, cancellationToken);
        var draft = SurveyDefinitionCloner.Clone(survey.Draft);
        draft.Pages[0].Questions[1].Title = "Changed";
        await services.Manager.SaveDraftAsync(survey, draft, cancellationToken: cancellationToken);

        // Act
        var second = await services.Publisher.PublishAsync(survey.ItemId, null, "admin-id", cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Assert
        survey = await services.Manager.FindByIdAsync(survey.ItemId, cancellationToken);
        Assert.Equal(2, second.Version.Number);
        Assert.Equal(2, survey.PublishedVersionNumber);

        var versions = await services.VersionStore.ListBySurveyAsync(survey.ItemId, cancellationToken);
        Assert.Equal([2, 1], versions.Select(version => version.Number));
        Assert.NotEqual("Changed", versions.Single(version => version.Number == 1).Definition.Pages[0].Questions[1].Title);
        Assert.NotEqual(first.Version.ContentHash, second.Version.ContentHash);
        Assert.Single(await services.CollectorStore.ListBySurveyAsync(survey.ItemId, cancellationToken));
    }

    [Fact]
    public async Task PublishAsync_InvalidDraft_IsRefusedWithMessages()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Questions[1].ControlType = "Missing";
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Bad", Definition = definition }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);

        // Act
        var result = await services.Publisher.PublishAsync(survey.ItemId, null, "admin-id", cancellationToken);

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidDefinition, result.Error);
        Assert.Contains(result.Messages, message => message.Code == "unknown-control");
        Assert.Equal(SurveyStatus.Draft, survey.Status);
    }

    [Fact]
    public async Task PublishAsync_BankDraw_CopiesCandidatesUpToTheLimit()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);

        for (var i = 0; i < 4; i++)
        {
            await services.BankQuestionStore.CreateAsync(new BankQuestion
            {
                ItemId = $"bq{i}",
                BankId = "bank1",
                Revision = 3,
                Tags = ["math"],
                Question = AuthoringDefinitions.Choice("ignored", "bank_q", "x", "y"),
            }, cancellationToken);
        }

        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Pools.Add(new QuestionPool { Id = "pool", Name = "pool", DrawCount = 2, BankDraws = [new BankDraw { BankId = "bank1", Tags = ["math"], MaxCandidates = 3 }] });
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Banked", Definition = definition }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);

        Assert.Equal(4, (await services.BankQuestionStore.ListIdsWithTagsAsync("bank1", ["math"], cancellationToken)).Count);
        Assert.Equal(4, (await services.BankQuestionStore.ListByBankAsync("bank1", false, cancellationToken)).Count);

        // Act
        var result = await services.Publisher.PublishAsync(survey.ItemId, null, "admin-id", cancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var poolId = result.Version.Definition.Pages[1].Pools[0].Id;
        var members = result.Version.Definition.Pages[1].Questions.Where(question => question.PoolId == poolId).ToList();
        Assert.Equal(3, members.Count);
        Assert.All(members, member => Assert.StartsWith("bq", member.Id));
        Assert.All(members, member => Assert.Equal(3, (int)member.Properties[SurveyBankDrawResolver.BankRevisionProperty]));
        Assert.Equal(3, members.Select(member => member.Name).Distinct().Count());
        Assert.Contains(result.Messages, message => message.Code == "bank-draw-capped");
        Assert.DoesNotContain(survey.Draft.Pages[1].Questions, question => question.PoolId == poolId);
    }

    [Fact]
    public async Task RestoreAsync_CopiesTheVersionIntoTheDraftWithoutBankCopies()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        await services.BankQuestionStore.CreateAsync(new BankQuestion { ItemId = "bq", BankId = "bank1", Question = AuthoringDefinitions.Choice("x", "bank_q", "a", "b") }, cancellationToken);
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Pools.Add(new QuestionPool { Id = "pool", Name = "pool", DrawCount = 1, BankDraws = [new BankDraw { BankId = "bank1" }] });
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Restore", Definition = definition }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);
        var published = await services.Publisher.PublishAsync(survey.ItemId, null, "admin-id", cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        survey = await services.Manager.FindByIdAsync(survey.ItemId, cancellationToken);
        var draft = SurveyDefinitionCloner.Clone(survey.Draft);
        draft.Pages[0].Questions.Clear();
        await services.Manager.SaveDraftAsync(survey, draft, cancellationToken: cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        var result = await services.Publisher.RestoreAsync(survey.ItemId, published.Version.ItemId, cancellationToken: cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        survey = await services.Manager.FindByIdAsync(survey.ItemId, cancellationToken);

        // Assert
        Assert.Equal(SurveyErrorCode.None, result);
        Assert.Equal(2, survey.Draft.Pages[0].Questions.Count);
        Assert.DoesNotContain(survey.Draft.Pages[1].Questions, question => question.Id == "bq");
        Assert.True(survey.HasUnpublishedChanges);
    }

    [Fact]
    public async Task RestoreAsync_OverADraftAnotherAuthorChangedSince_IsRefusedAndKeepsTheirChange()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Restore", Definition = AuthoringDefinitions.TwoPages() }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var published = await services.Publisher.PublishAsync(created.ItemId, null, "admin-id", cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);
        var seen = survey.DraftModifiedUtc;

        // Another author changes the draft after the Versions page was shown.
        _harness.Now = _harness.Now.AddMinutes(3);
        var theirs = SurveyDefinitionCloner.Clone(survey.Draft);
        theirs.Pages[0].Title = "Their change";
        await services.Manager.SaveDraftAsync(survey, theirs, cancellationToken: cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        var result = await services.Publisher.RestoreAsync(survey.ItemId, published.Version.ItemId, seen, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.Equal(SurveyErrorCode.Busy, result);
        Assert.Equal("Their change", (await services.Manager.FindByIdAsync(survey.ItemId, cancellationToken)).Draft.Pages[0].Title);
    }

    [Fact]
    public async Task DeleteVersion_WithResponses_IsRefused()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var created = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Pinned", Definition = AuthoringDefinitions.TwoPages() }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var survey = await services.Manager.FindByIdAsync(created.ItemId, cancellationToken);
        var published = await services.Publisher.PublishAsync(survey.ItemId, null, "admin-id", cancellationToken);
        await session.SaveAsync(new SurveyResponse { ItemId = "r1", SurveyId = survey.ItemId, SurveyVersionId = published.Version.ItemId, StartedUtc = _harness.Now }, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.VersionStore.DeleteAsync(published.Version, cancellationToken));
    }
}
