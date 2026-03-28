using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Tasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;

internal sealed class SetContactCommunicationPreferenceActivityTaskDisplayDriver : ActivityDisplayDriver<SetContactCommunicationPreferenceActivityTask, SetContactCommunicationPreferenceActivityTaskViewModel>
{
    internal readonly IStringLocalizer S;

    public SetContactCommunicationPreferenceActivityTaskDisplayDriver(IStringLocalizer<TryAgainActivityTaskDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override void EditActivity(SetContactCommunicationPreferenceActivityTask activity, SetContactCommunicationPreferenceActivityTaskViewModel model)
    {
        model.SetDoNotCall = activity.SetDoNotCall;
        model.SetDoNotSms = activity.SetDoNotSms;
        model.SetDoNotEmail = activity.SetDoNotEmail;
        model.SetDoNotChat = activity.SetDoNotChat;
    }

    public override async Task<IDisplayResult> UpdateAsync(SetContactCommunicationPreferenceActivityTask activity, UpdateEditorContext context)
    {
        var model = new SetContactCommunicationPreferenceActivityTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!model.SetDoNotCall.HasValue &&
            !model.SetDoNotSms.HasValue &&
            !model.SetDoNotEmail.HasValue &&
            !model.SetDoNotChat.HasValue)
        {
            context.Updater.ModelState.AddModelError(Prefix, string.Empty, S["At least one of the preferences must be set"]);
        }

        activity.SetDoNotCall = model.SetDoNotCall;
        activity.SetDoNotSms = model.SetDoNotSms;
        activity.SetDoNotEmail = model.SetDoNotEmail;
        activity.SetDoNotChat = model.SetDoNotChat;

        return await EditAsync(activity, context);
    }
}
