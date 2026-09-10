using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Drivers;

/// <summary>
/// Renders the admin list and editor for an internal <see cref="TelephonyExtension"/>, and enforces a unique
/// dialed number and a valid assigned user on save.
/// </summary>
internal sealed class TelephonyExtensionDisplayDriver : DisplayDriver<TelephonyExtension>
{
    private readonly ISession _session;
    private readonly ITelephonyExtensionStore _store;
    private readonly UserManager<IUser> _userManager;

    internal readonly IStringLocalizer S;

    public TelephonyExtensionDisplayDriver(
        ISession session,
        ITelephonyExtensionStore store,
        UserManager<IUser> userManager,
        IStringLocalizer<TelephonyExtensionDisplayDriver> stringLocalizer)
    {
        _session = session;
        _store = store;
        _userManager = userManager;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(TelephonyExtension extension, BuildDisplayContext context)
    {
        return CombineAsync(
            View("TelephonyExtension_Fields_SummaryAdmin", extension).Location("Content:1"),
            View("TelephonyExtension_Buttons_SummaryAdmin", extension).Location("Actions:5"),
            View("TelephonyExtension_DefaultMeta_SummaryAdmin", extension).Location("Meta:5"));
    }

    public override IDisplayResult Edit(TelephonyExtension extension, BuildEditorContext context)
    {
        return Initialize<TelephonyExtensionFieldsViewModel>("ExtensionFields_Edit", async model =>
        {
            model.IsNew = context.IsNew;
            model.Name = extension.Name;
            model.Number = extension.Number;
            model.UserId = extension.UserId;
            model.DisplayName = extension.DisplayName;
            model.Users = await BuildUserListAsync();
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(TelephonyExtension extension, UpdateEditorContext context)
    {
        var model = new TelephonyExtensionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var number = model.Number?.Trim();

        if (string.IsNullOrWhiteSpace(number))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Number), S["The extension number is required."]);
        }
        else
        {
            // Numbers are unique per tenant so a dialed extension resolves to exactly one user.
            var existing = await _store.FindByNumberAsync(number);

            if (existing is not null && !string.Equals(existing.ItemId, extension.ItemId, StringComparison.Ordinal))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Number), S["Extension {0} is already assigned.", number]);
            }
        }

        IUser user = null;

        if (string.IsNullOrWhiteSpace(model.UserId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserId), S["A user is required."]);
        }
        else
        {
            user = await _userManager.FindByIdAsync(model.UserId);

            if (user is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserId), S["The selected user was not found."]);
            }
        }

        extension.Number = number;

        if (user is not null)
        {
            extension.UserId = await _userManager.GetUserIdAsync(user);
            extension.UserName = await _userManager.GetUserNameAsync(user);
        }

        extension.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName)
            ? extension.UserName
            : model.DisplayName.Trim();

        // The catalog name carries the number and person so the list search can match either.
        extension.Name = string.IsNullOrWhiteSpace(number)
            ? extension.DisplayName
            : $"{number} {extension.DisplayName}".Trim();

        return Edit(extension, context);
    }

    private async Task<IEnumerable<SelectListItem>> BuildUserListAsync()
    {
        var users = await _session.Query<User, UserIndex>(x => x.IsEnabled).ListAsync();

        return users
            .Select(user => new SelectListItem(user.UserName, user.UserId))
            .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
