using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
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
/// Site settings display driver for the AbstractAPI provider connection. Rendered as a
/// dedicated tab within the Phone Number Verifications settings group.
/// </summary>
public sealed class AbstractApiPhoneNumberVerificationSettingsDisplayDriver : SiteDisplayDriver<AbstractApiPhoneNumberVerificationSettings>
{
    private const string ProtectorPurpose = "PhoneNumberVerifications.AbstractApi";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractApiPhoneNumberVerificationSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to encrypt secrets.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AbstractApiPhoneNumberVerificationSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<AbstractApiPhoneNumberVerificationSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => PhoneNumberVerificationsConstants.SettingsGroupIds.Verifications;

    public override IDisplayResult Edit(ISite site, AbstractApiPhoneNumberVerificationSettings settings, BuildEditorContext context)
    {
        return Initialize<AbstractApiPhoneNumberVerificationSettingsViewModel>("AbstractApiPhoneNumberVerificationSettings_Edit", viewModel =>
        {
            viewModel.Endpoint = settings.Endpoint;
            viewModel.AuthenticationType = settings.AuthenticationType;
            viewModel.Username = settings.Username;
            viewModel.ClientId = settings.ClientId;
            viewModel.HasApiKey = !string.IsNullOrWhiteSpace(settings.ProtectedApiKey);
            viewModel.HasPassword = !string.IsNullOrWhiteSpace(settings.ProtectedPassword);
            viewModel.HasClientSecret = !string.IsNullOrWhiteSpace(settings.ProtectedClientSecret);
        }).Location("Content:5#AbstractAPI")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(
            _httpContextAccessor.HttpContext?.User,
            PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AbstractApiPhoneNumberVerificationSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings))
        {
            return null;
        }

        var viewModel = new AbstractApiPhoneNumberVerificationSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        settings.Endpoint = string.IsNullOrWhiteSpace(viewModel.Endpoint)
            ? "https://phonevalidation.abstractapi.com/v1/"
            : viewModel.Endpoint.Trim();
        settings.AuthenticationType = viewModel.AuthenticationType;
        settings.Username = viewModel.Username;
        settings.ClientId = viewModel.ClientId;

        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);

        if (!string.IsNullOrWhiteSpace(viewModel.ApiKey))
        {
            settings.ProtectedApiKey = protector.Protect(viewModel.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(viewModel.Password))
        {
            settings.ProtectedPassword = protector.Protect(viewModel.Password);
        }

        if (!string.IsNullOrWhiteSpace(viewModel.ClientSecret))
        {
            settings.ProtectedClientSecret = protector.Protect(viewModel.ClientSecret);
        }

        if (settings.AuthenticationType == PhoneNumberVerificationAuthenticationType.ApiKey
            && string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.ApiKey), S["An API key is required when using API key authentication."]);
        }

        return Edit(site, settings, context);
    }
}
