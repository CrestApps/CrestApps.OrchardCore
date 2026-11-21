using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Edits the settings of a Surveys workflow task.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public sealed class SurveyTaskDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, SurveyTaskViewModel>
    where TActivity : SurveyTaskActivity<TActivity>, ITask
{
    /// <inheritdoc />
    protected override void EditActivity(TActivity activity, SurveyTaskViewModel model)
    {
        model.SurveyId = activity.SurveyId;
        model.ResponseId = activity.ResponseId;
        model.UserId = activity.UserId;
        model.Email = activity.Email;
        model.DisplayName = activity.DisplayName;
        model.ContextKind = activity.ContextKind;
        model.ContextId = activity.ContextId;
        model.ExpiresInDays = activity.ExpiresInDays;
        model.ExtraMinutes = activity.ExtraMinutes;
        model.ExtraAttempts = activity.ExtraAttempts;
        model.Reason = activity.Reason;
        model.PropertyName = activity.PropertyName;
        model.PropertyValue = activity.PropertyValue;
        model.OutputName = activity.OutputName;
    }

    /// <inheritdoc />
    protected override void UpdateActivity(SurveyTaskViewModel model, TActivity activity)
    {
        activity.SurveyId = Clean(model.SurveyId);
        activity.ResponseId = Clean(model.ResponseId);
        activity.UserId = Clean(model.UserId);
        activity.Email = Clean(model.Email);
        activity.DisplayName = Clean(model.DisplayName);
        activity.ContextKind = Clean(model.ContextKind);
        activity.ContextId = Clean(model.ContextId);
        activity.ExpiresInDays = Clean(model.ExpiresInDays);
        activity.ExtraMinutes = Clean(model.ExtraMinutes);
        activity.ExtraAttempts = Clean(model.ExtraAttempts);
        activity.Reason = Clean(model.Reason);
        activity.PropertyName = Clean(model.PropertyName);
        activity.PropertyValue = Clean(model.PropertyValue);
        activity.OutputName = Clean(model.OutputName);
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
