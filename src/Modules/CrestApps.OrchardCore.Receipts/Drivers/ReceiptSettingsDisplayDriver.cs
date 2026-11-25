using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Receipts.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;
using Microsoft.Extensions.Localization;

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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReceiptSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public ReceiptSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReceiptSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, ReceiptSettings settings, BuildEditorContext context)
    {
        ShapeResult Card(string shapeType, string position, LocalizedString card, string cardPosition)
            => Initialize<ReceiptSettingsViewModel>(shapeType, model =>
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
            }).Location(l => l.Zone("Content", position).Card(card, cardPosition))
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ReceiptsPermissions.ManageReceiptSettings))
            .OnGroup(SettingsGroupId);

        return Combine(
            Card("ReceiptSettingsHeader_Edit", "1", S["Header"], "10"),
            Card("ReceiptSettingsBusiness_Edit", "1.1", S["Business"], "20"),
            Card("ReceiptSettingsFooter_Edit", "1.2", S["Footer"], "30"));
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
