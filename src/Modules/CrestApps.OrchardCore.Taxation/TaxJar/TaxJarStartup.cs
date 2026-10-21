using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Taxation.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Options;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Taxation.TaxJar;

[Feature(TaxationConstants.Feature.TaxJar)]
public sealed class TaxJarStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(TaxJarTaxDeterminationProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddTransient<IConfigureOptions<TaxJarOptions>, TaxJarOptionsConfiguration>();
        services.AddSignalOptionsChangeTokenSource<TaxJarOptions>();
        services.AddTaxDeterminationProvider<TaxJarTaxDeterminationProvider>();
        services.AddSiteDisplayDriver<TaxJarSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(TaxJarSettingsDisplayDriver.GroupId, TaxationPermissions.ManageTaxation);
        services.AddNavigationProvider<TaxJarAdminMenu>();
    }
}

/// <summary>
/// Commerce › Taxation › TaxJar: the token, the sandbox switch and the countries TaxJar works tax out for. The token is
/// never shown again once saved; saving refreshes the <see cref="TaxJarOptions"/> so the new settings are used at once.
/// </summary>
public sealed class TaxJarSettingsDisplayDriver : SiteDisplayDriver<TaxJarSettings>
{
    public const string GroupId = "taxjar";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;

    public TaxJarSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IOptionsUpdateNotifier optionsUpdateNotifier)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _optionsUpdateNotifier = optionsUpdateNotifier;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, TaxJarSettings settings, BuildEditorContext context)
        => Initialize<TaxJarSettingsViewModel>("TaxJarSettings_Edit", model =>
        {
            model.HasApiKey = !string.IsNullOrEmpty(settings.ApiKey);
            model.UseSandbox = settings.UseSandbox;
            model.Countries = string.Join(", ", settings.Countries ?? []);
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TaxationPermissions.ManageTaxation))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, TaxJarSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TaxationPermissions.ManageTaxation))
        {
            return null;
        }

        var model = new TaxJarSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.RemoveApiKey)
        {
            settings.ApiKey = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.ApiKey))
        {
            settings.ApiKey = _dataProtectionProvider.ProtectSetting(TaxJarSettings.ProtectionPurpose, model.ApiKey.Trim());
        }

        settings.UseSandbox = model.UseSandbox;
        settings.Countries = [.. (model.Countries ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(CountryCodeUtility.IsCountryCode)
            .Select(CountryCodeUtility.Normalize)
            .Distinct(StringComparer.Ordinal)];

        // The provider reads the options when it works tax out; they are rebuilt once the settings are saved.
        _optionsUpdateNotifier.RequestUpdate<TaxJarOptions>();

        return Edit(site, settings, context);
    }
}

public class TaxJarSettingsViewModel
{
    public string ApiKey { get; set; }

    public bool RemoveApiKey { get; set; }

    public bool UseSandbox { get; set; }

    public string Countries { get; set; }

    [BindNever]
    public bool HasApiKey { get; set; }
}

/// <summary>
/// Settings › Commerce › TaxJar.
/// </summary>
public sealed class TaxJarAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public TaxJarAdminMenu(IStringLocalizer<TaxJarAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["TaxJar"], S["TaxJar"].PrefixPosition(), taxJar => taxJar
                        .Id("taxationTaxJar")
                        .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", TaxJarSettingsDisplayDriver.GroupId } })
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
