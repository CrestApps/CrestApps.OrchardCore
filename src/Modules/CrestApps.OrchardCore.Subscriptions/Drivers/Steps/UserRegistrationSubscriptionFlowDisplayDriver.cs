using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core;
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

namespace CrestApps.OrchardCore.Subscriptions.Drivers.Steps;

public sealed class UserRegistrationSubscriptionFlowDisplayDriver : SubscriptionFlowDisplayDriver
{
    public const string UserRegistrationFormGroupId = "Subscription";

    private readonly ISiteService _siteService;
    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IDisplayManager<SubscriptionRegisterUserForm> _registerUserDisplayManager;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    public UserRegistrationSubscriptionFlowDisplayDriver(
        ISiteService siteService,
        SubscriptionPaymentSession subscriptionPaymentSession,
        IDataProtectionProvider dataProtectionProvider,
        IDisplayManager<SubscriptionRegisterUserForm> registerUserDisplayManager,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _siteService = siteService;
        _subscriptionPaymentSession = subscriptionPaymentSession;
        _dataProtectionProvider = dataProtectionProvider;
        _registerUserDisplayManager = registerUserDisplayManager;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    protected override string StepKey
        => SubscriptionConstants.StepKey.UserRegistration;

    protected override IDisplayResult EditStep(SubscriptionFlow flow, BuildEditorContext context)
    {
        return Initialize<UserRegistrationStepViewModel>("UserRegistrationStep_Edit", async model =>
        {
            var form = new SubscriptionRegisterUserForm();

            if (flow.Session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.UserRegistration, out var node))
            {
                var stepInfo = node.Deserialize<UserRegistrationStep>(_documentJsonSerializerOptions.SerializerOptions);

                if (stepInfo.IsGuest)
                {
                    model.ContinueAsGuest = true;
                }
                else
                {
                    form.UserName = stepInfo.User.UserName;
                    form.Email = stepInfo.User.Email;
                    form.HasSavedPassword = await _subscriptionPaymentSession.UserPasswordExistsAsync(flow.Session.SessionId);
                }
            }

            model.SignupForm = await _registerUserDisplayManager.BuildEditorAsync(form, context.Updater, false, UserRegistrationFormGroupId, SubscriptionConstants.StepKey.UserRegistration);
            model.AllowGuestSignup = (await _siteService.GetSettingsAsync<SubscriptionSettings>()).AllowGuestSignup;
        }).Location("Content");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        var model = new UserRegistrationStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        var subscriptionSessions = await _siteService.GetSettingsAsync<SubscriptionSettings>();

        var stepInfo = new UserRegistrationStep();

        if (!subscriptionSessions.AllowGuestSignup || !model.ContinueAsGuest)
        {
            var form = new SubscriptionRegisterUserForm
            {
                HasSavedPassword = await _subscriptionPaymentSession.UserPasswordExistsAsync(flow.Session.SessionId),
            };

            var registrationSettings = await _siteService.GetSettingsAsync<RegistrationSettings>();

            await _registerUserDisplayManager.UpdateEditorAsync(form, context.Updater, false, UserRegistrationFormGroupId, SubscriptionConstants.StepKey.UserRegistration);

            var user = new User
            {
                UserName = form.UserName,
                Email = form.Email,
                EmailConfirmed = !registrationSettings.UsersMustValidateEmail,
                IsEnabled = true,
            };

            stepInfo.User = user;
            stepInfo.IsGuest = false;

            if (context.Updater.ModelState.IsValid && !string.IsNullOrEmpty(form.Password))
            {
                // Save the password in the cache not in the database.
                await _subscriptionPaymentSession.SetUserPasswordAsync(flow.Session.SessionId, form.Password, _dataProtectionProvider);
            }
        }
        else
        {
            stepInfo.IsGuest = true;
        }

        flow.Session.SavedSteps[SubscriptionConstants.StepKey.UserRegistration] = JObject.FromObject(stepInfo);

        return EditStep(flow, context);
    }
}
