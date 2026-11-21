using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.Workflows;
using CrestApps.OrchardCore.Records;
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
        var observer = new SurveysWorkflowObserver(new SurveyWorkflowInputBuilder(Registry()), trigger);
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
        Assert.Equal("LearningEnrollment", input["ContextKind"]);
    }

    [Fact]
    public async Task TheObserver_IgnoresTestResponses_AndRaisesPassedOrFailedWhenGraded()
    {
        // Arrange
        var trigger = new RecordingTrigger();
        var observer = new SurveysWorkflowObserver(new SurveyWorkflowInputBuilder(Registry()), trigger);
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
        var observer = new SurveysWorkflowObserver(new SurveyWorkflowInputBuilder(Registry()), trigger);
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
