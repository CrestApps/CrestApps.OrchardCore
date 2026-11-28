using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Api;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

/// <summary>
/// The Surveys API does not trust the caller: contexts must be registered and owned, variables are treated like query
/// string values, and the definition runs the access rules and never gives a ranking's key order away.
/// </summary>
public sealed class SurveysApiSecurityTests
{
    private const string UserId = "u1";

    [Fact]
    public async Task Start_TamperedContexts_AreRefused_AndGiveNoExtraAttempt()
    {
        // Arrange: one attempt allowed, and the caller has used it.
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))), configure: value =>
        {
            value.Access.Limits = new RespondentLimits { Counts = LimitCount.Started, Total = 1 };
        });

        var first = await StartAsync(harness, new StartResponseInput { SurveyId = survey.ItemId });
        await harness.PostAsync(await LatestAsync(harness, survey), new SurveyRespondentIdentity { UserId = UserId }, SurveyPageDirection.Finish, ("q1", "done"));

        // Act: made-up kinds, a registered kind the caller does not own, and no context at all.
        var madeUp = await StartAsync(harness, new StartResponseInput { SurveyId = survey.ItemId, Context = "x:1" });
        var madeUpAgain = await StartAsync(harness, new StartResponseInput { SurveyId = survey.ItemId, Context = "x:2" });
        var notOwned = await StartAsync(harness, new StartResponseInput { SurveyId = survey.ItemId, Context = $"{TicketCheck.Kind}:someone-else" });
        var noCheck = await StartAsync(harness, new StartResponseInput { SurveyId = survey.ItemId, Context = $"{UncheckedKind}:mine" });
        var again = await StartAsync(harness, new StartResponseInput { SurveyId = survey.ItemId });

        // Assert
        Assert.Equal(StatusCodes.Status200OK, StatusOf(first));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(madeUp));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(madeUpAgain));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(notOwned));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(noCheck));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusOf(again));
        Assert.Equal(1, await CountAsync(harness, survey));
    }

    [Fact]
    public async Task Start_OwnedContextOfARegisteredKind_IsAccepted()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act
        var result = await StartAsync(harness, new StartResponseInput { SurveyId = survey.ItemId, Context = $"{TicketCheck.Kind}:{TicketCheck.OwnedId}" });

        // Assert
        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        Assert.Equal(new RecordRef(TicketCheck.Kind, TicketCheck.OwnedId), (await LatestAsync(harness, survey)).Context);
    }

    [Fact]
    public async Task Start_Variables_AreTreatedAsQueryVariables()
    {
        // Arrange: one hidden variable accepts a value from the URL, the other does not.
        await using var harness = await CreateHarnessAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1")));

        definition.Variables.Add(new SurveyVariable { Name = "plan", Kind = VariableKind.Hidden, DefaultValue = "free" });
        definition.Variables.Add(new SurveyVariable { Name = "source", Kind = VariableKind.Hidden, AcceptFromUrl = true });

        var survey = await harness.PublishAsync(definition);

        // Act
        var result = await StartAsync(harness, new StartResponseInput
        {
            SurveyId = survey.ItemId,
            Variables = new Dictionary<string, string> { ["plan"] = "enterprise", ["source"] = "newsletter" },
        });

        // Assert
        var response = await LatestAsync(harness, survey);

        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        Assert.Equal("free", response.Variables["plan"]);
        Assert.Equal("newsletter", response.Variables["source"]);
    }

    [Fact]
    public async Task Definition_CallerFailingTheAccessRules_IsRefused()
    {
        // Arrange: the survey's link has a password, which the caller does not give.
        await using var harness = await CreateHarnessAsync();
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        await harness.RunAsync(async services =>
        {
            var collectors = services.GetRequiredService<SurveyCollectorStore>();
            var collector = await collectors.FindDefaultAsync(survey.ItemId);

            collector.PasswordHash = PasswordRule.HashPassword(collector, "letmein");
            await collectors.UpdateAsync(collector);

            return collector;
        });

        // Act
        var result = await DefinitionAsync(harness, survey.ItemId);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));
    }

    [Fact]
    public async Task Definition_NeverShowsTheRankingKeyOrder_AndLeavesPooledQuestionsOut()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var authored = Enumerable.Range(1, 8).Select(index => "item" + index).ToList();
        var ranking = new SurveyQuestion
        {
            Id = "rank",
            Name = "rank",
            ControlType = SurveyControlNames.Ranking,
            Title = "Put these in order",
            Choices = authored.Select(id => new SurveyChoice { Id = id, Value = id, Text = id }).ToList(),
        };

        var page = RuntimeSurveys.Page("p1", ranking, RuntimeSurveys.Text("pooled1"), RuntimeSurveys.Text("pooled2"));

        page.Pools.Add(new QuestionPool { Id = "pool", DrawCount = 1 });
        page.Questions[1].PoolId = "pool";
        page.Questions[2].PoolId = "pool";

        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(page));
        var orders = new List<List<string>>();
        var questionIds = new HashSet<string>(StringComparer.Ordinal);

        // Act
        for (var call = 0; call < 10; call++)
        {
            var result = await DefinitionAsync(harness, survey.ItemId);

            Assert.Equal(StatusCodes.Status200OK, StatusOf(result));

            var questions = Body(result)["pages"]![0]!["questions"]!.AsArray();

            foreach (var question in questions)
            {
                questionIds.Add(question!["Id"]!.GetValue<string>());
            }

            orders.Add(questions.Single(question => question!["Id"]!.GetValue<string>() == "rank")!["Choices"]!.AsArray().Select(choice => choice!["Id"]!.GetValue<string>()).ToList());
        }

        // Assert
        Assert.All(orders, order => Assert.Equal(authored.Order(StringComparer.Ordinal), order.Order(StringComparer.Ordinal)));
        Assert.Contains(orders, order => !order.SequenceEqual(authored));
        Assert.Equal(new[] { "rank" }, questionIds.ToArray());
    }

    private const string UncheckedKind = "Unchecked";

    private static Task<RuntimeTestHarness> CreateHarnessAsync()
        => RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddSurveyControl<RankingControlHandler>(SurveyControlNames.Ranking, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.Scorable);
            services.AddSurveyControlScorer<RankingScorer>();
            services.AddSurveyContextKind(TicketCheck.Kind, LocalizationSource.Create<SurveysApiSecurityTests>("Ticket"));
            services.AddSurveyContextKind(UncheckedKind, LocalizationSource.Create<SurveysApiSecurityTests>("Unchecked"));
            services.AddSurveyContextOwnershipCheck<TicketCheck>();
        });

    private static Task<IResult> StartAsync(RuntimeTestHarness harness, StartResponseInput input)
        => harness.RunAsync(services => SurveysApiEndpoints.StartAsync(
            input,
            Http(services),
            Allow(),
            services.GetRequiredService<ISurveyResponseService>(),
            services.GetRequiredService<SurveyStore>(),
            services.GetRequiredService<SurveyCollectorStore>(),
            services.GetRequiredService<SurveyContextGuard>()));

    private static Task<IResult> DefinitionAsync(RuntimeTestHarness harness, string surveyId)
        => harness.RunAsync(services => SurveysApiEndpoints.GetDefinitionAsync(
            surveyId,
            null,
            null,
            Http(services),
            Allow(),
            services.GetRequiredService<SurveyStore>(),
            services.GetRequiredService<SurveyCollectorStore>(),
            services.GetRequiredService<ISurveyVersionCache>(),
            services.GetRequiredService<ISurveyResponseService>(),
            services.GetRequiredService<SurveyRuntimeVersions>(),
            services.GetRequiredService<SurveyAccessEvaluator>(),
            services.GetRequiredService<SurveyPlanBuilder>(),
            services.GetRequiredService<SurveyRespondentProjector>(),
            services.GetRequiredService<IClock>()));

    private static Task<SurveyResponse> LatestAsync(RuntimeTestHarness harness, Survey survey)
        => harness.RunAsync(async services => (await ListAsync(services, survey)).OrderByDescending(response => response.Number, StringComparer.Ordinal).First());

    private static Task<int> CountAsync(RuntimeTestHarness harness, Survey survey)
        => harness.RunAsync(async services => (await ListAsync(services, survey)).Count);

    private static async Task<IReadOnlyList<SurveyResponse>> ListAsync(IServiceProvider services, Survey survey)
    {
        var surveyId = survey.ItemId;
        var responses = await services.GetRequiredService<global::YesSql.ISession>()
            .Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.ResponsesCollectionName)
            .ListAsync();

        return responses.ToList();
    }

    private static DefaultHttpContext Http(IServiceProvider services)
        => new()
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Api")),
        };

    private static IAuthorizationService Allow()
    {
        var service = new Mock<IAuthorizationService>();

        service.Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        service.Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return service.Object;
    }

    private static int StatusOf(IResult result)
        => result is IStatusCodeHttpResult status ? status.StatusCode ?? StatusCodes.Status200OK : StatusCodes.Status200OK;

    private static JsonNode Body(IResult result)
        => JsonSerializer.SerializeToNode(((IValueHttpResult)result).Value);

    private sealed class TicketCheck : ISurveyContextOwnershipCheck
    {
        public const string Kind = "Ticket";

        public const string OwnedId = "mine";

        public bool CanCheck(string kind)
            => string.Equals(kind, Kind, StringComparison.Ordinal);

        public Task<bool> IsOwnerAsync(Survey survey, RecordRef context, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default)
            => Task.FromResult(identity?.UserId == UserId && context.Id == OwnedId);
    }
}
