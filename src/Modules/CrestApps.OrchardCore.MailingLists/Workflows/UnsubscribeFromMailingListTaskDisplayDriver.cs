using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Edits the Unsubscribe from Mailing List task.
/// </summary>
internal sealed class UnsubscribeFromMailingListTaskDisplayDriver : ActivityDisplayDriver<UnsubscribeFromMailingListTask, UnsubscribeFromMailingListTaskViewModel>
{
    protected override void EditActivity(UnsubscribeFromMailingListTask activity, UnsubscribeFromMailingListTaskViewModel model)
    {
        model.List = activity.List;
        model.Email = activity.Email;
    }

    protected override void UpdateActivity(UnsubscribeFromMailingListTaskViewModel model, UnsubscribeFromMailingListTask activity)
    {
        activity.List = model.List?.Trim();
        activity.Email = model.Email?.Trim();
    }
}
