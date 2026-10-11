using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Json;
using OrchardCore.Settings;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Renders the account step of a subscription checkout.
/// </summary>
/// <remarks>
/// The password never reaches the checkout session document. It is protected and held in the payment session
/// cache, which expires on its own, so an abandoned checkout does not leave a recoverable credential sitting
/// in the database.
/// </remarks>
public sealed class UserRegistrationCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    /// <summary>
    /// The display group used for the registration form.
    /// </summary>
    public const string UserRegistrationFormGroupId = "Subscription";

    private readonly ISiteService _siteService;
    private readonly PaymentSessionCache _paymentSessionCache;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IDisplayManager<SubscriptionRegisterUserForm> _registerUserDisplayManager;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRegistrationCheckoutFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read subscription and registration settings.</param>
    /// <param name="paymentSessionCache">The cache that holds the password outside the database.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect the password.</param>
    /// <param name="registerUserDisplayManager">The display manager used to build the registration form.</param>
    /// <param name="documentJsonSerializerOptions">The document serializer options.</param>
    public UserRegistrationCheckoutFlowDisplayDriver(
        ISiteService siteService,
        PaymentSessionCache paymentSessionCache,
        IDataProtectionProvider dataProtectionProvider,
        IDisplayManager<SubscriptionRegisterUserForm> registerUserDisplayManager,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _siteService = siteService;
        _paymentSessionCache = paymentSessionCache;
        _dataProtectionProvider = dataProtectionProvider;
        _registerUserDisplayManager = registerUserDisplayManager;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    /// <inheritdoc/>
    protected override string StepKey
        => SubscriptionConstants.StepKey.UserRegistration;

    /// <inheritdoc/>
    protected override IDisplayResult EditStep(CheckoutFlow flow, BuildEditorContext context)
    {
        return Initialize<UserRegistrationStepViewModel>("UserRegistrationStep_Edit", async model =>
        {
            var form = new SubscriptionRegisterUserForm();

            if (flow.Session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.UserRegistration, out var node))
            {
                var step = node.Deserialize<UserRegistrationStep>(_documentJsonSerializerOptions.SerializerOptions);

                if (step is not null)
                {
                    if (step.IsGuest)
                    {
                        model.ContinueAsGuest = true;
                    }
                    else if (step.User is not null)
                    {
                        form.UserName = step.User.UserName;
                        form.Email = step.User.Email;
                        form.HasSavedPassword = await HasPasswordAsync(flow.Session.SessionId);
                    }
                }
            }

            model.SignupForm = await _registerUserDisplayManager.BuildEditorAsync(form, context.Updater, false, UserRegistrationFormGroupId, SubscriptionConstants.StepKey.UserRegistration);
            model.AllowGuestSignup = (await _siteService.GetSettingsAsync<SubscriptionSettings>()).AllowGuestSignup;
        }).Location("Content");
    }

    /// <inheritdoc/>
    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        var model = new UserRegistrationStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = await _siteService.GetSettingsAsync<SubscriptionSettings>();

        var step = new UserRegistrationStep();

        if (!settings.AllowGuestSignup || !model.ContinueAsGuest)
        {
            var form = new SubscriptionRegisterUserForm
            {
                HasSavedPassword = await HasPasswordAsync(flow.Session.SessionId),
            };

            var registrationSettings = await _siteService.GetSettingsAsync<RegistrationSettings>();

            await _registerUserDisplayManager.UpdateEditorAsync(form, context.Updater, false, UserRegistrationFormGroupId, SubscriptionConstants.StepKey.UserRegistration);

            step.IsGuest = false;
            step.User = new User
            {
                UserName = form.UserName,
                Email = form.Email,
                EmailConfirmed = !registrationSettings.UsersMustValidateEmail,
                IsEnabled = true,
            };

            if (context.Updater.ModelState.IsValid && !string.IsNullOrEmpty(form.Password))
            {
                var protectedPassword = _dataProtectionProvider
                    .CreateProtector(SubscriptionConstants.ProtectorPurposes.UserRegistrationStep)
                    .Protect(form.Password);

                await _paymentSessionCache.SetAsync(flow.Session.SessionId, UserRegistrationCheckoutHandler.PasswordPurpose, protectedPassword);
            }
        }
        else
        {
            step.IsGuest = true;
        }

        flow.Session.SavedSteps[SubscriptionConstants.StepKey.UserRegistration] = JObject.FromObject(step);

        return EditStep(flow, context);
    }

    private async Task<bool> HasPasswordAsync(string sessionId)
        => !string.IsNullOrEmpty(await _paymentSessionCache.GetAsync<string>(sessionId, UserRegistrationCheckoutHandler.PasswordPurpose));
}
