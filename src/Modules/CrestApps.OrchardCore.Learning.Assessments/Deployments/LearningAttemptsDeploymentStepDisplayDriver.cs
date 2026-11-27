using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Assessments.Deployments;

internal sealed class LearningAttemptsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningAttemptsDeploymentStep>
{
    private readonly global::YesSql.ISession _session;

    internal readonly IStringLocalizer S;

    public LearningAttemptsDeploymentStepDisplayDriver(
        global::YesSql.ISession session,
        IStringLocalizer<LearningAttemptsDeploymentStepDisplayDriver> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(LearningAttemptsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningAttemptsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningAttemptsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(LearningAttemptsDeploymentStep step, BuildEditorContext context)
        => Initialize<LearningAttemptsDeploymentStepViewModel>("LearningAttemptsDeploymentStep_Fields_Edit", async model =>
        {
            model.QuizId = step.QuizId;
            model.IncludeOverrides = step.IncludeOverrides;

            var quizzes = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == AssessmentsConstants.ContentTypes.Quiz && index.Latest)
                .OrderBy(index => index.DisplayText)
                .Take(500)
                .ListAsync();

            model.QuizOptions = quizzes.Select(quiz => new SelectListItem(quiz.DisplayText, quiz.ContentItemId, quiz.ContentItemId == step.QuizId)).ToList();
        }).Location(l => l.Zone("Content", "1").Card(S["Quiz Attempts"], "10"));

    public override async Task<IDisplayResult> UpdateAsync(LearningAttemptsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new LearningAttemptsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        step.QuizId = string.IsNullOrWhiteSpace(model.QuizId) ? null : model.QuizId;
        step.IncludeOverrides = model.IncludeOverrides;

        return Edit(step, context);
    }
}
