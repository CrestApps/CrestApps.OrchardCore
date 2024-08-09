using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users;
using OrchardCore.Users.ViewModels;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class SubscriptionRegisterUserFormDisplayDriver : DisplayDriver<SubscriptionRegisterUserForm>
{
    private readonly UserManager<IUser> _userManager;
    private readonly IdentityOptions _identityOptions;

    internal readonly IStringLocalizer S;

    public SubscriptionRegisterUserFormDisplayDriver(
        UserManager<IUser> userManager,
        IOptions<IdentityOptions> identityOptions,
        IStringLocalizer<SubscriptionRegisterUserFormDisplayDriver> stringLocalizer
        )
    {
        _userManager = userManager;
        _identityOptions = identityOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubscriptionRegisterUserForm model, BuildEditorContext context)
    {
        return Initialize<RegisterViewModel>("SubscriptionRegisterUserFormIdentifier", vm =>
        {
            vm.UserName = model.UserName;
            vm.Email = model.Email;
        }).Location("Content")
        .OnGroup(UserRegistrationSubscriptionFlowDisplayDriver.UserRegistrationFormGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionRegisterUserForm form, UpdateEditorContext context)
    {
        if (!string.Equals(context.GroupId, UserRegistrationSubscriptionFlowDisplayDriver.UserRegistrationFormGroupId, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new RegisterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (await _userManager.FindByNameAsync(model.UserName) != null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserName), S["A user with the same username already exists."]);
        }
        else if (_identityOptions.User.RequireUniqueEmail && await _userManager.FindByEmailAsync(model.Email) != null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Email), S["A user with the same email address already exists."]);
        }

        form.UserName = model.UserName;
        form.Email = model.Email;
        form.Password = model.Password;

        return Edit(form, context);
    }
}
