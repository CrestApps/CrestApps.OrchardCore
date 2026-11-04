using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Assets.Recipes;

/// <summary>
/// Imports the Assets step: Types, Assets, each created or updated by ItemId with the same checks as the admin.
/// </summary>
internal sealed class AssetsRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string RecipeName = "Assets";

    private readonly IServiceProvider _services;

    public AssetsRecipeStep(IServiceProvider services)
        : base(RecipeName)
    {
        _services = services;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        await CatalogRecipeSections.ImportAsync(context, "Types", _services.GetRequiredService<AssetTypeManager>(), "asset type");
        await CatalogRecipeSections.ImportAsync(context, "Assets", _services.GetRequiredService<AssetManager>(), "asset");
    }
}

/// <summary>
/// Describes the Assets recipe step.
/// </summary>
internal sealed class AssetsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => AssetsRecipeStep.RecipeName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Every asset type, and every asset with its owner, place and parent. Records are matched by ItemId.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(AssetsRecipeStep.RecipeName).Description("Recipe step discriminator. Must be 'Assets'.")),
                ("Types", Array("Asset types with their attributes and meters.")),
                ("Assets", Array("Assets, parents first; their meter readings stay where they were taken.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Array(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description(description);
}

/// <summary>
/// Exports every asset type, and every asset with its owner, place and parent.
/// </summary>
public sealed class AssetsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AssetsDeploymentStep>("Asset");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AssetsDeploymentStep>("Asset types and assets");

    /// <inheritdoc/>
    public override string Name => AssetsRecipeStep.RecipeName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
    /// <summary>
    /// Gets or sets whether the assets are exported too.
    /// </summary>
    public bool IncludeAssets { get; set; } = true;
}

internal sealed class AssetsDeploymentSource : DeploymentSourceBase<AssetsDeploymentStep>
{
    private readonly IServiceProvider _services;

    public AssetsDeploymentSource(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ProcessAsync(AssetsDeploymentStep step, DeploymentPlanResult result)
    {
        var node = new JsonObject { ["name"] = step.Name };

        node["Types"] = CatalogRecipeSections.Export((await _services.GetRequiredService<AssetTypeManager>().GetAllAsync()));
        if (step.IncludeAssets)
        {
            node["Assets"] = CatalogRecipeSections.Export(ParentsFirst((await _services.GetRequiredService<AssetManager>().GetAllAsync()).ToList(), item => item.ParentAssetId));
        }

        result.Steps.Add(node);
    }

    // Parents before their children, so each child's parent exists when it is imported.
    private static List<T> ParentsFirst<T>(List<T> items, Func<T, string> parentOf)
        where T : CrestApps.Core.Models.CatalogItem
    {
        var ordered = new List<T>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var ids = items.Select(item => item.ItemId).ToHashSet(StringComparer.Ordinal);
        var left = items.ToList();

        while (left.Count > 0)
        {
            var ready = left.Where(item => parentOf(item) is not { Length: > 0 } parent || !ids.Contains(parent) || placed.Contains(parent)).ToList();

            if (ready.Count == 0)
            {
                ready = left;
            }

            foreach (var item in ready)
            {
                ordered.Add(item);
                placed.Add(item.ItemId);
                left.Remove(item);
            }
        }

        return ordered;
    }
}

/// <summary>
/// The deployment step's settings form.
/// </summary>
public class AssetsDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets whether the assets are exported too.
    /// </summary>
    public bool IncludeAssets { get; set; } = true;
}

internal sealed class AssetsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AssetsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AssetsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("AssetsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("AssetsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(AssetsDeploymentStep step, BuildEditorContext context)
        => Initialize<AssetsDeploymentStepViewModel>("AssetsDeploymentStep_Fields_Edit", model =>
        {
            model.IncludeAssets = step.IncludeAssets;
        }).Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(AssetsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AssetsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        step.IncludeAssets = model.IncludeAssets;

        return Edit(step, context);
    }
}

/// <summary>
/// Describes the Assets deployment step.
/// </summary>
internal sealed class AssetsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(AssetsDeploymentStep);

    protected override string DisplayText => "Asset types and assets";

    protected override string Description => "Exports every asset type, and every asset with its owner, place and parent.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAssets", DeploymentSchemaBuilders.Boolean("When true, exports the assets too."));
    }
}
