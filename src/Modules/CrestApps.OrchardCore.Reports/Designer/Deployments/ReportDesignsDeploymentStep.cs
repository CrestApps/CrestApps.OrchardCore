using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Recipes;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Reports.Designer.Deployments;

/// <summary>
/// A deployment step that exports every designed report and view. Share links are never exported, because a link's
/// token cannot be recovered from what is stored and must not leave the site.
/// </summary>
public sealed class ReportDesignsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ReportDesignsDeploymentStep>("Reporting");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ReportDesignsDeploymentStep>("Designed Reports and Views");

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignsDeploymentStep"/> class.
    /// </summary>
    public ReportDesignsDeploymentStep()
    {
        Name = ReportDesignsRecipeStep.Name;
        Category = _category;
        Title = _title;
    }
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
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var reports = new JsonArray();
        var views = new JsonArray();

        foreach (var design in (await _designs.GetAllAsync()).OrderBy(design => design.DisplayText, StringComparer.OrdinalIgnoreCase))
        {
            reports.Add(await ToNodeAsync(design, design.OwnerId, owners));
        }

        foreach (var view in (await _views.GetAllAsync()).OrderBy(view => view.DisplayText, StringComparer.OrdinalIgnoreCase))
        {
            views.Add(await ToNodeAsync(view, view.OwnerId, owners));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Views"] = views,
            ["Reports"] = reports,
        });
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
/// Shows the designed reports deployment step in the deployment plan editor.
/// </summary>
internal sealed class ReportDesignsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ReportDesignsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(ReportDesignsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ReportDesignsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ReportDesignsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(ReportDesignsDeploymentStep step, BuildEditorContext context)
    {
        return View("ReportDesignsDeploymentStep_Fields_Edit", step).Location("Content");
    }
}
