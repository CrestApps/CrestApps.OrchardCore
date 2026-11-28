using System.Security.Claims;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.Webhooks;
using CrestApps.OrchardCore.Surveys.Workflows;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using CrestApps.OrchardCore.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Surveys.Integrations;

public sealed class SurveysWorkflowTests
{
    [Fact]
    public void AResponseEvent_StartsForItsFiltersOnly_AndNotForAWorkflowsOwnChange()
    {
        // Arrange
        var activity = new SurveyResponseCompletedEvent(new Localizer<SurveyResponseCompletedEvent>())
        {
            SurveyIds = ["s1"],
            Kinds = ["Quiz"],
            ContextKinds = ["LearningEnrollment"],
        };

        // Act
        var matching = activity.CanExecute(Context(("SurveyId", "s1"), ("Kind", "Quiz"), ("ContextKind", "LearningEnrollment"), ("Actor", "user1")), null);
        var otherSurvey = activity.CanExecute(Context(("SurveyId", "s2"), ("Kind", "Quiz"), ("ContextKind", "LearningEnrollment")), null);
        var otherContext = activity.CanExecute(Context(("SurveyId", "s1"), ("Kind", "Quiz"), ("ContextKind", "Ticket")), null);
        var byWorkflow = activity.CanExecute(Context(("SurveyId", "s1"), ("Kind", "Quiz"), ("ContextKind", "LearningEnrollment"), ("CausedByWorkflow", true)), null);
        activity.IncludeWorkflowChanges = true;
        var byWorkflowAllowed = activity.CanExecute(Context(("SurveyId", "s1"), ("Kind", "Quiz"), ("ContextKind", "LearningEnrollment"), ("CausedByWorkflow", true)), null);

        // Assert
        Assert.True(matching);
        Assert.False(otherSurvey);
        Assert.False(otherContext);
        Assert.False(byWorkflow);
        Assert.True(byWorkflowAllowed);
    }

    [Fact]
    public void ThePageAndEndingEvents_FilterByName()
    {
        // Arrange
        var page = new SurveyPageCompletedEvent(new Localizer<SurveyPageCompletedEvent>()) { PageNames = ["page_2"] };
        var screened = new SurveyResponseScreenedOutEvent(new Localizer<SurveyResponseScreenedOutEvent>()) { EndingNames = ["too_young"] };

        // Act & Assert
        Assert.True(page.CanExecute(Context(("PageName", "page_2")), null));
        Assert.False(page.CanExecute(Context(("PageName", "page_1")), null));
        Assert.True(screened.CanExecute(Context(("EndingName", "too_young")), null));
        Assert.False(screened.CanExecute(Context(("EndingName", "other")), null));
    }

    [Fact]
    public async Task TheObserver_RaisesEachEventOnce_WithTheAnswersAndNoIdentityForAnonymousSurveys()
    {
        // Arrange
        var trigger = new RecordingTrigger();
        var observer = new SurveysWorkflowObserver(new SurveyWorkflowInputBuilder(Registry()), trigger, new ServiceCollection().BuildServiceProvider());
        var context = CompletedContext(AnonymityMode.Anonymous, "c1");

        // Act
        await observer.CompletedAsync(context);

        // Assert
        Assert.Equal([nameof(SurveyResponseCompletedEvent), nameof(SurveyResponseMatchesConditionEvent)], trigger.Events.Select(item => item.Name));
        var input = trigger.Events[0].Input;
        Assert.Equal("r1", trigger.Events[0].CorrelationId);
        Assert.Equal("s1", input["SurveyId"]);
        Assert.Null(input["UserId"]);
        Assert.Null(input["Respondent"]);
        Assert.Equal("Very unhappy", ((IDictionary<string, string>)input["Answers"])["q_sat"]);

        // R7-Mi20: with no small-group rule to count the survey's responses, its collector, version and context kind
        // can't be shown to be shared by enough people, so they are left out.
        Assert.Null(input["ContextKind"]);
        Assert.Null(input["CollectorId"]);
    }

    [Fact]
    public async Task R8Mi19_AResponseInTheOneSmallGroup_StartsNoWorkflowAndSendsNoWebhook_WhileTheOthersDo()
    {
        // Arrange: an anonymous survey (minimum 5) with five completed responses on the main link and Bob's on a link sent
        // to him, the only small group: an event with an empty collector would name him.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var results = await ResultsSqliteStore.CreateAsync();
        var version = ResultsSqliteStore.Version("v1", "s1", 1, ResultsSqliteStore.ChoiceQuestion("q1", "q_sat", "1", "5"));

        await using (var write = results.OpenSession())
        {
            foreach (var id in new[] { "a0", "a1", "a2", "a3", "a4", "bob" })
            {
                var stored = ResultsSqliteStore.Response(id, version, SurveyResponseStatus.Completed, new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc));
                stored.CollectorId = id == "bob" ? "for-bob" : "main";
                await write.SaveAsync(stored, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = results.OpenSession();
        var services = new ServiceCollection()
            .AddSingleton(new SurveyAnonymousGroups(new SurveyResultsQueries(session), new ServiceCollection().BuildServiceProvider()))
            .BuildServiceProvider();
        var trigger = new RecordingTrigger();
        var sender = new RecordingSender();
        var workflows = new SurveysWorkflowObserver(new SurveyWorkflowInputBuilder(Registry()), trigger, services);
        var webhooks = new SurveysWebhookObserver(sender, services);

        static SurveyResponseEventContext Event(string id, string collectorId)
        {
            var context = CompletedContext(AnonymityMode.Anonymous, "c1");
            context.Survey.Access.MinimumGroupSize = 5;
            context.Response.ItemId = id;
            context.Response.CollectorId = collectorId;
            context.Response.VersionNumber = 1;
            context.Response.Context = null;

            return context;
        }

        // Act
        await workflows.CompletedAsync(Event("bob", "for-bob"));
        await webhooks.CompletedAsync(Event("bob", "for-bob"));
        await workflows.CompletedAsync(Event("a0", "main"));
        await webhooks.CompletedAsync(Event("a0", "main"));

        // Assert: Bob's response starts nothing and sends nothing; a main-link response does, with its collector.
        Assert.NotEmpty(trigger.Events);
        Assert.All(trigger.Events, item => Assert.Equal("a0", item.CorrelationId));
        Assert.Equal("main", trigger.Events[0].Input[SurveyWorkflowInput.CollectorId]);
        Assert.Null(trigger.Events[0].Input["ContextId"]);
        Assert.Equal("a0:completed", Assert.Single(sender.Published).EventId);
    }

    [Fact]
    public async Task TheObserver_IgnoresTestResponses_AndRaisesPassedOrFailedWhenGraded()
    {
        // Arrange
        var trigger = new RecordingTrigger();
        var observer = new SurveysWorkflowObserver(new SurveyWorkflowInputBuilder(Registry()), trigger, new ServiceCollection().BuildServiceProvider());
        var test = CompletedContext(AnonymityMode.Identified, "c1");
        test.Response.IsTest = true;
        var graded = CompletedContext(AnonymityMode.Identified, "c1");
        graded.Response.Passed = false;

        // Act
        await observer.CompletedAsync(test);
        await observer.GradedAsync(test);
        await observer.GradedAsync(graded);

        // Assert
        Assert.Equal([nameof(SurveyResponseGradedEvent), nameof(SurveyResponseFailedEvent)], trigger.Events.Select(item => item.Name));
        Assert.Equal("user1", trigger.Events[0].Input["UserId"]);
    }

    [Fact]
    public async Task TheMatchesConditionEvent_StartsOnlyWhenTheRuleHolds_ByChoiceValueOrId()
    {
        // Arrange
        var trigger = new RecordingTrigger();
        var observer = new SurveysWorkflowObserver(new SurveyWorkflowInputBuilder(Registry()), trigger, new ServiceCollection().BuildServiceProvider());
        await observer.CompletedAsync(CompletedContext(AnonymityMode.Identified, "c1"));
        var unhappy = trigger.Events.Single(item => item.Name == nameof(SurveyResponseMatchesConditionEvent)).Input;
        trigger.Events.Clear();
        await observer.CompletedAsync(CompletedContext(AnonymityMode.Identified, "c2"));
        var happy = trigger.Events.Single(item => item.Name == nameof(SurveyResponseMatchesConditionEvent)).Input;

        var byValue = new SurveyResponseMatchesConditionEvent(new Localizer<SurveyResponseMatchesConditionEvent>())
        {
            Rule = new ConditionRule { Conditions = [new Condition { Source = "q_sat", Operator = ConditionOperator.Is, Values = ["1"] }] },
        };

        var byId = new SurveyResponseMatchesConditionEvent(new Localizer<SurveyResponseMatchesConditionEvent>())
        {
            Rule = new ConditionRule { Conditions = [new Condition { Source = "q1", Operator = ConditionOperator.IsOneOf, Values = ["c1", "x"] }] },
        };

        // Act & Assert
        Assert.True(byValue.CanExecute(Context(unhappy), null));
        Assert.False(byValue.CanExecute(Context(happy), null));
        Assert.True(byId.CanExecute(Context(unhappy), null));
        Assert.False(byId.CanExecute(Context(happy), null));
    }

    [Fact]
    public void TheEventEditor_ReadsARuleWrittenAsJson_AndIgnoresText()
    {
        // Act
        var rule = SurveyEventActivityDisplayDriver<SurveyResponseMatchesConditionEvent>.ParseRule("""{"Match":"Any","Conditions":[{"Source":"q_sat","Operator":"LessThan","Values":["3"]}]}""");
        var broken = SurveyEventActivityDisplayDriver<SurveyResponseMatchesConditionEvent>.ParseRule("not json");

        // Assert
        Assert.Equal(ConditionMatch.Any, rule.Match);
        Assert.Equal(ConditionOperator.LessThan, Assert.Single(rule.Conditions).Operator);
        Assert.Null(broken);
    }

    [Fact]
    public async Task TheVoidTask_VoidsAsTheWorkflow_AndReportsMissingResponses()
    {
        // Arrange
        var responses = new Mock<ISurveyResponseService>();
        responses.Setup(service => service.VoidAsync("r1", "Workflow", "Spam", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SurveyResponseResult { Response = new SurveyResponse { ItemId = "r1", Status = SurveyResponseStatus.Voided } });
        responses.Setup(service => service.VoidAsync("missing", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SurveyResponseResult { Error = SurveyErrorCode.NotFound });
        var provider = new Mock<IServiceProvider>();
        provider.Setup(services => services.GetService(typeof(ISurveyResponseService))).Returns(responses.Object);
        var task = new VoidSurveyResponseTask(provider.Object, Evaluator(), new Localizer<VoidSurveyResponseTask>()) { ResponseId = "r1", Reason = "Spam" };

        // Act
        var done = await task.ExecuteAsync(Context(), null);
        task.ResponseId = "missing";
        var missing = await task.ExecuteAsync(Context(), null);
        task.ResponseId = "r1";
        task.Reason = null;
        var noReason = await task.ExecuteAsync(Context(), null);

        // Assert
        Assert.Equal("Done", done.Outcomes.Single());
        Assert.Equal("NotFound", missing.Outcomes.Single());
        Assert.Equal("Failed", noReason.Outcomes.Single());
    }

    [Fact]
    public async Task AnAnonymousSurveysEvent_RunsWithoutTheRespondentsRequest_AndPutsTheRequestBack()
    {
        // Arrange: the respondent's request, signed in, with a header, a cookie and a query string.
        var request = new DefaultHttpContext();
        request.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "pat")], "test"));
        request.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        request.Request.Headers.Cookie = "sr_g=abc";
        request.Request.QueryString = new QueryString("?email=pat@example.com");

        var accessor = new HttpContextAccessor { HttpContext = request };
        HttpContext seen = null;
        var manager = new Mock<IWorkflowManager>();
        manager
            .Setup(candidate => candidate.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback(() => seen = accessor.HttpContext)
            .ReturnsAsync([]);

        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(accessor)
            .AddSingleton(manager.Object)
            .BuildServiceProvider();

        // Act
        await DeferredSurveyWorkflowTrigger.RunAsync(services, nameof(SurveyResponseCompletedEvent), new Dictionary<string, object> { [SurveyWorkflowInput.Anonymous] = true }, "r1");

        // Assert
        Assert.NotNull(seen);
        Assert.NotSame(request, seen);
        Assert.False(seen.User.Identity?.IsAuthenticated ?? false);
        Assert.Empty(seen.Request.Headers);
        Assert.Empty(seen.Request.Cookies);
        Assert.False(seen.Request.QueryString.HasValue);
        Assert.Same(request, accessor.HttpContext);
    }

    [Fact]
    public async Task AnAnonymousSurveysEvent_KeepsOnlyTheRequestsSchemeHostAndPathBase_ForLinks()
    {
        // Arrange: the respondent's request to a tenant on its own host and prefix.
        var request = new DefaultHttpContext();
        request.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "pat")], "test"));
        request.Request.Scheme = "https";
        request.Request.Host = new HostString("surveys.example.com", 8443);
        request.Request.PathBase = "/tenant";
        request.Request.Path = "/s/abc";
        request.Request.Headers.UserAgent = "Respondent browser";
        request.Request.Headers.Cookie = "sr_g=abc";
        request.Request.QueryString = new QueryString("?email=pat@example.com");

        var accessor = new HttpContextAccessor { HttpContext = request };
        (string Scheme, HostString Host, PathString PathBase, PathString Path, int Headers)? seen = null;
        var manager = new Mock<IWorkflowManager>();
        manager
            .Setup(candidate => candidate.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback(() => seen = (accessor.HttpContext.Request.Scheme, accessor.HttpContext.Request.Host, accessor.HttpContext.Request.PathBase, accessor.HttpContext.Request.Path, accessor.HttpContext.Request.Headers.Count(header => header.Key != "Host")))
            .ReturnsAsync([]);

        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(accessor)
            .AddSingleton(manager.Object)
            .BuildServiceProvider();

        // Act
        await DeferredSurveyWorkflowTrigger.RunAsync(services, nameof(SurveyResponseCompletedEvent), new Dictionary<string, object> { [SurveyWorkflowInput.Anonymous] = true }, "r1");

        // Assert
        Assert.NotNull(seen);
        Assert.Equal("https", seen.Value.Scheme);
        Assert.Equal(new HostString("surveys.example.com", 8443), seen.Value.Host);
        Assert.Equal(new PathString("/tenant"), seen.Value.PathBase);
        Assert.NotEqual(new PathString("/s/abc"), seen.Value.Path);
        Assert.Equal(0, seen.Value.Headers);
    }

    private static SurveyResponseEventContext CompletedContext(AnonymityMode anonymity, string choiceId)
        => new()
        {
            Survey = new Survey { ItemId = "s1", Name = "pulse", Kind = SurveyKinds.Survey, Access = new SurveyAccessSettings { Anonymity = anonymity }, AnonymitySecret = "never" },
            Version = new SurveyVersion
            {
                Number = 1,
                Definition = new SurveyDefinition
                {
                    Pages =
                    [
                        new SurveyPage
                        {
                            Id = "p1",
                            Name = "page_1",
                            Questions =
                            [
                                new SurveyQuestion
                                {
                                    Id = "q1",
                                    Name = "q_sat",
                                    ControlType = "Choice",
                                    Choices = [new SurveyChoice { Id = "c1", Value = "1", Text = "Very unhappy" }, new SurveyChoice { Id = "c2", Value = "5", Text = "Very happy" }],
                                },
                            ],
                        },
                    ],
                },
            },
            Response = new SurveyResponse
            {
                ItemId = "r1",
                SurveyId = "s1",
                UserId = anonymity == AnonymityMode.Anonymous ? null : "user1",
                Context = new RecordRef("LearningEnrollment", "e1"),
                Status = SurveyResponseStatus.Completed,
                Answers = { ["q1"] = new SurveyAnswer { Values = [choiceId] } },
            },
        };

    private static ISurveyControlRegistry Registry()
    {
        var registry = new Mock<ISurveyControlRegistry>();
        registry.Setup(item => item.GetHandler("Choice")).Returns(new ChoiceHandler());

        return registry.Object;
    }

    private static IWorkflowExpressionEvaluator Evaluator()
    {
        var evaluator = new Mock<IWorkflowExpressionEvaluator>();
        evaluator
            .Setup(service => service.EvaluateAsync(It.IsAny<WorkflowExpression<string>>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<System.Text.Encodings.Web.TextEncoder>()))
            .Returns((WorkflowExpression<string> expression, WorkflowExecutionContext _, System.Text.Encodings.Web.TextEncoder _) => Task.FromResult(expression.Expression));

        return evaluator.Object;
    }

    private static WorkflowExecutionContext Context(params (string Key, object Value)[] input)
        => Context(input.ToDictionary(pair => pair.Key, pair => pair.Value));

    private static WorkflowExecutionContext Context(IDictionary<string, object> input)
        => new(
            new WorkflowType { WorkflowTypeId = "t1" },
            new Workflow { WorkflowId = "w1" },
            new Dictionary<string, object>(input),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);

    private sealed class RecordingSender : ISurveyWebhookSender
    {
        public List<(string EventName, string EventId, object Payload)> Published { get; } = [];

        public void Publish(string eventName, string eventId, object payload)
            => Published.Add((eventName, eventId, payload));
    }

    private sealed class RecordingTrigger : ISurveyWorkflowTrigger
    {
        public List<(string Name, IDictionary<string, object> Input, string CorrelationId)> Events { get; } = [];

        public void Trigger(string eventName, IDictionary<string, object> input, string correlationId)
            => Events.Add((eventName, input, correlationId));
    }

    // A minimal choice handler: the condition value is the chosen ids, the text is the chosen texts.
    private sealed class ChoiceHandler : ISurveyControlHandler
    {
        public string ControlType => "Choice";

        public void Initialize(SurveyQuestion question)
        {
        }

        public Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context) => Task.CompletedTask;

        public void PreparePresentation(SurveyQuestion question, SurveyPresentationContext context)
        {
        }

        public System.Text.Json.Nodes.JsonObject GetRespondentSettings(SurveyQuestion question) => [];

        public Task ReadAnswerAsync(SurveyAnswerReadContext context) => Task.CompletedTask;

        public Task ValidateAnswerAsync(SurveyAnswerValidationContext context) => Task.CompletedTask;

        public bool IsEmpty(SurveyQuestion question, SurveyAnswer answer) => answer is null || answer.Values.Count == 0;

        public ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId) => new(answer?.Values ?? []);

        public object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId) => null;

        public DataFieldType GetFormulaType(SurveyQuestion question, string rowId) => DataFieldType.Text;

        public string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
            => string.Join(", ", (answer?.Values ?? []).Select(id => question.Choices.FirstOrDefault(choice => choice.Id == id)?.Text ?? id));

        public IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question) => [];

        public void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
        {
        }

        public IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer) => [];
    }

    private sealed class Localizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
