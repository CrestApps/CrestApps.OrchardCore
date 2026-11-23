using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.MailingLists.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.MailingLists.Drivers;

/// <summary>
/// The Provider card of the mailing list editor: where the list lives. A provider module adds its own card for the
/// list's settings at the provider.
/// </summary>
internal sealed class MailingListProviderDisplayDriver : DisplayDriver<MailingList>
{
    private readonly IMailingListProviderResolver _providers;

    internal readonly IStringLocalizer S;

    public MailingListProviderDisplayDriver(
        IMailingListProviderResolver providers,
        IStringLocalizer<MailingListProviderDisplayDriver> stringLocalizer)
    {
        _providers = providers;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(MailingList list, BuildEditorContext context)
        => Initialize<MailingListProviderViewModel>("MailingListProvider_Edit", async model =>
        {
            var name = string.IsNullOrWhiteSpace(list.ProviderName) ? MailingListsConstants.LocalProviderName : list.ProviderName;
            var provider = _providers.Find(name);

            model.ProviderName = name;
            model.ProviderMissing = provider is null;
            model.ProviderNotConfigured = provider is not null && !await provider.IsConfiguredAsync();
            model.Providers = [.. _providers.GetAll().Select(item => new SelectListItem(item.DisplayName?.Value ?? item.Name, item.Name, string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))];

            if (provider is null)
            {
                model.Providers.Add(new SelectListItem(S["{0} (not available)", name], name, true));
            }
        }).Location("Content:10");

    public override async Task<IDisplayResult> UpdateAsync(MailingList list, UpdateEditorContext context)
    {
        var model = new MailingListProviderViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var name = string.IsNullOrWhiteSpace(model.ProviderName) ? MailingListsConstants.LocalProviderName : model.ProviderName.Trim();

        // The select offers the providers that are on and the list's own provider. Anything else keeps the list's
        // provider; a recipe may still name a provider whose feature is not on yet, and the list waits for it.
        if (_providers.Find(name) is not null || string.Equals(name, list.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            list.ProviderName = name;
        }

        return Edit(list, context);
    }
}
