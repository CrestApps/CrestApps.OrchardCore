using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
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
    private readonly INotifier _notifier;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractApiPhoneNumberVerificationSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to encrypt secrets.</param>
    /// <param name="notifier">The notifier used to surface admin messages.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AbstractApiPhoneNumberVerificationSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        INotifier notifier,
        IStringLocalizer<AbstractApiPhoneNumberVerificationSettingsDisplayDriver> stringLocalizer,
        IHtmlLocalizer<AbstractApiPhoneNumberVerificationSettingsDisplayDriver> htmlLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _notifier = notifier;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    protected override string SettingsGroupId
        => PhoneNumberVerificationsConstants.SettingsGroupIds.Verifications;

    public override IDisplayResult Edit(ISite site, AbstractApiPhoneNumberVerificationSettings settings, BuildEditorContext context)
    {
        return Initialize<AbstractApiPhoneNumberVerificationSettingsViewModel>("AbstractApiPhoneNumberVerificationSettings_Edit", viewModel =>
        {
            viewModel.IsEnabled = settings.IsEnabled;
            viewModel.AuthenticationType = PhoneNumberVerificationAuthenticationType.ApiKey;
            viewModel.HasApiKey = !string.IsNullOrWhiteSpace(settings.ProtectedApiKey);
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

        settings.AuthenticationType = PhoneNumberVerificationAuthenticationType.ApiKey;
        settings.Username = null;
        settings.ProtectedPassword = null;

        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);

        if (!string.IsNullOrWhiteSpace(viewModel.ApiKey))
        {
            settings.ProtectedApiKey = protector.Protect(viewModel.ApiKey.Trim());
        }

        if (viewModel.IsEnabled)
        {
            settings.IsEnabled = true;

            if (string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.ApiKey), S["An API key is required."]);
            }
        }
        else
        {
            await DisableProviderAsync(site, settings);
        }

        return Edit(site, settings, context);
    }

    private async Task DisableProviderAsync(ISite site, AbstractApiPhoneNumberVerificationSettings settings)
    {
        if (settings.IsEnabled)
        {
            var mainSettings = site.GetOrCreate<PhoneNumberVerificationsSettings>();

            if (string.Equals(mainSettings.SelectedProvider, PhoneNumberVerificationsConstants.Providers.AbstractApi, StringComparison.OrdinalIgnoreCase))
            {
                mainSettings.SelectedProvider = null;
                site.Put(mainSettings);

                await _notifier.WarningAsync(H["The AbstractAPI provider was the default provider. The default provider has been cleared until you select a new one."]);
            }
        }

        settings.IsEnabled = false;
    }
}
