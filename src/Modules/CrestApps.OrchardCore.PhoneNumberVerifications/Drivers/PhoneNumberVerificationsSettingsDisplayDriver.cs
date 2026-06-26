using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using CrestApps.OrchardCore.PhoneNumberVerifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Drivers;

/// <summary>
/// Site settings display driver for the core Phone Number Verifications settings,
/// including just-in-time verification, the revalidation interval, and provider selection.
/// </summary>
public sealed class PhoneNumberVerificationsSettingsDisplayDriver : SiteDisplayDriver<PhoneNumberVerificationsSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPhoneNumberVerificationManager _verificationManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationsSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="verificationManager">The verification manager used to discover providers.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PhoneNumberVerificationsSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IPhoneNumberVerificationManager verificationManager,
        IStringLocalizer<PhoneNumberVerificationsSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _verificationManager = verificationManager;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => PhoneNumberVerificationsConstants.SettingsGroupIds.Verifications;

    public override IDisplayResult Edit(ISite site, PhoneNumberVerificationsSettings settings, BuildEditorContext context)
    {
        return Initialize<PhoneNumberVerificationsSettingsViewModel>("PhoneNumberVerificationsSettings_Edit", viewModel =>
        {
            viewModel.EnableJustInTimeVerification = settings.EnableJustInTimeVerification;
            viewModel.RevalidationIntervalDays = settings.RevalidationIntervalDays;
            viewModel.SelectedProvider = settings.SelectedProvider;
            viewModel.Providers = _verificationManager.GetProviders()
                .Select(provider => new SelectListItem(provider.DisplayName ?? provider.Key, provider.Key))
                .OrderBy(item => item.Text)
                .ToList();
        }).Location("Content:1#General")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(
            _httpContextAccessor.HttpContext?.User,
            PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, PhoneNumberVerificationsSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings))
        {
            return null;
        }

        var viewModel = new PhoneNumberVerificationsSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        if (viewModel.RevalidationIntervalDays <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.RevalidationIntervalDays), S["The revalidation interval must be greater than zero."]);
        }

        settings.EnableJustInTimeVerification = viewModel.EnableJustInTimeVerification;
        settings.RevalidationIntervalDays = viewModel.RevalidationIntervalDays > 0
            ? viewModel.RevalidationIntervalDays
            : PhoneNumberVerificationsSettings.DefaultRevalidationIntervalDays;
        settings.SelectedProvider = viewModel.SelectedProvider;

        return Edit(site, settings, context);
    }
}
