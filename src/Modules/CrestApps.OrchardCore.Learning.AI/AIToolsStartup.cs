using CrestApps.Core.AI;
using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.AI.Tools;
using CrestApps.OrchardCore.Learning.Assessments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.AI;

/// <summary>
/// Registers the Learning AI tools that administrators can pick on AI profiles: the learner's progress and next lesson,
/// and the overdue and expiring learners within the caller's scope.
/// </summary>
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<MyLearningProgressTool>(LearningAIConstants.Tools.MyProgress)
            .WithTitle(S["My learning progress"])
            .WithDescription(S["Lists the signed-in learner's courses with their progress, due dates and grades."])
            .WithCategory(S["Learning"])
            .Selectable();

        services.AddCoreAITool<MyNextLessonTool>(LearningAIConstants.Tools.MyNextLesson)
            .WithTitle(S["My next lesson"])
            .WithDescription(S["Finds the next lesson the signed-in learner can open."])
            .WithCategory(S["Learning"])
            .Selectable();

        services.AddCoreAITool<OverdueLearnersTool>(LearningAIConstants.Tools.OverdueLearners)
            .WithTitle(S["Overdue learners"])
            .WithDescription(S["Lists the learners whose course is overdue, within the user's scope."])
            .WithCategory(S["Learning"])
            .Selectable();

        services.AddCoreAITool<ExpiringLearnersTool>(LearningAIConstants.Tools.ExpiringLearners)
            .WithTitle(S["Learners whose access ends soon"])
            .WithDescription(S["Lists the learners whose access to a course ends soon, within the user's scope."])
            .WithCategory(S["Learning"])
            .Selectable();
    }
}

/// <summary>
/// Registers the explain-my-quiz-result tool when Learning Assessments is on.
/// </summary>
[RequireFeatures(AssessmentsConstants.Features.Assessments)]
public sealed class AssessmentsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssessmentsAIToolsStartup(IStringLocalizer<AssessmentsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ExplainMyQuizResultTool>(LearningAIConstants.Tools.ExplainMyResult)
            .WithTitle(S["Explain my quiz result"])
            .WithDescription(S["Explains the signed-in learner's quiz result as far as the quiz's review policy allows."])
            .WithCategory(S["Learning"])
            .Selectable();
    }
}
