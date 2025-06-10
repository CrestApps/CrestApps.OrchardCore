using CrestApps.OrchardCore.Subscriptions.Drivers.Steps;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users;

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
        return Initialize<CreateUserViewModel>("CreateUser", vm =>
        {
            vm.UserName = model.UserName;
            vm.Email = model.Email;
            vm.HasSavedPassword = model.HasSavedPassword;
        }).Location("Content")
        .OnGroup(UserRegistrationSubscriptionFlowDisplayDriver.UserRegistrationFormGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionRegisterUserForm form, UpdateEditorContext context)
    {
        if (!string.Equals(context.GroupId, UserRegistrationSubscriptionFlowDisplayDriver.UserRegistrationFormGroupId, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new CreateUserViewModel();

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

        ValidateAndProtectPassword(context.Updater, model, form);

        return Edit(form, context);
    }

    private void ValidateAndProtectPassword(IUpdateModel updater, CreateUserViewModel model, SubscriptionRegisterUserForm form)
    {
        var validatePassword =
            !form.HasSavedPassword ||
            !string.IsNullOrEmpty(model.Password) ||
            !string.IsNullOrEmpty(model.ConfirmPassword);

        if (!validatePassword)
        {
            return;
        }

        var hasValidPassword = true;

        if (string.IsNullOrEmpty(model.Password))
        {
            hasValidPassword = false;
            updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["Password is a required value"]);
        }

        if (string.IsNullOrEmpty(model.ConfirmPassword))
        {
            hasValidPassword = false;
            updater.ModelState.AddModelError(Prefix, nameof(model.ConfirmPassword), S["Password Confirmation is a required value"]);
        }

        if (model.Password != model.ConfirmPassword)
        {
            hasValidPassword = false;
            updater.ModelState.AddModelError(Prefix, nameof(model.ConfirmPassword), S["Password and Password Confirmation values must be the same."]);
        }

        if (hasValidPassword)
        {
            form.Password = model.Password;
        }
    }
}
