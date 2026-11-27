using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.ServiceLevels.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ServiceLevels.Drivers;

/// <summary>
/// The policy's row in the list of policies, and the General and Targets cards of its editor. The targets grid shows
/// the metrics and priorities of the policy's scope. Other modules add cards with their own drivers and keep their
/// values in the policy's <c>Properties</c>.
/// </summary>
internal sealed class ServiceLevelPolicyDisplayDriver : DisplayDriver<ServiceLevelPolicy>
{
    private readonly IEnumerable<IServiceLevelScopeProvider> _scopes;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public ServiceLevelPolicyDisplayDriver(
        IEnumerable<IServiceLevelScopeProvider> scopes,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<ServiceLevelPolicyDisplayDriver> stringLocalizer)
    {
        _scopes = scopes;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ServiceLevelPolicy policy, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = policy.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("ServiceLevelPolicy_Fields_SummaryAdmin", policy)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ServiceLevelPolicy_Description_SummaryAdmin", policy)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(policy.Description))),
            Initialize<ServiceLevelPolicyRowViewModel>("ServiceLevelPolicy_Tags_SummaryAdmin", model =>
            {
                model.Policy = policy;
                model.ScopeDisplayName = ServiceLevelScopeNames.Get(_localizerFactory, _scopes, policy.Scope);
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Scope:5"),
            View("ServiceLevelPolicy_Buttons_SummaryAdmin", policy).Location("Actions:5"),
            View("ServiceLevelPolicy_ActionsMenu_SummaryAdmin", policy).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(ServiceLevelPolicy policy, BuildEditorContext context)
    {
        var scope = FindScope(policy.Scope);

        return Combine(
            Initialize<ServiceLevelPolicyViewModel>("ServiceLevelPolicyGeneral_Edit", model =>
            {
                model.Name = policy.Name;
                model.Description = policy.Description;
                model.HoursScheduleId = policy.HoursScheduleId;
                model.WarningPercent = policy.WarningPercent;
                model.ScopeDisplayName = ServiceLevelScopeNames.Get(_localizerFactory, _scopes, policy.Scope);
            }).Location(l => l.Zone("Content", "1").Card(S["General"], "1")),
            Initialize<ServiceLevelTargetsViewModel>("ServiceLevelPolicyTargets_Edit", async model =>
            {
                model.Metrics = await GetMetricsAsync(scope);
                model.Priorities = await GetPrioritiesAsync(scope);
                model.Cells = ServiceLevelTargetGrid.BuildCells(policy.Targets, model.Metrics, model.Priorities);
            }).Location(l => l.Zone("Content", "1").Card(S["Targets"], "2")));
    }

    public override async Task<IDisplayResult> UpdateAsync(ServiceLevelPolicy policy, UpdateEditorContext context)
    {
        var general = new ServiceLevelPolicyViewModel();
        var targets = new ServiceLevelTargetsViewModel();

        await context.Updater.TryUpdateModelAsync(general, Prefix);
        await context.Updater.TryUpdateModelAsync(targets, Prefix);

        var scope = FindScope(policy.Scope);

        policy.Name = general.Name?.Trim();
        policy.Description = general.Description?.Trim();
        policy.HoursScheduleId = string.IsNullOrWhiteSpace(general.HoursScheduleId) ? null : general.HoursScheduleId.Trim();
        policy.WarningPercent = general.WarningPercent;
        policy.Targets = ServiceLevelTargetGrid.ApplyCells(policy.Targets, targets.Cells, await GetMetricsAsync(scope), await GetPrioritiesAsync(scope));

        return Edit(policy, context);
    }

    private IServiceLevelScopeProvider FindScope(string name)
        => _scopes.FirstOrDefault(scope => string.Equals(scope.Name, name, StringComparison.OrdinalIgnoreCase));

    private static async Task<IList<ServiceLevelOption>> GetMetricsAsync(IServiceLevelScopeProvider scope)
        => scope is null ? [] : [.. await scope.GetMetricsAsync()];

    private static async Task<IList<ServiceLevelOption>> GetPrioritiesAsync(IServiceLevelScopeProvider scope)
        => scope is null ? [] : [.. await scope.GetPrioritiesAsync()];
}
