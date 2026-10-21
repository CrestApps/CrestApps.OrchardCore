using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Downloads.Drivers;

public sealed class DownloadSettingsDisplayDriver : SiteDisplayDriver<DownloadSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public DownloadSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, IStringLocalizer<DownloadSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => DownloadsConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, DownloadSettings settings, BuildEditorContext context)
        => Initialize<DownloadSettingsViewModel>("DownloadSettings_Edit", model =>
        {
            model.DownloadLimit = settings.DownloadLimit;
            model.LinkLifetimeDays = settings.LinkLifetimeDays;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, DownloadsPermissions.ManageDownloads))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, DownloadSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, DownloadsPermissions.ManageDownloads))
        {
            return null;
        }

        var model = new DownloadSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.DownloadLimit is < 0 or > 10_000)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DownloadLimit), S["Enter a number of downloads from 0 to 10000."]);
        }

        if (model.LinkLifetimeDays is < 0 or > 3650)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.LinkLifetimeDays), S["Enter a number of days from 0 to 3650."]);
        }

        if (!context.Updater.ModelState.IsValid)
        {
            return Edit(site, settings, context);
        }

        settings.DownloadLimit = model.DownloadLimit;
        settings.LinkLifetimeDays = model.LinkLifetimeDays;

        return Edit(site, settings, context);
    }
}
