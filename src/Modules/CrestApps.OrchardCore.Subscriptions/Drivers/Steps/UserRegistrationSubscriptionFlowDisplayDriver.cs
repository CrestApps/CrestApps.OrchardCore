using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata;
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
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    public UserRegistrationSubscriptionFlowDisplayDriver(
        ISiteService siteService,
        SubscriptionPaymentSession subscriptionPaymentSession,
        IDataProtectionProvider dataProtectionProvider,
        IDisplayManager<SubscriptionRegisterUserForm> registerUserDisplayManager,
        IContentDefinitionManager contentDefinitionManager,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _siteService = siteService;
        _subscriptionPaymentSession = subscriptionPaymentSession;
        _dataProtectionProvider = dataProtectionProvider;
        _registerUserDisplayManager = registerUserDisplayManager;
        _contentDefinitionManager = contentDefinitionManager;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    protected override string StepKey
        => UserRegistrationFormGroupId;

    protected override IDisplayResult EditStep(SubscriptionFlow flow, BuildEditorContext context)
    {
        return Initialize<UserRegistrationStepViewModel>("UserRegistrationStep_Edit", async model =>
        {
            var form = new SubscriptionRegisterUserForm();

            if (flow.Session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.UserRegistration, out var node))
            {
                var stepInfo = node.Deserialize<UserRegistrationStep>(_documentJsonSerializerOptions.SerializerOptions);

                form.UserName = stepInfo.User.UserName;
                form.Email = stepInfo.User.Email;
            }

            model.SignupForm = await _registerUserDisplayManager.BuildEditorAsync(form, context.Updater, false, UserRegistrationFormGroupId, SubscriptionConstants.StepKey.UserRegistration);

            model.AllowGuestSignup = await ShouldAllowGuestSignupAsync(flow);
        }).Location("Content");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        var model = new UserRegistrationStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!await ShouldAllowGuestSignupAsync(flow) || !model.ContinueAsGuest)
        {
            var form = new SubscriptionRegisterUserForm();

            var settings = await _siteService.GetSettingsAsync<RegistrationSettings>();

            // Validate
            await _registerUserDisplayManager.UpdateEditorAsync(form, context.Updater, false, UserRegistrationFormGroupId, SubscriptionConstants.StepKey.UserRegistration);

            var user = new User
            {
                UserName = form.UserName,
                Email = form.Email,
                EmailConfirmed = !settings.UsersMustValidateEmail,
                IsEnabled = true,
            };

            flow.Session.SavedSteps[SubscriptionConstants.StepKey.UserRegistration] = JObject.FromObject(new UserRegistrationStep
            {
                User = user,
            });

            if (context.Updater.ModelState.IsValid)
            {
                // Save the password in the cache not in the database.
                await _subscriptionPaymentSession.SetUserPasswordAsync(flow.Session.SessionId, form.Password, _dataProtectionProvider);
            }
        }

        return EditStep(flow, context);
    }

    private bool? _allowGuests;

    private async Task<bool> ShouldAllowGuestSignupAsync(SubscriptionFlow flow)
    {
        if (_allowGuests == null)
        {
            var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(flow.ContentItem.ContentType);

            var settings = typeDefinition?.Parts?.FirstOrDefault(x => x.Name == nameof(SubscriptionPart))?.GetSettings<SubscriptionPartSettings>();

            _allowGuests = settings?.AllowGuestSignup ?? false;
        }

        return _allowGuests ?? false;
    }
}
