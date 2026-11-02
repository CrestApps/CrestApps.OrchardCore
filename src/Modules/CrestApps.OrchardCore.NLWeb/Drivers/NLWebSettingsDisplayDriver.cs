using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;
using CrestApps.OrchardCore.NLWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Search.Abstractions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.NLWeb.Drivers;

/// <summary>
/// Settings › Search › NLWeb: the search indexes and content types questions are answered from, the number of
/// results, the default response mode, and who can call the endpoints.
/// </summary>
internal sealed class NLWebSettingsDisplayDriver : SiteDisplayDriver<NLWebSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<ISearchService> _searchServices;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="indexProfileStore">The search index store.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="searchServices">The search services of the enabled search providers.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NLWebSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IIndexProfileStore indexProfileStore,
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<ISearchService> searchServices,
        IStringLocalizer<NLWebSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _indexProfileStore = indexProfileStore;
        _contentDefinitionManager = contentDefinitionManager;
        _searchServices = searchServices;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => NLWebConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ISite site, NLWebSettings settings, BuildEditorContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (!await _authorizationService.AuthorizeAsync(httpContext?.User, NLWebPermissionsProvider.ManageNLWebSettings))
        {
            return null;
        }

        var indexProfiles = (await _indexProfileStore.GetAllAsync())
            .Where(profile => profile.Type == IndexingConstants.ContentsIndexSource)
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase);

        var contentTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .OrderBy(type => type.DisplayName, StringComparer.CurrentCultureIgnoreCase);

        return Initialize<NLWebSettingsViewModel>("NLWebSettings_Edit", model =>
        {
            model.IndexProfileNames = settings.IndexProfileNames ?? [];
            model.ContentTypes = settings.ContentTypes ?? [];
            model.MaxResults = settings.MaxResults;
            model.DefaultMode = settings.DefaultMode;
            model.AllowLegacyRequests = settings.AllowLegacyRequests;
            model.AllowCrossOrigin = settings.AllowCrossOrigin;
            model.HasSearchProvider = _searchServices.Any();
            model.AskUrl = httpContext is null
                ? $"/{NLWebConstants.AskRoute}"
                : $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}/{NLWebConstants.AskRoute}";
            model.AvailableIndexProfiles = indexProfiles
                .Select(profile => new SelectListItem($"{profile.Name} ({profile.ProviderName})", profile.Name))
                .ToList();
            model.AvailableContentTypes = contentTypes
                .Select(type => new SelectListItem(type.DisplayName, type.Name))
                .ToList();
            model.Modes =
            [
                new SelectListItem(S["List: the results only"], NLWebProtocol.Modes.List),
                new SelectListItem(S["Summarize: the results and a summary of them"], NLWebProtocol.Modes.Summarize),
                new SelectListItem(S["Generate: an answer written from the results"], NLWebProtocol.Modes.Generate),
            ];
        }).Location("Content:1")
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, NLWebSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, NLWebPermissionsProvider.ManageNLWebSettings))
        {
            return null;
        }

        var model = new NLWebSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.MaxResults < 1 || model.MaxResults > NLWebConstants.MaxResultsLimit)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxResults), S["The number of results must be between 1 and {0}.", NLWebConstants.MaxResultsLimit]);
        }

        settings.IndexProfileNames = Clean(model.IndexProfileNames);
        settings.ContentTypes = Clean(model.ContentTypes);
        settings.MaxResults = Math.Clamp(model.MaxResults, 1, NLWebConstants.MaxResultsLimit);
        settings.DefaultMode = model.DefaultMode switch
        {
            NLWebProtocol.Modes.Summarize or NLWebProtocol.Modes.Generate => model.DefaultMode,
            _ => NLWebProtocol.Modes.List,
        };
        settings.AllowLegacyRequests = model.AllowLegacyRequests;
        settings.AllowCrossOrigin = model.AllowCrossOrigin;

        return await EditAsync(site, settings, context);
    }

    private static string[] Clean(string[] values)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
