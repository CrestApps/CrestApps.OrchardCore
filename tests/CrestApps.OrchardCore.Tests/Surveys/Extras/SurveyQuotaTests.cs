using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

public sealed class SurveyQuotaTests
{
    [Fact]
    public async Task Complete_QuotaFull_ScreensOutMatchingRespondentsWithTheQuotaEnding()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSurveysCollectingExtras());
        var survey = await harness.PublishAsync(WithQuota(QuotaAction.ScreenOut, ruleValue: "a"));

        // Act
        var first = await AnswerAsync(harness, survey, "u1", "a");
        var second = await AnswerAsync(harness, survey, "u2", "a");
        var other = await AnswerAsync(harness, survey, "u3", "b");

        // Assert
        Assert.Equal(SurveyResponseStatus.Completed, first.Status);
        Assert.Equal(SurveyResponseStatus.ScreenedOut, second.Status);
        Assert.Equal("full", second.EndingId);
        Assert.Equal(SurveyResponseStatus.Completed, other.Status);

        var counts = await harness.RunAsync(services => services.GetRequiredService<ISurveyQuotaService>().GetCountsAsync(survey.ItemId));

        Assert.Equal(1, counts["qa"]);
    }

    [Fact]
    public async Task Complete_TwoParallelCompletions_CountOnlyOneInAFullQuota()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSurveysCollectingExtras());
        var survey = await harness.PublishAsync(WithQuota(QuotaAction.ScreenOut, ruleValue: "a"));
        var one = (await harness.StartAsync(survey, "u1")).Response;
        var two = (await harness.StartAsync(survey, "u2")).Response;

        // Act
        await Task.WhenAll(
            harness.PostAsync(one, new SurveyRespondentIdentity { UserId = "u1" }, SurveyPageDirection.Finish, ("q1", "a")),
            harness.PostAsync(two, new SurveyRespondentIdentity { UserId = "u2" }, SurveyPageDirection.Finish, ("q1", "a")));

        // Assert
        var statuses = new[] { (await harness.FindAsync(one.ItemId)).Status, (await harness.FindAsync(two.ItemId)).Status };

        Assert.Single(statuses, status => status == SurveyResponseStatus.Completed);
        Assert.Single(statuses, status => status == SurveyResponseStatus.ScreenedOut);
    }

    [Fact]
    public async Task Start_UnconditionalQuotaFull_IsRefused()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSurveysCollectingExtras());
        var survey = await harness.PublishAsync(WithQuota(QuotaAction.CloseSurvey, ruleValue: null));

        // Act
        await AnswerAsync(harness, survey, "u1", "a");
        var refused = await harness.StartAsync(survey, "u2");

        // Assert
        Assert.Equal(SurveyErrorCode.QuotaFull, refused.Error);
    }

    [Fact]
    public async Task Complete_QuotaWithOnlyCount_NeverScreensOut()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSurveysCollectingExtras());
        var survey = await harness.PublishAsync(WithQuota(QuotaAction.None, ruleValue: "a"));

        // Act
        await AnswerAsync(harness, survey, "u1", "a");
        var second = await AnswerAsync(harness, survey, "u2", "a");

        // Assert
        Assert.Equal(SurveyResponseStatus.Completed, second.Status);
    }

    private static async Task<SurveyResponse> AnswerAsync(RuntimeTestHarness harness, Survey survey, string userId, string choice)
    {
        var response = (await harness.StartAsync(survey, userId)).Response;

        await harness.PostAsync(response, new SurveyRespondentIdentity { UserId = userId }, SurveyPageDirection.Finish, ("q1", choice));

        return await harness.FindAsync(response.ItemId);
    }

    private static SurveyDefinition WithQuota(QuotaAction action, string ruleValue)
    {
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "a", "b")));

        definition.Endings.Add(new SurveyEnding { Id = "full", Name = "full", Title = "Full", Outcome = EndingOutcome.ScreenedOut });
        definition.Quotas.Add(new SurveyQuota
        {
            Id = "qa",
            Name = "qa",
            Limit = 1,
            Action = action,
            EndingId = action == QuotaAction.ScreenOut ? "full" : null,
            Rule = ruleValue is null
                ? null
                : new ConditionRule { Conditions = [new Condition { Source = "q1", Operator = ConditionOperator.Is, Values = [ruleValue] }] },
        });

        return definition;
    }
}
