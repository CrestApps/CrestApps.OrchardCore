using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Commerce.StoreProfile.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.StoreProfile.Drivers;

/// <summary>
/// Settings › Commerce › Store: the store profile, which receipts, the ship-from address and the tax origin fall back to.
/// </summary>
public sealed class StoreProfileSettingsDisplayDriver : SiteDisplayDriver<StoreProfileSettings>
{
    /// <summary>
    /// The settings group of the Store page, at <c>Admin/Settings/store-profile</c>.
    /// </summary>
    public const string GroupId = "store-profile";

    private static readonly EmailAddressAttribute _emailAddress = new();

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;
    private readonly IPhoneNumberService _phoneNumberService;

    internal readonly IStringLocalizer S;

    public StoreProfileSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAddressEditorService addressEditor,
        IShapeFactory shapeFactory,
        ISiteService siteService,
        IEnumerable<IPhoneNumberService> phoneNumberServices,
        IStringLocalizer<StoreProfileSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
        _phoneNumberService = phoneNumberServices.FirstOrDefault();
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, StoreProfileSettings settings, BuildEditorContext context)
    {
        var model = new StoreProfileSettingsViewModel
        {
            Name = settings.Name,
            LogoUrl = settings.LogoUrl,
            TaxId = settings.TaxId,
            Email = settings.Email,
            Phone = settings.Phone,
            Address = await _addressEditor.BuildAsync(settings.Address, EditorOptions(settings.Address?.Country)),
        };

        return await BuildEditorAsync(model, site);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, StoreProfileSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, StoreProfilePermissions.ManageStoreProfile))
        {
            return null;
        }

        var model = new StoreProfileSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        model.Address ??= new AddressEditorViewModel();

        var email = Clean(model.Email);

        if (email is not null && !_emailAddress.IsValid(email))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Email), S["Enter a valid email address."]);
        }

        // The address is optional; one that's started has to be complete for its country, as parcels ship from it.
        var address = settings.Address;

        if (!model.Address.HasAnyValue())
        {
            address = null;
        }
        else
        {
            model.Address.Name = null;

            var result = await _addressEditor.ValidateAsync(model.Address, context.Updater.ModelState, $"{Prefix}.{nameof(model.Address)}", EditorOptions(model.Address.Country));

            if (result.IsValid)
            {
                address = result.Address;
            }
        }

        if (!context.Updater.ModelState.IsValid)
        {
            return await BuildEditorAsync(model, site);
        }

        settings.Name = Clean(model.Name);
        settings.LogoUrl = Clean(model.LogoUrl);
        settings.TaxId = Clean(model.TaxId);
        settings.Email = email;
        settings.Phone = PhoneNumberNormalization.ToE164OrTrimmed(model.Phone, _phoneNumberService, address?.Country);
        settings.Address = address;

        return await EditAsync(site, settings, context);
    }

    private async Task<IDisplayResult> BuildEditorAsync(StoreProfileSettingsViewModel model, ISite site)
    {
        model.Address ??= new AddressEditorViewModel();

        await _addressEditor.PopulateAsync(model.Address, EditorOptions(model.Address.Country));

        var addressEditor = await _shapeFactory.CreateAddressEditorAsync(model.Address, $"{Prefix}.{nameof(model.Address)}");
        var siteName = (site ?? await _siteService.GetSiteSettingsAsync())?.SiteName;

        return Initialize<StoreProfileSettingsViewModel>("StoreProfileSettings_Edit", view =>
        {
            view.Name = model.Name;
            view.LogoUrl = model.LogoUrl;
            view.TaxId = model.TaxId;
            view.Email = model.Email;
            view.Phone = model.Phone;
            view.Address = model.Address;
            view.AddressEditor = addressEditor;
            view.SiteName = siteName;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, StoreProfilePermissions.ManageStoreProfile))
        .OnGroup(SettingsGroupId);
    }

    private static AddressEditorOptions EditorOptions(string country)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = string.IsNullOrWhiteSpace(country) ? null : country,
        };

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
