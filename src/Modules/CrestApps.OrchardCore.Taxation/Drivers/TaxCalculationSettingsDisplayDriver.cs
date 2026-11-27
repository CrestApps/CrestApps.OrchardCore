using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// How the site rounds tax, under Settings > Commerce > Tax Calculation. The checkout and invoices both read it.
/// </summary>
public sealed class TaxCalculationSettingsDisplayDriver : SiteDisplayDriver<TaxCalculationSettings>
{
    public const string GroupId = "tax-calculation";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public TaxCalculationSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<TaxCalculationSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, TaxCalculationSettings settings, BuildEditorContext context)
        => Initialize<TaxCalculationSettingsViewModel>("TaxCalculationSettings_Edit", model =>
        {
            model.RoundingLevel = settings.RoundingLevel;
        }).Location(l => l.Zone("Content", "1").Card(S["Rounding"], "10"))
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TaxationPermissions.ManageTaxation))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, TaxCalculationSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TaxationPermissions.ManageTaxation))
        {
            return null;
        }

        var model = new TaxCalculationSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (Enum.IsDefined(model.RoundingLevel))
        {
            settings.RoundingLevel = model.RoundingLevel;
        }

        return Edit(site, settings, context);
    }
}
