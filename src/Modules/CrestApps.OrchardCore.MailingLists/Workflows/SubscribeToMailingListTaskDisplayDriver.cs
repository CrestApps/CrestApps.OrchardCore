using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Edits the Subscribe to Mailing List task.
/// </summary>
internal sealed class SubscribeToMailingListTaskDisplayDriver : ActivityDisplayDriver<SubscribeToMailingListTask, SubscribeToMailingListTaskViewModel>
{
    protected override void EditActivity(SubscribeToMailingListTask activity, SubscribeToMailingListTaskViewModel model)
    {
        model.List = activity.List;
        model.Email = activity.Email;
        model.PersonName = activity.PersonName;
    }

    protected override void UpdateActivity(SubscribeToMailingListTaskViewModel model, SubscribeToMailingListTask activity)
    {
        activity.List = model.List?.Trim();
        activity.Email = model.Email?.Trim();
        activity.PersonName = model.PersonName?.Trim();
    }
}
