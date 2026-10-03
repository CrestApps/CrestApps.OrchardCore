using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Edits the default phone and SMS numbers on the Contact Center settings screen: the numbers used for an agent who has
/// none of their own.
/// </summary>
public sealed class ContactCenterDefaultAddressSettingsDisplayDriver : SiteDisplayDriver<ContactCenterDefaultAddressSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IOmnichannelChannelEndpointManager _addressManager;

    internal readonly IStringLocalizer S;

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => ContactCenterConstants.Settings.GroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDefaultAddressSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="addressManager">The address list the numbers are picked from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterDefaultAddressSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOmnichannelChannelEndpointManager addressManager,
        IStringLocalizer<ContactCenterDefaultAddressSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _addressManager = addressManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, ContactCenterDefaultAddressSettings settings, BuildEditorContext context)
    {
        return Initialize<ContactCenterDefaultAddressSettingsViewModel>("ContactCenterDefaultAddressSettings_Edit", async model =>
        {
            var addresses = await _addressManager.GetAllAsync();

            model.DefaultPhoneAddressId = settings.DefaultPhoneAddressId;
            model.DefaultSmsAddressId = settings.DefaultSmsAddressId;
            model.PhoneAddressOptions = BuildOptions(addresses, OmnichannelConstants.Channels.Phone, settings.DefaultPhoneAddressId);
            model.SmsAddressOptions = BuildOptions(addresses, OmnichannelConstants.Channels.Sms, settings.DefaultSmsAddressId);
        })
        .Location("Content:4#Default numbers")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ContactCenterPermissions.ManageContactCenter));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, ContactCenterDefaultAddressSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ContactCenterPermissions.ManageContactCenter))
        {
            return null;
        }

        var model = new ContactCenterDefaultAddressSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var addresses = await _addressManager.GetAllAsync();

        settings.DefaultPhoneAddressId = Validate(addresses, model.DefaultPhoneAddressId, OmnichannelConstants.Channels.Phone, nameof(model.DefaultPhoneAddressId), context);
        settings.DefaultSmsAddressId = Validate(addresses, model.DefaultSmsAddressId, OmnichannelConstants.Channels.Sms, nameof(model.DefaultSmsAddressId), context);

        return Edit(site, settings, context);
    }

    private string Validate(IEnumerable<OmnichannelChannelEndpoint> addresses, string addressId, string channel, string member, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(addressId))
        {
            return null;
        }

        var address = addresses.FirstOrDefault(candidate => candidate.IsKnownAs(addressId.Trim()));

        if (address is null || !address.HasCapability(channel))
        {
            context.Updater.ModelState.AddModelError(Prefix, member, S["Pick a number used for {0}.", channel == OmnichannelConstants.Channels.Phone ? S["voice calls"] : S["text messages"]]);

            return addressId.Trim();
        }

        return address.ItemId;
    }

    private static List<SelectListItem> BuildOptions(IEnumerable<OmnichannelChannelEndpoint> addresses, string channel, string selectedId)
        => addresses
            .Where(address => address.HasCapability(channel) && !string.IsNullOrWhiteSpace(address.Value))
            .OrderBy(address => address.DisplayText ?? address.Value, StringComparer.CurrentCultureIgnoreCase)
            .Select(address => new SelectListItem(
                string.IsNullOrWhiteSpace(address.DisplayText) || address.DisplayText == address.Value ? address.Value : $"{address.DisplayText} ({address.Value})",
                address.ItemId,
                !string.IsNullOrEmpty(selectedId) && address.IsKnownAs(selectedId)))
            .ToList();
}
