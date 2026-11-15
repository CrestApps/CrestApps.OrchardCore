using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Workflows.Display;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

public sealed class AttemptEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, AttemptEventActivityViewModel>
    where TActivity : AttemptEventActivity
{
    private readonly global::YesSql.ISession _session;

    public AttemptEventActivityDisplayDriver(global::YesSql.ISession session)
    {
        _session = session;
    }

    protected override async ValueTask EditActivityAsync(TActivity activity, AttemptEventActivityViewModel model)
    {
        model.QuizIds = activity.QuizIds.ToArray();
        model.IncludeWorkflowChanges = activity.IncludeWorkflowChanges;

        var quizzes = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == AssessmentsConstants.ContentTypes.Quiz && index.Latest)
            .OrderBy(index => index.DisplayText)
            .Take(500)
            .ListAsync();

        model.QuizOptions = quizzes.Select(quiz => new SelectListItem(quiz.DisplayText, quiz.ContentItemId, model.QuizIds.Contains(quiz.ContentItemId))).ToList();
    }

    protected override void UpdateActivity(AttemptEventActivityViewModel model, TActivity activity)
    {
        activity.QuizIds = (model.QuizIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        activity.IncludeWorkflowChanges = model.IncludeWorkflowChanges;
    }
}
