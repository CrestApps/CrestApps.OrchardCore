using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Recipes;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Reports.Designer.Deployments;

/// <summary>
/// A deployment step that exports designed reports and views: every one of them, or the ones picked. Share links are
/// never exported, because a link's token cannot be recovered from what is stored and must not leave the site.
/// </summary>
public sealed class ReportDesignsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ReportDesignsDeploymentStep>("Reporting");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ReportDesignsDeploymentStep>("Designed Reports and Views");

    /// <inheritdoc />
    public override string Name => ReportDesignsRecipeStep.Name;

    /// <inheritdoc />
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets a value indicating whether every report and view is exported. It is on by default, so a plan saved
    /// before reports could be picked keeps exporting all of them.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the identifiers of the reports to export when <see cref="IncludeAll"/> is off.
    /// </summary>
    public string[] ReportIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifiers of the views to export when <see cref="IncludeAll"/> is off. The views the exported
    /// reports and views read are exported too.
    /// </summary>
    public string[] ViewIds { get; set; } = [];
}

/// <summary>
/// Writes designed reports and views into a deployment plan as a <c>ReportDesigns</c> recipe step, with each owner's
/// user name so the import can match the owner on another site.
/// </summary>
internal sealed class ReportDesignsDeploymentSource : DeploymentSourceBase<ReportDesignsDeploymentStep>
{
    private readonly ICatalog<ReportDesign> _designs;
    private readonly ICatalog<ReportView> _views;
    private readonly UserManager<IUser> _userManager;

    public ReportDesignsDeploymentSource(
        ICatalog<ReportDesign> designs,
        ICatalog<ReportView> views,
        UserManager<IUser> userManager)
    {
        _designs = designs;
        _views = views;
        _userManager = userManager;
    }

    protected override async Task ProcessAsync(ReportDesignsDeploymentStep step, DeploymentPlanResult result)
    {
        var allDesigns = await _designs.GetAllAsync();
        var allViews = await _views.GetAllAsync();
        var designs = step.IncludeAll ? allDesigns : Pick(allDesigns, step.ReportIds);
        var views = step.IncludeAll ? allViews : WithDependencies(designs, Pick(allViews, step.ViewIds), allViews);

        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var reportNodes = new JsonArray();
        var viewNodes = new JsonArray();

        foreach (var design in designs.OrderBy(design => design.DisplayText, StringComparer.OrdinalIgnoreCase))
        {
            reportNodes.Add(await ToNodeAsync(design, design.OwnerId, owners));
        }

        foreach (var view in views.OrderBy(view => view.DisplayText, StringComparer.OrdinalIgnoreCase))
        {
            viewNodes.Add(await ToNodeAsync(view, view.OwnerId, owners));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Views"] = viewNodes,
            ["Reports"] = reportNodes,
        });
    }

    private static List<T> Pick<T>(IEnumerable<T> items, string[] ids)
        where T : CatalogItem
    {
        var picked = new HashSet<string>(ids ?? [], StringComparer.Ordinal);

        return [.. items.Where(item => picked.Contains(item.ItemId))];
    }

    // The picked views, and every view the exported reports and views read, so each one runs on the other site.
    private static List<ReportView> WithDependencies(IEnumerable<ReportDesign> designs, List<ReportView> picked, IEnumerable<ReportView> allViews)
    {
        var byId = allViews.ToDictionary(view => view.ItemId, StringComparer.Ordinal);
        var included = new Dictionary<string, ReportView>(StringComparer.Ordinal);
        var pending = new Queue<string>();

        foreach (var view in picked)
        {
            pending.Enqueue(view.ItemId);
        }

        foreach (var design in designs)
        {
            foreach (var viewId in design.GetQueries().SelectMany(query => ViewsReadBy(query.Query)))
            {
                pending.Enqueue(viewId);
            }
        }

        while (pending.TryDequeue(out var viewId))
        {
            if (included.ContainsKey(viewId) || !byId.TryGetValue(viewId, out var view))
            {
                continue;
            }

            included[viewId] = view;

            foreach (var dependency in ViewsReadBy(view.Query))
            {
                pending.Enqueue(dependency);
            }
        }

        return [.. included.Values];
    }

    private static IEnumerable<string> ViewsReadBy(ReportQueryDefinition query)
    {
        return (query?.DataSets ?? [])
            .Where(dataSet => string.Equals(dataSet.Source, ReportsConstants.ViewsDataSource, StringComparison.Ordinal) && !string.IsNullOrEmpty(dataSet.DataSet))
            .Select(dataSet => dataSet.DataSet);
    }

    private async Task<JsonNode> ToNodeAsync<T>(T item, string ownerId, Dictionary<string, string> owners)
    {
        var node = JsonSerializer.SerializeToNode(item, ReportDesignerJson.Options);

        if (!string.IsNullOrEmpty(ownerId))
        {
            if (!owners.TryGetValue(ownerId, out var userName))
            {
                var user = await _userManager.FindByIdAsync(ownerId);

                userName = user is null ? null : await _userManager.GetUserNameAsync(user);
                owners[ownerId] = userName;
            }

            if (!string.IsNullOrEmpty(userName))
            {
                node["OwnerUserName"] = userName;
            }
        }

        return node;
    }
}

/// <summary>
/// Shows the designed reports deployment step in the deployment plan editor, where the reports and views to export
/// are picked.
/// </summary>
internal sealed class ReportDesignsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ReportDesignsDeploymentStep>
{
    private readonly ICatalog<ReportDesign> _designs;
    private readonly ICatalog<ReportView> _views;

    internal readonly IStringLocalizer S;

    public ReportDesignsDeploymentStepDisplayDriver(
        ICatalog<ReportDesign> designs,
        ICatalog<ReportView> views,
        IStringLocalizer<ReportDesignsDeploymentStepDisplayDriver> stringLocalizer)
    {
        _designs = designs;
        _views = views;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ReportDesignsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ReportDesignsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ReportDesignsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(ReportDesignsDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<ReportDesignsDeploymentStepViewModel>("ReportDesignsDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Reports = Entries(await _designs.GetAllAsync(), step.ReportIds);
            model.Views = Entries(await _views.GetAllAsync(), step.ViewIds);
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportDesignsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new ReportDesignsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.IncludeAll, m => m.Reports, m => m.Views);

        step.IncludeAll = model.IncludeAll;
        step.ReportIds = model.IncludeAll ? [] : Selected(model.Reports);
        step.ViewIds = model.IncludeAll ? [] : Selected(model.Views);

        if (!step.IncludeAll && step.ReportIds.Length == 0 && step.ViewIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Reports), S["Pick at least one report or view, or include all of them."]);
        }

        return Edit(step, context);
    }

    private static ReportDesignsDeploymentEntryViewModel[] Entries<T>(IEnumerable<T> items, string[] selected)
        where T : CatalogItem, IDisplayTextAwareModel
    {
        var picked = new HashSet<string>(selected ?? [], StringComparer.Ordinal);

        return [.. items
            .OrderBy(item => item.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ReportDesignsDeploymentEntryViewModel
            {
                ItemId = item.ItemId,
                DisplayText = item.DisplayText,
                IsSelected = picked.Contains(item.ItemId),
            })];
    }

    private static string[] Selected(ReportDesignsDeploymentEntryViewModel[] entries)
    {
        return [.. (entries ?? []).Where(entry => entry.IsSelected && !string.IsNullOrEmpty(entry.ItemId)).Select(entry => entry.ItemId)];
    }
}
