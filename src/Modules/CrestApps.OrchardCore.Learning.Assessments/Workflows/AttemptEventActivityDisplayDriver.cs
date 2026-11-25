using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

public sealed class AttemptEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, AttemptEventActivityViewModel>
    where TActivity : AttemptEventActivity
{
    private readonly global::YesSql.ISession _session;

    internal readonly IStringLocalizer S;

    public AttemptEventActivityDisplayDriver(
        global::YesSql.ISession session,
        IStringLocalizer<AttemptEventActivityDisplayDriver<TActivity>> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
    {
        return Initialize<AttemptEventActivityViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Trigger"], "10"));
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
