using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using CrestApps.OrchardCore.Banking.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Checks.Drivers;

/// <summary>
/// The Checks card on one of the business's own bank accounts: the check layout its checks print with when the person
/// printing doesn't choose one. Kept in the account's properties, so it travels with the account.
/// </summary>
internal sealed class BankAccountCheckSettingsDisplayDriver : DisplayDriver<BankAccount>
{
    private readonly INamedCatalogManager<CheckLayout> _layouts;

    internal readonly IStringLocalizer S;

    public BankAccountCheckSettingsDisplayDriver(
        INamedCatalogManager<CheckLayout> layouts,
        IStringLocalizer<BankAccountCheckSettingsDisplayDriver> stringLocalizer)
    {
        _layouts = layouts;
        S = stringLocalizer;
    }

    // Its own prefix, so its fields never meet the bank account editor's.
    protected override void BuildPrefix(BankAccount model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? nameof(BankAccountCheckSettings) : htmlFieldPrefix + "." + nameof(BankAccountCheckSettings);
    }

    public override async Task<IDisplayResult> EditAsync(BankAccount account, BuildEditorContext context)
    {
        // Only on the business's own accounts in the admin editor, never on a person's own account page.
        if (!account.Owner.IsEmpty || !string.IsNullOrEmpty(context.GroupId))
        {
            return null;
        }

        var settings = account.TryGet<BankAccountCheckSettings>(out var stored) ? stored : new BankAccountCheckSettings();
        var layouts = await _layouts.GetAllAsync();

        return Initialize<BankAccountCheckSettingsViewModel>("BankAccountCheckSettings_Edit", model =>
        {
            model.DefaultLayoutId = settings.DefaultLayoutId;
            model.LayoutOptions =
            [
                new SelectListItem(S["None: choose one each time"], string.Empty),
                .. layouts
                    .OrderBy(layout => layout.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(layout => new SelectListItem(layout.Name, layout.ItemId, layout.ItemId == settings.DefaultLayoutId)),
            ];
        }).Location(l => l.Zone("Content", "25").Card(S["Checks"], "40"));
    }

    public override async Task<IDisplayResult> UpdateAsync(BankAccount account, UpdateEditorContext context)
    {
        if (!account.Owner.IsEmpty || !string.IsNullOrEmpty(context.GroupId))
        {
            return null;
        }

        var model = new BankAccountCheckSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // The card was not on the form (the account became the business's in this save): keep what is stored.
        if (!model.Shown)
        {
            return await EditAsync(account, context);
        }

        var layoutId = string.IsNullOrWhiteSpace(model.DefaultLayoutId) ? null : model.DefaultLayoutId.Trim();

        if (layoutId is not null && await _layouts.FindByIdAsync(layoutId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.DefaultLayoutId), S["Choose one of the check layouts."]);

            return await EditAsync(account, context);
        }

        account.Put(new BankAccountCheckSettings { DefaultLayoutId = layoutId });

        return await EditAsync(account, context);
    }
}
