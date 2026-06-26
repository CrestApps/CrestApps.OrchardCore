using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Drivers;

/// <summary>
/// Site settings display driver for the Twilio Lookup provider connection. Rendered as a
/// dedicated tab within the Phone Number Verifications settings group.
/// </summary>
public sealed class TwilioPhoneNumberVerificationSettingsDisplayDriver : SiteDisplayDriver<TwilioPhoneNumberVerificationSettings>
{
    private const string ProtectorPurpose = "PhoneNumberVerifications.Twilio";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TwilioPhoneNumberVerificationSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to encrypt secrets.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TwilioPhoneNumberVerificationSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<TwilioPhoneNumberVerificationSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => PhoneNumberVerificationsConstants.SettingsGroupIds.Verifications;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, TwilioPhoneNumberVerificationSettings settings, BuildEditorContext context)
    {
        return Initialize<TwilioPhoneNumberVerificationSettingsViewModel>("TwilioPhoneNumberVerificationSettings_Edit", viewModel =>
        {
            viewModel.Endpoint = settings.Endpoint;
            viewModel.AuthenticationType = settings.AuthenticationType;
            viewModel.ApiKeySid = settings.ApiKeySid;
            viewModel.AccountSid = settings.AccountSid;
            viewModel.CountryCode = settings.CountryCode;
            viewModel.Fields = settings.Fields;
            viewModel.HasApiKeySecret = !string.IsNullOrWhiteSpace(settings.ProtectedApiKeySecret);
            viewModel.HasAuthToken = !string.IsNullOrWhiteSpace(settings.ProtectedAuthToken);
        }).Location("Content:7#Twilio")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(
            _httpContextAccessor.HttpContext?.User,
            PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, TwilioPhoneNumberVerificationSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings))
        {
            return null;
        }

        var viewModel = new TwilioPhoneNumberVerificationSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        settings.Endpoint = string.IsNullOrWhiteSpace(viewModel.Endpoint)
            ? TwilioPhoneNumberVerificationSettings.DefaultEndpoint
            : viewModel.Endpoint.Trim();
        settings.AuthenticationType = viewModel.AuthenticationType;
        settings.ApiKeySid = viewModel.ApiKeySid?.Trim();
        settings.AccountSid = viewModel.AccountSid?.Trim();
        settings.CountryCode = viewModel.CountryCode?.Trim();
        settings.Fields = viewModel.Fields?.Trim();

        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);

        if (!string.IsNullOrWhiteSpace(viewModel.ApiKeySecret))
        {
            settings.ProtectedApiKeySecret = protector.Protect(viewModel.ApiKeySecret);
        }

        if (!string.IsNullOrWhiteSpace(viewModel.AuthToken))
        {
            settings.ProtectedAuthToken = protector.Protect(viewModel.AuthToken);
        }

        ValidateSettings(settings, viewModel, context);

        return Edit(site, settings, context);
    }

    private void ValidateSettings(
        TwilioPhoneNumberVerificationSettings settings,
        TwilioPhoneNumberVerificationSettingsViewModel viewModel,
        UpdateEditorContext context)
    {
        if (settings.AuthenticationType == TwilioPhoneNumberVerificationAuthenticationType.ApiKey)
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKeySid))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.ApiKeySid), S["An API key SID is required when using API key authentication."]);
            }

            if (string.IsNullOrWhiteSpace(settings.ProtectedApiKeySecret))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.ApiKeySecret), S["An API key secret is required when using API key authentication."]);
            }
        }

        if (settings.AuthenticationType == TwilioPhoneNumberVerificationAuthenticationType.AccountCredentials)
        {
            if (string.IsNullOrWhiteSpace(settings.AccountSid))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.AccountSid), S["An Account SID is required when using account credential authentication."]);
            }

            if (string.IsNullOrWhiteSpace(settings.ProtectedAuthToken))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.AuthToken), S["An Auth Token is required when using account credential authentication."]);
            }
        }
    }
}
