using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
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

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class UserRegistrationSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
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

    public override IDisplayResult Edit(SubscriptionFlow flow, BuildEditorContext context)
    {
        if (!flow.CurrentStepEquals(UserRegistrationSubscriptionHandler.StepKey))
        {
            return null;
        }

        return Initialize<UserRegistrationStepViewModel>("RegisterUserFormSubscription_Edit", async model =>
        {
            var form = new SubscriptionRegisterUserForm();

            if (flow.Session.SavedSteps.TryGetPropertyValue(UserRegistrationSubscriptionHandler.StepKey, out var node))
            {
                var user = node.Deserialize<User>(_documentJsonSerializerOptions.SerializerOptions);

                form.UserName = user.UserName;
                form.Email = user.Email;
            }

            model.SignupForm = await _registerUserDisplayManager.BuildEditorAsync(form, context.Updater, false, UserRegistrationFormGroupId, UserRegistrationSubscriptionHandler.StepKey);

            model.AllowGuestSignup = await ShouldAllowGuestSignupAsync(flow);
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        if (!flow.CurrentStepEquals(UserRegistrationSubscriptionHandler.StepKey))
        {
            return null;
        }

        var model = new UserRegistrationStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!await ShouldAllowGuestSignupAsync(flow) || !model.ContinueAsGuest)
        {
            var form = new SubscriptionRegisterUserForm();

            var settings = await _siteService.GetSettingsAsync<RegistrationSettings>();

            // Validate
            await _registerUserDisplayManager.UpdateEditorAsync(form, context.Updater, false, UserRegistrationFormGroupId, UserRegistrationSubscriptionHandler.StepKey);

            var user = new User
            {
                UserName = form.UserName,
                Email = form.Email,
                EmailConfirmed = !settings.UsersMustValidateEmail,
                IsEnabled = true,
            };

            flow.Session.SavedSteps[UserRegistrationSubscriptionHandler.StepKey] = JObject.FromObject(user);

            if (context.Updater.ModelState.IsValid)
            {
                // Save the password in the cache not in the database.
                await _subscriptionPaymentSession.SetUserPasswordAsync(flow.Session.SessionId, form.Password, _dataProtectionProvider);
            }
        }

        return Edit(flow, context);
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
