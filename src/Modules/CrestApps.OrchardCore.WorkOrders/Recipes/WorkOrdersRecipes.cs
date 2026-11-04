using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.WorkOrders.Recipes;

/// <summary>
/// Imports the WorkOrders step: Checklists, ServiceLevels, SubStatuses, Types, Agreements, each created or updated by ItemId with the same checks as the admin.
/// </summary>
internal sealed class WorkOrdersRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string RecipeName = "WorkOrders";

    private readonly IServiceProvider _services;

    public WorkOrdersRecipeStep(IServiceProvider services)
        : base(RecipeName)
    {
        _services = services;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        await CatalogRecipeSections.ImportAsync(context, "Checklists", _services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>(), "checklist");
        await CatalogRecipeSections.ImportAsync(context, "ServiceLevels", _services.GetRequiredService<WorkOrderSettingManager<ServiceLevelPolicy>>(), "service level");
        await CatalogRecipeSections.ImportAsync(context, "SubStatuses", _services.GetRequiredService<WorkOrderSettingManager<WorkOrderSubStatus>>(), "sub-status");
        await CatalogRecipeSections.ImportAsync(context, "Types", _services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>(), "work order type");
        if (_services.GetService<ServiceAgreementManager>() is { } agreements)
        {
            await CatalogRecipeSections.ImportAsync(context, "Agreements", agreements, "service agreement");
        }
    }
}

/// <summary>
/// Describes the WorkOrders recipe step.
/// </summary>
internal sealed class WorkOrdersRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => WorkOrdersRecipeStep.RecipeName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Work order types, checklists, service levels and sub-statuses, and service agreements when asked. Records are matched by ItemId.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(WorkOrdersRecipeStep.RecipeName).Description("Recipe step discriminator. Must be 'WorkOrders'.")),
                ("Checklists", Array("Checklist templates with their sections and questions.")),
                ("ServiceLevels", Array("Service level policies with their targets.")),
                ("SubStatuses", Array("Sub-statuses.")),
                ("Types", Array("Work order types, which name their checklists and service level by ItemId.")),
                ("Agreements", Array("Service agreements; their customers, places and assets must be on the site too.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Array(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description(description);
}

/// <summary>
/// Exports work order types, checklists, service levels and sub-statuses, and service agreements when asked.
/// </summary>
public sealed class WorkOrdersDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<WorkOrdersDeploymentStep>("Work");
    private static readonly LocalizationSource _title = LocalizationSource.Create<WorkOrdersDeploymentStep>("Work order settings");

    /// <inheritdoc/>
    public override string Name => WorkOrdersRecipeStep.RecipeName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
    /// <summary>
    /// Gets or sets whether the agreements are exported too.
    /// </summary>
    public bool IncludeAgreements { get; set; }
}

internal sealed class WorkOrdersDeploymentSource : DeploymentSourceBase<WorkOrdersDeploymentStep>
{
    private readonly IServiceProvider _services;

    public WorkOrdersDeploymentSource(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ProcessAsync(WorkOrdersDeploymentStep step, DeploymentPlanResult result)
    {
        var node = new JsonObject { ["name"] = step.Name };

        node["Checklists"] = CatalogRecipeSections.Export((await _services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>().GetAllAsync()));
        node["ServiceLevels"] = CatalogRecipeSections.Export((await _services.GetRequiredService<WorkOrderSettingManager<ServiceLevelPolicy>>().GetAllAsync()));
        node["SubStatuses"] = CatalogRecipeSections.Export((await _services.GetRequiredService<WorkOrderSettingManager<WorkOrderSubStatus>>().GetAllAsync()));
        node["Types"] = CatalogRecipeSections.Export((await _services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().GetAllAsync()));
        if (step.IncludeAgreements)
        {
            node["Agreements"] = CatalogRecipeSections.Export((_services.GetService<ServiceAgreementManager>() is { } managerAgreements ? await managerAgreements.GetAllAsync() : Enumerable.Empty<ServiceAgreement>()));
        }

        result.Steps.Add(node);
    }
}

/// <summary>
/// The deployment step's settings form.
/// </summary>
public class WorkOrdersDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets whether the agreements are exported too.
    /// </summary>
    public bool IncludeAgreements { get; set; }
}

internal sealed class WorkOrdersDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, WorkOrdersDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(WorkOrdersDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("WorkOrdersDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("WorkOrdersDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(WorkOrdersDeploymentStep step, BuildEditorContext context)
        => Initialize<WorkOrdersDeploymentStepViewModel>("WorkOrdersDeploymentStep_Fields_Edit", model =>
        {
            model.IncludeAgreements = step.IncludeAgreements;
        }).Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(WorkOrdersDeploymentStep step, UpdateEditorContext context)
    {
        var model = new WorkOrdersDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        step.IncludeAgreements = model.IncludeAgreements;

        return Edit(step, context);
    }
}

/// <summary>
/// Describes the WorkOrders deployment step.
/// </summary>
internal sealed class WorkOrdersDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(WorkOrdersDeploymentStep);

    protected override string DisplayText => "Work order settings";

    protected override string Description => "Exports work order types, checklists, service levels and sub-statuses, and service agreements when asked.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAgreements", DeploymentSchemaBuilders.Boolean("When true, exports the agreements too."));
    }
}
