using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;
using CrestApps.OrchardCore.NLWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.NLWeb.Drivers;

/// <summary>
/// The AI card of the NLWeb settings, shown when AI is enabled: query rewriting, ranking, summaries, answers and the
/// deployment they use.
/// </summary>
internal sealed class NLWebAISettingsDisplayDriver : SiteDisplayDriver<NLWebSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIDeploymentManager _deploymentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebAISettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    public NLWebAISettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAIDeploymentManager deploymentManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _deploymentManager = deploymentManager;
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

        var deployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Utility);

        return Initialize<NLWebAISettingsViewModel>("NLWebAISettings_Edit", model =>
        {
            model.RewriteQueries = settings.RewriteQueries;
            model.RankResults = settings.RankResults;
            model.AllowSummarize = settings.AllowSummarize;
            model.AllowGenerate = settings.AllowGenerate;
            model.DeploymentName = settings.DeploymentName;
            model.Deployments = deployments;
        }).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, NLWebSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, NLWebPermissionsProvider.ManageNLWebSettings))
        {
            return null;
        }

        var model = new NLWebAISettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.RewriteQueries = model.RewriteQueries;
        settings.RankResults = model.RankResults;
        settings.AllowSummarize = model.AllowSummarize;
        settings.AllowGenerate = model.AllowGenerate;
        settings.DeploymentName = string.IsNullOrWhiteSpace(model.DeploymentName) ? null : model.DeploymentName.Trim();

        return await EditAsync(site, settings, context);
    }
}
