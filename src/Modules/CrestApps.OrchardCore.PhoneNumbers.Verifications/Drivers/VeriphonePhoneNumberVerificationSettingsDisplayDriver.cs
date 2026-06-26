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
/// Site settings display driver for the Veriphone provider connection. Rendered as a
/// dedicated tab within the Phone Number Verifications settings group.
/// </summary>
public sealed class VeriphonePhoneNumberVerificationSettingsDisplayDriver : SiteDisplayDriver<VeriphonePhoneNumberVerificationSettings>
{
    private const string ProtectorPurpose = "PhoneNumberVerifications.Veriphone";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="VeriphonePhoneNumberVerificationSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to encrypt secrets.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VeriphonePhoneNumberVerificationSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<VeriphonePhoneNumberVerificationSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => PhoneNumberVerificationsConstants.SettingsGroupIds.Verifications;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, VeriphonePhoneNumberVerificationSettings settings, BuildEditorContext context)
    {
        return Initialize<VeriphonePhoneNumberVerificationSettingsViewModel>("VeriphonePhoneNumberVerificationSettings_Edit", viewModel =>
        {
            viewModel.Endpoint = settings.Endpoint;
            viewModel.HasApiKey = !string.IsNullOrWhiteSpace(settings.ProtectedApiKey);
        }).Location("Content:6#Veriphone")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(
            _httpContextAccessor.HttpContext?.User,
            PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, VeriphonePhoneNumberVerificationSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings))
        {
            return null;
        }

        var viewModel = new VeriphonePhoneNumberVerificationSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        settings.Endpoint = string.IsNullOrWhiteSpace(viewModel.Endpoint)
            ? VeriphonePhoneNumberVerificationSettings.DefaultEndpoint
            : viewModel.Endpoint.Trim();

        if (!string.IsNullOrWhiteSpace(viewModel.ApiKey))
        {
            var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
            settings.ProtectedApiKey = protector.Protect(viewModel.ApiKey);
        }
        else if (string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.ApiKey), S["An API key is required to call the Veriphone API."]);
        }

        return Edit(site, settings, context);
    }
}
