using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Settings › Commerce › Returns.
/// </summary>
public sealed class ReturnSettingsDisplayDriver : SiteDisplayDriver<ReturnSettings>
{
    private const int MaxWindowDays = 365;
    private const int MaxReasons = 20;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public ReturnSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReturnSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => OrderConstants.ReturnsSettingsGroupId;

    public override IDisplayResult Edit(ISite site, ReturnSettings settings, BuildEditorContext context)
        => Initialize<ReturnSettingsViewModel>("ReturnSettings_Edit", model =>
        {
            model.WindowDays = settings.WindowDays;
            model.Reasons = string.Join(Environment.NewLine, settings.Reasons ?? []);
            model.AllowExchanges = settings.AllowExchanges;
            model.Instructions = settings.Instructions;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, OrderPermissions.ManageOrders))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ReturnSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, OrderPermissions.ManageOrders))
        {
            return null;
        }

        var model = new ReturnSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var reasons = (model.Reasons ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxReasons)
            .ToList();

        if (model.WindowDays is < 1 or > MaxWindowDays)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.WindowDays), S["Enter a number of days from 1 to {0}.", MaxWindowDays]);
        }

        if (reasons.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Reasons), S["Enter at least one reason buyers can choose."]);
        }

        settings.WindowDays = Math.Clamp(model.WindowDays, 1, MaxWindowDays);
        settings.Reasons = reasons.Count == 0 ? settings.Reasons : reasons;
        settings.AllowExchanges = model.AllowExchanges;
        settings.Instructions = string.IsNullOrWhiteSpace(model.Instructions) ? null : model.Instructions.Trim();

        return Edit(site, settings, context);
    }
}
