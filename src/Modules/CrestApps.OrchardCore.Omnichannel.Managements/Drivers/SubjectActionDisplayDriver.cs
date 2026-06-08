using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class SubjectActionDisplayDriver : DisplayDriver<SubjectAction>
{
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;

    internal readonly IStringLocalizer S;

    public SubjectActionDisplayDriver(
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        IStringLocalizer<SubjectActionDisplayDriver> stringLocalizer)
    {
        _dispositionsCatalog = dispositionsCatalog;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(SubjectAction action, BuildEditorContext context)
    {
        return Combine(
            Initialize<SubjectActionViewModel>("SubjectActionFields_Edit", async model => await PopulateAsync(model, action))
                .Location("Content:1"),
            Initialize<SubjectActionViewModel>("SubjectActionCommunicationPreferences_Edit", async model => await PopulateAsync(model, action))
                .Location("Content:100"));
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectAction action, UpdateEditorContext context)
    {
        var model = new SubjectActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DispositionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DispositionId), S["Disposition is a required field."]);
        }

        action.DispositionId = model.DispositionId;

        if (model.ShowCommunicationPreferences)
        {
            action.SetDoNotCall = model.SetDoNotCall;
            action.SetDoNotSms = model.SetDoNotSms;
            action.SetDoNotEmail = model.SetDoNotEmail;
            action.SetDoNotChat = model.SetDoNotChat;
        }
        else
        {
            action.SetDoNotCall = null;
            action.SetDoNotSms = null;
            action.SetDoNotEmail = null;
            action.SetDoNotChat = null;
        }

        return await EditAsync(action, context);
    }

    private async Task PopulateAsync(SubjectActionViewModel model, SubjectAction action)
    {
        model.DispositionId = action.DispositionId;
        model.ShowCommunicationPreferences =
            action.SetDoNotCall.HasValue ||
            action.SetDoNotSms.HasValue ||
            action.SetDoNotEmail.HasValue ||
            action.SetDoNotChat.HasValue;
        model.SetDoNotCall = action.SetDoNotCall;
        model.SetDoNotSms = action.SetDoNotSms;
        model.SetDoNotEmail = action.SetDoNotEmail;
        model.SetDoNotChat = action.SetDoNotChat;

        var dispositions = await _dispositionsCatalog.GetAllAsync();

        model.Dispositions = dispositions
            .Select(d => new SelectListItem
            {
                Text = d.Name,
                Value = d.ItemId,
                Selected = string.Equals(d.ItemId, action.DispositionId, StringComparison.OrdinalIgnoreCase),
            })
            .OrderBy(x => x.Text);
    }
}
