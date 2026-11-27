using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Learning.Packages.Drivers;

/// <summary>
/// Settings › Learning Packages: the package content host.
/// </summary>
public sealed class LearningPackageContentHostSettingsDisplayDriver : SiteDisplayDriver<LearningPackageContentHostSettings>
{
    /// <summary>
    /// The settings group of the page.
    /// </summary>
    public const string GroupId = "learning-packages";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningPackageContentHostSettingsDisplayDriver"/> class.
    /// </summary>
    public LearningPackageContentHostSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<LearningPackageContentHostSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override string SettingsGroupId
        => GroupId;

    /// <inheritdoc />
    public override IDisplayResult Edit(ISite site, LearningPackageContentHostSettings settings, BuildEditorContext context)
        => Initialize<LearningPackageContentHostSettingsViewModel>("LearningPackageContentHostSettings_Edit", model =>
        {
            model.BaseUrl = settings.BaseUrl;
        }).Location(l => l.Zone("Content", "1").Card(S["Package Content Host"], "10"))
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, LearningPackagesPermissions.ManageLearningPackageSettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ISite site, LearningPackageContentHostSettings settings, UpdateEditorContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (!await _authorizationService.AuthorizeAsync(httpContext?.User, LearningPackagesPermissions.ManageLearningPackageSettings))
        {
            return null;
        }

        var model = new LearningPackageContentHostSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.BaseUrl))
        {
            settings.BaseUrl = null;
        }
        else if (!PackageContentHost.TryNormalizeOrigin(model.BaseUrl, out var origin))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.BaseUrl), S["Enter an address with http:// or https://, a host name and an optional port, and nothing after it."]);
        }
        else if (httpContext is not null && PackageContentHost.IsContentHost(origin, httpContext.Request.Host))
        {
            // On the site's own host, the content host would refuse every page of the site, this one included.
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.BaseUrl), S["The package content host must be another host than the one you use for the site."]);
        }
        else
        {
            settings.BaseUrl = origin;
        }

        return Edit(site, settings, context);
    }
}
