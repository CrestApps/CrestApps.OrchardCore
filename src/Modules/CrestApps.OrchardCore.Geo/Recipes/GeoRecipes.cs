using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
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

namespace CrestApps.OrchardCore.Geo.Recipes;

/// <summary>
/// Imports the Geo step: Territories, each created or updated by ItemId with the same checks as the admin.
/// </summary>
internal sealed class GeoRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string RecipeName = "Geo";

    private readonly IServiceProvider _services;

    public GeoRecipeStep(IServiceProvider services)
        : base(RecipeName)
    {
        _services = services;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        await CatalogRecipeSections.ImportAsync(context, "Territories", _services.GetRequiredService<ServiceTerritoryManager>(), "service territory");
    }
}

/// <summary>
/// Describes the Geo recipe step.
/// </summary>
internal sealed class GeoRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => GeoRecipeStep.RecipeName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Every service territory, with its shape or postal codes, parent and time zone. Records are matched by ItemId.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(GeoRecipeStep.RecipeName).Description("Recipe step discriminator. Must be 'Geo'.")),
                ("Territories", Array("Service territories, with their ItemId, so children keep their parent.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Array(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description(description);
}

/// <summary>
/// Exports every service territory, with its shape or postal codes, parent and time zone.
/// </summary>
public sealed class GeoDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<GeoDeploymentStep>("Geo");
    private static readonly LocalizationSource _title = LocalizationSource.Create<GeoDeploymentStep>("Service territories");

    /// <inheritdoc/>
    public override string Name => GeoRecipeStep.RecipeName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class GeoDeploymentSource : DeploymentSourceBase<GeoDeploymentStep>
{
    private readonly IServiceProvider _services;

    public GeoDeploymentSource(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ProcessAsync(GeoDeploymentStep step, DeploymentPlanResult result)
    {
        var node = new JsonObject { ["name"] = step.Name };

        node["Territories"] = CatalogRecipeSections.Export(ParentsFirst((await _services.GetRequiredService<ServiceTerritoryManager>().GetAllAsync()).ToList(), item => item.ParentId));

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

internal sealed class GeoDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, GeoDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(GeoDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("GeoDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("GeoDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(GeoDeploymentStep step, BuildEditorContext context)
        => View("GeoDeploymentStep_Fields_Edit", step).Location("Content");
}

/// <summary>
/// Describes the Geo deployment step.
/// </summary>
internal sealed class GeoDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(GeoDeploymentStep);

    protected override string DisplayText => "Service territories";

    protected override string Description => "Exports every service territory, with its shape or postal codes, parent and time zone.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
