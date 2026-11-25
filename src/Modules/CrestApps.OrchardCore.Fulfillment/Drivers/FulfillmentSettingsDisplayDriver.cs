using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Fulfillment.Drivers;

/// <summary>
/// The "Send paid orders automatically" switch on the Fulfillment partners settings.
/// </summary>
internal sealed class FulfillmentSettingsDisplayDriver : SiteDisplayDriver<FulfillmentSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public FulfillmentSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<FulfillmentSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId => FulfillmentConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, FulfillmentSettings settings, BuildEditorContext context)
        => Initialize<FulfillmentSettingsViewModel>("FulfillmentSettings_Edit", model => model.SendAutomatically = settings.SendAutomatically)
            .Location(l => l.Zone("Content", "2").Card(S["Sending Orders"], "20"))
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, FulfillmentPermissions.ManageFulfillmentPartners))
            .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, FulfillmentSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, FulfillmentPermissions.ManageFulfillmentPartners))
        {
            return null;
        }

        var model = new FulfillmentSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.SendAutomatically = model.SendAutomatically;

        return Edit(site, settings, context);
    }
}
