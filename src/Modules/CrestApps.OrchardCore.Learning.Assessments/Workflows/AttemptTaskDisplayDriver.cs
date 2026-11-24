using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

public sealed class AttemptTaskDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, AttemptTaskViewModel>
    where TActivity : AttemptTaskActivity<TActivity>, ITask
{
    protected override void EditActivity(TActivity activity, AttemptTaskViewModel model)
    {
        model.AttemptId = activity.AttemptId;
        model.Reason = activity.Reason;
        model.OutputName = activity.OutputName;
    }

    protected override void UpdateActivity(AttemptTaskViewModel model, TActivity activity)
    {
        activity.AttemptId = string.IsNullOrWhiteSpace(model.AttemptId) ? null : model.AttemptId.Trim();
        activity.Reason = string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim();
        activity.OutputName = string.IsNullOrWhiteSpace(model.OutputName) ? null : model.OutputName.Trim();
    }
}
