using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Records;
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

    public CourseworkEventDisplayDriver(ISession session)
    {
        _session = session;
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
