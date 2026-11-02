using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.Services;
using CrestApps.OrchardCore.Seo.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.Drivers;

/// <summary>
/// Settings › Search › Search Engine Optimization › Structured Data: the site's publisher and web site, and whether
/// pages get a JSON-LD script.
/// </summary>
internal sealed class StructuredDataSettingsDisplayDriver : SiteDisplayDriver<StructuredDataSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDataSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StructuredDataSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<StructuredDataSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => StructuredDataConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, StructuredDataSettings settings, BuildEditorContext context)
        => Initialize<StructuredDataSettingsViewModel>("StructuredDataSettings_Edit", model =>
        {
            model.RenderOnPages = settings.RenderOnPages;
            model.PublisherType = settings.PublisherType;
            model.PublisherName = settings.PublisherName;
            model.PublisherLogoUrl = settings.PublisherLogoUrl;
            model.SameAs = string.Join(Environment.NewLine, settings.SameAs ?? []);
            model.IncludeWebSite = settings.IncludeWebSite;
            model.IncludeSearchAction = settings.IncludeSearchAction;
            model.PublisherTypes =
            [
                new SelectListItem(S["Organization"], StructuredDataPublisherTypes.Organization),
                new SelectListItem(S["Person"], StructuredDataPublisherTypes.Person),
                new SelectListItem(S["None"], string.Empty),
            ];
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, StructuredDataPermissionsProvider.ManageStructuredDataSettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, StructuredDataSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, StructuredDataPermissionsProvider.ManageStructuredDataSettings))
        {
            return null;
        }

        var model = new StructuredDataSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.RenderOnPages = model.RenderOnPages;
        settings.PublisherType = model.PublisherType switch
        {
            StructuredDataPublisherTypes.Organization or StructuredDataPublisherTypes.Person => model.PublisherType,
            _ => string.Empty,
        };
        settings.PublisherName = model.PublisherName?.Trim();
        settings.PublisherLogoUrl = model.PublisherLogoUrl?.Trim();
        settings.SameAs = (model.SameAs ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .ToArray();
        settings.IncludeWebSite = model.IncludeWebSite;
        settings.IncludeSearchAction = model.IncludeSearchAction;

        return Edit(site, settings, context);
    }
}
