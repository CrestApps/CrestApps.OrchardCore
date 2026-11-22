using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

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

    [Fact]
    public async Task DuplicateAsync_GivesNewIdsAndKeepsReferences()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Questions[0].Visibility = new SurveyVisibility
        {
            Mode = VisibilityMode.ShowWhen,
            Rule = new() { Conditions = [new() { Source = "q1", Operator = CrestApps.OrchardCore.Conditions.ConditionOperator.Is, Values = ["red"] }] },
        };
        var source = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Source", Definition = definition }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        var copy = await services.Manager.DuplicateAsync(source.ItemId, cancellationToken: cancellationToken);

        // Assert
        var sourceIds = SurveyDefinitions.Questions(source.Draft).Select(item => item.Question.Id).ToHashSet();
        var copyQuestions = SurveyDefinitions.Questions(copy.Draft).Select(item => item.Question).ToList();
        Assert.DoesNotContain(copyQuestions, question => sourceIds.Contains(question.Id));
        Assert.All(copyQuestions, question => Assert.EndsWith("_copy", question.Name));

        var color = copyQuestions.Single(question => question.Name == "color_copy");
        var why = copyQuestions.Single(question => question.Name == "why_copy");
        var condition = why.Visibility.Rule.Conditions.Single();
        Assert.Equal(color.Id, condition.Source);
        Assert.Contains(condition.Values.Single(), color.Choices.Select(choice => choice.Id));
        Assert.Equal("source-copy", copy.Name);
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
}
