using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.ViewModels;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Drivers;

/// <summary>
/// Display driver that renders the Pay Later settings tab.
/// </summary>
public sealed class PayLaterSettingsDisplayDriver : SiteDisplayDriver<PayLaterSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    protected override string SettingsGroupId
        => PayLaterConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayLaterSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public PayLaterSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, PayLaterSettings settings, BuildEditorContext context)
    {
        return Initialize<PayLaterSettingsViewModel>("PayLaterSettings_Edit", model =>
        {
            model.NetTermDays = settings.NetTermDays;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TransactionsPermissions.ManageTransactionSettings))
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, PayLaterSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, TransactionsPermissions.ManageTransactionSettings))
        {
            return null;
        }

        var model = new PayLaterSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.NetTermDays = Math.Max(0, model.NetTermDays);

        return Edit(site, settings, context);
    }
}
