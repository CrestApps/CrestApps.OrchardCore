using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Addresses.GooglePlaces;

/// <summary>
/// Settings › Address suggestions: the Google Places key. The key is never shown again once saved; a blank field keeps
/// it.
/// </summary>
public sealed class GooglePlacesSettingsDisplayDriver : SiteDisplayDriver<GooglePlacesSettings>
{
    public const string GroupId = "address-suggestions";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public GooglePlacesSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, GooglePlacesSettings settings, BuildEditorContext context)
        => Initialize<GooglePlacesSettingsViewModel>("GooglePlacesSettings_Edit", model =>
        {
            model.HasApiKey = ProtectedSettingExtensions.HasSecret(settings.ApiKey);
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AddressPermissions.ManageAddressSuggestions))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, GooglePlacesSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AddressPermissions.ManageAddressSuggestions))
        {
            return null;
        }

        var model = new GooglePlacesSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.ApiKey = _dataProtectionProvider.ApplySecret(GooglePlacesSettings.ProtectionPurpose, settings.ApiKey, model.ApiKey, model.RemoveApiKey);


        return Edit(site, settings, context);
    }
}

public class GooglePlacesSettingsViewModel
{
    public string ApiKey { get; set; }

    public bool RemoveApiKey { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool HasApiKey { get; set; }
}
