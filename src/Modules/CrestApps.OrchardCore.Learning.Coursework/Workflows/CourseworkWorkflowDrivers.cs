using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Coursework.Workflows;

public class CourseworkEventViewModel
{
    public string[] CourseworkIds { get; set; } = [];

    [BindNever]
    public List<SelectListItem> CourseworkOptions { get; set; } = [];
}

public class ReturnCourseworkTaskViewModel
{
    public string SubmissionId { get; set; }

    public string Feedback { get; set; }
}

public sealed class CourseworkEventDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, CourseworkEventViewModel>
    where TActivity : CourseworkEventActivity
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public CourseworkEventDisplayDriver(
        ISession session,
        IStringLocalizer<CourseworkEventDisplayDriver<TActivity>> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
    {
        return Initialize<CourseworkEventViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Trigger"], "10"));
    }

    protected override async ValueTask EditActivityAsync(TActivity activity, CourseworkEventViewModel model)
    {
        model.CourseworkIds = activity.CourseworkIds.ToArray();
        model.CourseworkOptions = (await _session.QueryIndex<ContentItemIndex>(index => index.ContentType == CourseworkConstants.ContentTypes.Coursework && index.Latest)
                .OrderBy(index => index.DisplayText)
                .Take(500)
                .ListAsync())
            .Select(index => new SelectListItem(index.DisplayText, index.ContentItemId, model.CourseworkIds.Contains(index.ContentItemId)))
            .ToList();
    }

    protected override void UpdateActivity(CourseworkEventViewModel model, TActivity activity)
        => activity.CourseworkIds = (model.CourseworkIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
}

public sealed class ReturnCourseworkTaskDisplayDriver : ActivityDisplayDriver<ReturnCourseworkTask, ReturnCourseworkTaskViewModel>
{
    internal readonly IStringLocalizer S;

    public ReturnCourseworkTaskDisplayDriver(IStringLocalizer<ReturnCourseworkTaskDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ReturnCourseworkTask activity, BuildEditorContext context)
    {
        return Initialize<ReturnCourseworkTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Task"], "10"));
    }

    protected override void EditActivity(ReturnCourseworkTask activity, ReturnCourseworkTaskViewModel model)
    {
        model.SubmissionId = activity.SubmissionId;
        model.Feedback = activity.Feedback;
    }

    protected override void UpdateActivity(ReturnCourseworkTaskViewModel model, ReturnCourseworkTask activity)
    {
        activity.SubmissionId = model.SubmissionId?.Trim();
        activity.Feedback = model.Feedback?.Trim();
    }
}
