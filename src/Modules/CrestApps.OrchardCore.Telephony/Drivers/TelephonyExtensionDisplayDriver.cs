using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Drivers;

/// <summary>
/// Renders the admin list and editor for an internal <see cref="TelephonyExtension"/>. The rules an extension must
/// satisfy (a unique number and a user who exists) belong to its handler, so an import enforces the same set.
/// </summary>
internal sealed class TelephonyExtensionDisplayDriver : DisplayDriver<TelephonyExtension>
{
    private readonly ISession _session;
    private readonly UserManager<IUser> _userManager;

    public TelephonyExtensionDisplayDriver(
        ISession session,
        UserManager<IUser> userManager)
    {
        _session = session;
        _userManager = userManager;
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
        }).Location("Content:1%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(TelephonyExtension extension, UpdateEditorContext context)
    {
        var model = new TelephonyExtensionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var number = model.Number?.Trim();
        var user = string.IsNullOrWhiteSpace(model.UserId)
            ? null
            : await _userManager.FindByIdAsync(model.UserId);

        extension.Number = number;

        // An unknown user keeps the identifier the operator picked, so the handler reports it as not found.
        extension.UserId = user is null ? model.UserId : await _userManager.GetUserIdAsync(user);

        if (user is not null)
        {
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
