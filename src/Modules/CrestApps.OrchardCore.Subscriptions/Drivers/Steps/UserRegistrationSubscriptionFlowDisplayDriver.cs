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

/// <summary>
/// Displays and updates the user registration step of a subscription flow.
/// </summary>
public sealed class UserRegistrationSubscriptionFlowDisplayDriver : SubscriptionFlowDisplayDriver
{
    /// <summary>
    /// Defines the display editor group used for the subscription registration form.
    /// </summary>
    public const string UserRegistrationFormGroupId = "Subscription";

    private readonly ISiteService _siteService;
    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IDisplayManager<SubscriptionRegisterUserForm> _registerUserDisplayManager;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRegistrationSubscriptionFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read subscription and registration settings.</param>
    /// <param name="subscriptionPaymentSession">The subscription payment session used to persist passwords outside the database.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect saved passwords.</param>
    /// <param name="registerUserDisplayManager">The display manager used to build and update the registration form.</param>
    /// <param name="documentJsonSerializerOptions">The JSON serializer options used to read saved registration step data.</param>
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

    /// <summary>
    /// Gets the user registration step key handled by this display driver.
    /// </summary>
    protected override string StepKey
        => SubscriptionConstants.StepKey.UserRegistration;

    /// <summary>
    /// Builds the user registration editor and restores saved registration or guest signup values.
    /// </summary>
    /// <param name="flow">The subscription flow being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result that renders the user registration step editor.</returns>
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

    /// <summary>
    /// Updates the user registration step, saves guest selection or user details, and stores the password in the subscription session.
    /// </summary>
    /// <param name="flow">The subscription flow being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The display result that renders the updated user registration step editor.</returns>
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
