using CrestApps.Core.AI.DataSources;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;
using CrestApps.OrchardCore.NLWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.NLWeb.Drivers;

/// <summary>
/// The AI data source card of the NLWeb settings, shown when AI Data Sources is enabled: the data source whose vector
/// index is searched by meaning, beside the keyword indexes.
/// </summary>
internal sealed class NLWebDataSourceSettingsDisplayDriver : SiteDisplayDriver<NLWebSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIDataSourceStore _dataSourceStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebDataSourceSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataSourceStore">The AI data source store.</param>
    public NLWebDataSourceSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAIDataSourceStore dataSourceStore)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataSourceStore = dataSourceStore;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => NLWebConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ISite site, NLWebSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, NLWebPermissionsProvider.ManageNLWebSettings))
        {
            return null;
        }

        var dataSources = await _dataSourceStore.GetAllAsync();

        return Initialize<NLWebDataSourceSettingsViewModel>("NLWebDataSourceSettings_Edit", model =>
        {
            model.DataSourceId = settings.DataSourceId;
            model.DataSources = dataSources
                .OrderBy(dataSource => dataSource.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                .Select(dataSource => new SelectListItem(dataSource.DisplayText, dataSource.ItemId))
                .ToList();
        }).Location("Content:3")
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, NLWebSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, NLWebPermissionsProvider.ManageNLWebSettings))
        {
            return null;
        }

        var model = new NLWebDataSourceSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.DataSourceId = string.IsNullOrWhiteSpace(model.DataSourceId) ? null : model.DataSourceId.Trim();

        return await EditAsync(site, settings, context);
    }
}
