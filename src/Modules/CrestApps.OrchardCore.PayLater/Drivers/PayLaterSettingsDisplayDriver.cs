using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.ViewModels;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceProvider _serviceProvider;

    protected override string SettingsGroupId
        => PayLaterConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayLaterSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="serviceProvider">The service provider the payment terms are read from when the Payment Terms feature is on.</param>
    public PayLaterSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, PayLaterSettings settings, BuildEditorContext context)
    {
        return Initialize<PayLaterSettingsViewModel>("PayLaterSettings_Edit", async model =>
        {
            model.Enabled = settings.Enabled;
            model.NetTermDays = settings.NetTermDays;
            model.PaymentTermId = settings.PaymentTermId;

            // The terms are the Payment Terms feature's; without it the number of days is the only choice.
            if (_serviceProvider.GetService<IPaymentTermManager>() is { } terms)
            {
                model.PaymentTerms = [.. (await terms.GetEnabledAsync()).Select(term => new SelectListItem(term.Name, term.ItemId))];
            }
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

        settings.Enabled = model.Enabled;
        settings.NetTermDays = Math.Max(0, model.NetTermDays);

        // Kept only while the Payment Terms feature is on; with it off the field isn't shown, so the term stays as it was.
        if (_serviceProvider.GetService<IPaymentTermManager>() is not null)
        {
            settings.PaymentTermId = string.IsNullOrWhiteSpace(model.PaymentTermId) ? null : model.PaymentTermId;
        }

        return Edit(site, settings, context);
    }
}
