using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Assessments.Workflows;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

public sealed class AssessmentsWorkflowTests
{
    [Fact]
    public void AnAttemptEvent_StartsForItsQuizzesOnly_AndNotForAWorkflowsOwnChange()
    {
        // Arrange
        var activity = new AttemptGradedEvent(new PassThroughStringLocalizer<AttemptGradedEvent>()) { QuizIds = ["quiz"] };

        // Act
        var matching = activity.CanExecute(Context(("QuizId", "quiz"), ("Actor", "grader")), null);
        var otherQuiz = activity.CanExecute(Context(("QuizId", "other"), ("Actor", "grader")), null);
        var byWorkflow = activity.CanExecute(Context(("QuizId", "quiz"), ("Actor", AttemptEventActivity.WorkflowActor)), null);
        activity.IncludeWorkflowChanges = true;
        var byWorkflowAllowed = activity.CanExecute(Context(("QuizId", "quiz"), ("Actor", AttemptEventActivity.WorkflowActor)), null);

        // Assert
        Assert.True(matching);
        Assert.False(otherQuiz);
        Assert.False(byWorkflow);
        Assert.True(byWorkflowAllowed);
    }

    [Fact]
    public async Task TheVoidTask_VoidsTheAttemptAsTheWorkflow_AndReportsMissingAttempts()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1"]);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));

        // Act
        var (done, missing, noReason) = await h.RunAsync(async services =>
        {
            var task = new VoidAttemptTask(services.GetRequiredService<IAssessmentAttemptService>(), Evaluator(), new PassThroughStringLocalizer<VoidAttemptTask>()) { AttemptId = attempt.ItemId, Reason = "Copied answers" };
            var first = await task.ExecuteAsync(Context(), null);

            task.AttemptId = "no-such-attempt";
            var second = await task.ExecuteAsync(Context(), null);

            task.AttemptId = attempt.ItemId;
            task.Reason = null;
            var third = await task.ExecuteAsync(Context(), null);

            return (first.Outcomes.Single(), second.Outcomes.Single(), third.Outcomes.Single());
        });

        // Assert
        Assert.Equal("Done", done);
        Assert.Equal("NotFound", missing);
        Assert.Equal("Failed", noReason);
        var stored = await h.RunAsync(async services => await services.GetRequiredService<CrestApps.OrchardCore.Learning.Assessments.Core.Services.AssessmentAttemptStore>().FindByIdAsync(attempt.ItemId));
        Assert.Equal(AttemptStatus.Voided, stored.Status);
        Assert.Equal(AttemptEventActivity.WorkflowActor, stored.ScoreHistory.Last().Actor);
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
        => new(
            new WorkflowType { WorkflowTypeId = "t1" },
            new Workflow { WorkflowId = "w1" },
            input.ToDictionary(pair => pair.Key, pair => pair.Value),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
