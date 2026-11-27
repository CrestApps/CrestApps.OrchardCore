using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Fulfillment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Fulfillment.Drivers;

/// <summary>
/// Opens the Fulfillment partners settings page with the list of partners whose features are on. Each provider
/// module adds its own connection section below it.
/// </summary>
public sealed class FulfillmentPartnersDisplayDriver : DisplayDriver<ISite>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IFulfillmentProvider> _providers;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentPartnersDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="providers">The fulfillment partners whose features are on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FulfillmentPartnersDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<IFulfillmentProvider> providers,
        IStringLocalizer<FulfillmentPartnersDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _providers = providers;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, BuildEditorContext context)
        => Initialize<FulfillmentPartnersViewModel>("FulfillmentPartners_Edit", async model =>
        {
            foreach (var provider in _providers.OrderBy(provider => provider.DisplayName.Value, StringComparer.CurrentCultureIgnoreCase))
            {
                model.Partners.Add(new FulfillmentPartnerEntry
                {
                    DisplayName = provider.DisplayName.Value,
                    IsConnected = await provider.IsConnectedAsync(),
                });
            }
        })
            .Location(l => l.Zone("Content", "1").Card(S["Partners"], "10"))
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, FulfillmentPermissions.ManageFulfillmentPartners))
            .OnGroup(FulfillmentConstants.SettingsGroupId);
}
