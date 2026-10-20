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
using OrchardCore.Environment.Shell;
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
        services.AddTaxDeterminationProvider<TaxJarTaxDeterminationProvider>();
        services.AddSiteDisplayDriver<TaxJarSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(TaxJarSettingsDisplayDriver.GroupId, TaxationPermissions.ManageTaxation);
        services.AddNavigationProvider<TaxJarAdminMenu>();
    }
}

/// <summary>
/// Commerce › Taxation › TaxJar: the token, the sandbox switch and the countries TaxJar works tax out for. The token is
/// never shown again once saved; saving restarts the tenant so the new settings are used at once.
/// </summary>
public sealed class TaxJarSettingsDisplayDriver : SiteDisplayDriver<TaxJarSettings>
{
    public const string GroupId = "taxjar";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IShellReleaseManager _shellReleaseManager;

    public TaxJarSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IShellReleaseManager shellReleaseManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _shellReleaseManager = shellReleaseManager;
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
            settings.ApiKey = _dataProtectionProvider.CreateProtector(TaxJarSettings.ProtectionPurpose).Protect(model.ApiKey.Trim());
        }

        settings.UseSandbox = model.UseSandbox;
        settings.Countries = [.. (model.Countries ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(code => code.Length == 2 && code.All(char.IsAsciiLetter))
            .Select(code => code.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)];

        // The provider reads the settings once per tenant; a restart has it read the new ones.
        _shellReleaseManager.RequestRelease();

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
/// Commerce › Taxation › TaxJar.
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
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Taxation"], taxation => taxation
                    .Add(S["TaxJar"], S["TaxJar"].PrefixPosition(), taxJar => taxJar
                        .AddClass("taxation-taxjar")
                        .Id("taxationTaxJar")
                        .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", TaxJarSettingsDisplayDriver.GroupId } })
                        .Permission(TaxationPermissions.ManageTaxation)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
