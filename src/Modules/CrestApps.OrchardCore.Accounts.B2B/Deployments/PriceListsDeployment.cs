using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts.B2B.Recipes;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Accounts.B2B.Deployments;

/// <summary>
/// Exports every price list, as a <c>PriceLists</c> recipe step.
/// </summary>
public sealed class PriceListsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<PriceListsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PriceListsDeploymentStep>("Price Lists");

    public override string Name => nameof(PriceListsDeploymentStep);

    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class PriceListsDeploymentSource : DeploymentSourceBase<PriceListsDeploymentStep>
{
    private readonly IPriceListStore _priceLists;

    public PriceListsDeploymentSource(IPriceListStore priceLists)
    {
        _priceLists = priceLists;
    }

    protected override async Task ProcessAsync(PriceListsDeploymentStep step, DeploymentPlanResult result)
    {
        var data = new JsonArray();

        foreach (var list in await _priceLists.ListAllAsync())
        {
            data.Add(new JsonObject
            {
                ["ItemId"] = list.ItemId,
                ["Name"] = list.Name,
                ["PercentOff"] = list.PercentOff,
                ["Currency"] = list.Currency,
                ["Entries"] = JsonSerializer.SerializeToNode(list.Entries ?? [], JOptions.Default),
            });
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = PriceListsStep.Name,
            ["PriceLists"] = data,
        });
    }
}

internal sealed class PriceListsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, PriceListsDeploymentStep>
{
    public override IDisplayResult Display(PriceListsDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("PriceListsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("PriceListsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
}

internal sealed class PriceListsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PriceListsDeploymentStep);

    protected override string DisplayText => "Price Lists";

    protected override string Description => "Exports every price list with its prices.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
