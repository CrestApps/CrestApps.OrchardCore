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
            Website = settings.Website,
            TermsOfService = settings.Policies?.TermsOfService,
            PrivacyPolicy = settings.Policies?.PrivacyPolicy,
            ReturnPolicy = settings.Policies?.ReturnPolicy,
            ShippingPolicy = settings.Policies?.ShippingPolicy,
            Faq = settings.Policies?.Faq,
            Contact = settings.Policies?.Contact,
            About = settings.Policies?.About,
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

        foreach (var (field, value) in new[]
        {
            (nameof(model.Website), model.Website),
            (nameof(model.TermsOfService), model.TermsOfService),
            (nameof(model.PrivacyPolicy), model.PrivacyPolicy),
            (nameof(model.ReturnPolicy), model.ReturnPolicy),
            (nameof(model.ShippingPolicy), model.ShippingPolicy),
            (nameof(model.Faq), model.Faq),
            (nameof(model.Contact), model.Contact),
            (nameof(model.About), model.About),
        })
        {
            if (Clean(value) is { } link && !IsLink(link))
            {
                context.Updater.ModelState.AddModelError(Prefix, field, S["Enter a full address that starts with https://, or a path on this site that starts with /."]);
            }
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
        settings.Website = Clean(model.Website);
        settings.Policies = new StorePolicyLinks
        {
            TermsOfService = Clean(model.TermsOfService),
            PrivacyPolicy = Clean(model.PrivacyPolicy),
            ReturnPolicy = Clean(model.ReturnPolicy),
            ShippingPolicy = Clean(model.ShippingPolicy),
            Faq = Clean(model.Faq),
            Contact = Clean(model.Contact),
            About = Clean(model.About),
        };

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
            view.Website = model.Website;
            view.TermsOfService = model.TermsOfService;
            view.PrivacyPolicy = model.PrivacyPolicy;
            view.ReturnPolicy = model.ReturnPolicy;
            view.ShippingPolicy = model.ShippingPolicy;
            view.Faq = model.Faq;
            view.Contact = model.Contact;
            view.About = model.About;
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

    // A policy is a page on the site or an absolute web address: buyers and agents open it as is.
    private static bool IsLink(string value)
        => value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) ||
            Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
