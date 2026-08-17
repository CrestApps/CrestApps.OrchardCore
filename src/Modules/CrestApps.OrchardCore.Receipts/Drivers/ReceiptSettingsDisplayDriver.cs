using CrestApps.OrchardCore.Receipts.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Receipts.Drivers;

/// <summary>
/// Display driver that renders the receipt branding settings tab on the receipt settings screen.
/// </summary>
public sealed class ReceiptSettingsDisplayDriver : SiteDisplayDriver<ReceiptSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    protected override string SettingsGroupId
        => ReceiptsConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReceiptSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public ReceiptSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, ReceiptSettings settings, BuildEditorContext context)
    {
        return Initialize<ReceiptSettingsViewModel>("ReceiptSettings_Edit", model =>
        {
            model.HeaderTitle = settings.HeaderTitle;
            model.BusinessName = settings.BusinessName;
            model.LogoUrl = settings.LogoUrl;
            model.BusinessAddress = settings.BusinessAddress;
            model.ContactEmail = settings.ContactEmail;
            model.ContactPhone = settings.ContactPhone;
            model.Website = settings.Website;
            model.FooterText = settings.FooterText;
            model.ShowTestPaymentBadge = settings.ShowTestPaymentBadge;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ReceiptsPermissions.ManageReceiptSettings))
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, ReceiptSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, ReceiptsPermissions.ManageReceiptSettings))
        {
            return null;
        }

        var model = new ReceiptSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.HeaderTitle = model.HeaderTitle?.Trim();
        settings.BusinessName = model.BusinessName?.Trim();
        settings.LogoUrl = model.LogoUrl?.Trim();
        settings.BusinessAddress = model.BusinessAddress?.Trim();
        settings.ContactEmail = model.ContactEmail?.Trim();
        settings.ContactPhone = model.ContactPhone?.Trim();
        settings.Website = model.Website?.Trim();
        settings.FooterText = model.FooterText?.Trim();
        settings.ShowTestPaymentBadge = model.ShowTestPaymentBadge;

        return Edit(site, settings, context);
    }
}
