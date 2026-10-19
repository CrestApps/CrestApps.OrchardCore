using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// Settings > Commerce > Rewards: how customers earn loyalty points and what they're worth.
/// </summary>
public sealed class LoyaltySettingsDisplayDriver : SiteDisplayDriver<LoyaltySettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public LoyaltySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<LoyaltySettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => CustomersConstants.LoyaltySettingsGroupId;

    public override IDisplayResult Edit(ISite site, LoyaltySettings settings, BuildEditorContext context)
        => Initialize<LoyaltySettingsViewModel>("LoyaltySettings_Edit", model =>
        {
            model.ProgramName = settings.ProgramName;
            model.PointsPerUnit = settings.PointsPerUnit;
            model.PointValue = settings.PointValue;
            model.MinimumPointsToRedeem = settings.MinimumPointsToRedeem;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CustomerPermissions.ManageCustomers))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, LoyaltySettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CustomerPermissions.ManageCustomers))
        {
            return null;
        }

        var model = new LoyaltySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.PointsPerUnit < 0m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PointsPerUnit), S["Enter 0 or more points."]);
        }

        if (model.PointValue <= 0m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PointValue), S["Enter what a point is worth, more than zero."]);
        }

        if (model.MinimumPointsToRedeem < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MinimumPointsToRedeem), S["Enter 1 or more points."]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            settings.ProgramName = string.IsNullOrWhiteSpace(model.ProgramName) ? null : model.ProgramName.Trim();
            settings.PointsPerUnit = model.PointsPerUnit;
            settings.PointValue = model.PointValue;
            settings.MinimumPointsToRedeem = model.MinimumPointsToRedeem;
        }

        return Edit(site, settings, context);
    }
}
