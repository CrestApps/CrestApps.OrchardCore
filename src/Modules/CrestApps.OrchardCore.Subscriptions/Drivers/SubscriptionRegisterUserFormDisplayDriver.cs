
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

/// <summary>
/// Provides editor shapes and validation for the user registration step in a subscription flow.
/// </summary>
public sealed class SubscriptionRegisterUserFormDisplayDriver : DisplayDriver<SubscriptionRegisterUserForm>
{
    private readonly UserManager<IUser> _userManager;
    private readonly IdentityOptions _identityOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRegisterUserFormDisplayDriver"/> class.
    /// </summary>
    /// <param name="userManager">The user manager used to validate unique user names and email addresses.</param>
    /// <param name="identityOptions">The identity options that control user validation rules.</param>
    /// <param name="stringLocalizer">The localizer used for validation messages.</param>
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

    /// <summary>
    /// Builds the editor shape for collecting user registration details.
    /// </summary>
    /// <param name="model">The subscription registration form model.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result for the registration form editor.</returns>
    public override IDisplayResult Edit(SubscriptionRegisterUserForm model, BuildEditorContext context)
    {
        return Initialize<CreateUserViewModel>("CreateUser", vm =>
        {
            vm.UserName = model.UserName;
            vm.Email = model.Email;
            vm.HasSavedPassword = model.HasSavedPassword;
        }).Location("Content")
        .OnGroup(UserRegistrationCheckoutFlowDisplayDriver.UserRegistrationFormGroupId);
    }

    /// <summary>
    /// Updates the subscription registration form from the posted editor values and validates the user data.
    /// </summary>
    /// <param name="form">The subscription registration form to update.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The updated editor display result, or <see langword="null"/> when the active group is not the registration step.</returns>
    public override async Task<IDisplayResult> UpdateAsync(SubscriptionRegisterUserForm form, UpdateEditorContext context)
    {
        if (!string.Equals(context.GroupId, UserRegistrationCheckoutFlowDisplayDriver.UserRegistrationFormGroupId, StringComparison.Ordinal))
        {
            return null;
        }

        var model = new CreateUserViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // One answer for a taken user name and a taken e-mail address, so the form doesn't tell which accounts exist.
        if (await _userManager.FindByNameAsync(model.UserName) != null ||
            (_identityOptions.User.RequireUniqueEmail && await _userManager.FindByEmailAsync(model.Email) != null))
        {
            context.Updater.ModelState.AddModelError(Prefix, string.Empty, S["This user name or email address can't be used. Choose others, or sign in if you already have an account."]);
        }

        form.UserName = model.UserName;
        form.Email = model.Email;

        await ValidateAndProtectPasswordAsync(context.Updater, model, form);

        return Edit(form, context);
    }

    private async Task ValidateAndProtectPasswordAsync(IUpdateModel updater, CreateUserViewModel model, SubscriptionRegisterUserForm form)
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

        // The site's password policy is checked here, when the password is entered, not when the account is made after
        // the payment.
        if (hasValidPassword)
        {
            foreach (var validator in _userManager.PasswordValidators)
            {
                var result = await validator.ValidateAsync(_userManager, null, model.Password);

                foreach (var error in result.Errors)
                {
                    hasValidPassword = false;
                    updater.ModelState.AddModelError(Prefix, nameof(model.Password), error.Description);
                }
            }
        }

        if (hasValidPassword)
        {
            form.Password = model.Password;
        }
    }
}
