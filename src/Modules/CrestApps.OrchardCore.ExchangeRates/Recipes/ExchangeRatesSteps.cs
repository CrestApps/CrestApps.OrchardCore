using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.ExchangeRates.Models;
using CrestApps.OrchardCore.ExchangeRates.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.Localization;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.ExchangeRates.Recipes;

/// <summary>
/// Exports every exchange rate.
/// </summary>
public sealed class ExchangeRatesDeploymentStep : DeploymentStep
{
    public const string StepName = "ExchangeRates";

    public const string EntriesName = "Rates";

    private static readonly LocalizationSource _category = LocalizationSource.Create<ExchangeRatesDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ExchangeRatesDeploymentStep>("Exchange rates");

    public override string Name => StepName;

    [JsonIgnore]
    public override LocalizationSource Category => _category;

    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class ExchangeRatesDeploymentSource : CatalogDeploymentSource<ExchangeRatesDeploymentStep, ExchangeRate>
{
    public ExchangeRatesDeploymentSource(IExchangeRateManager manager)
        : base(ExchangeRatesDeploymentStep.EntriesName, manager)
    {
    }
}

/// <summary>
/// Creates or updates the exchange rates a recipe lists under <c>Rates</c>, matched by id.
/// </summary>
internal sealed class ExchangeRatesStep : CatalogRecipeStep<ExchangeRate>
{
    public ExchangeRatesStep(IExchangeRateManager manager)
        : base(ExchangeRatesDeploymentStep.StepName, ExchangeRatesDeploymentStep.EntriesName, manager)
    {
    }
}

internal sealed class ExchangeRatesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(ExchangeRatesDeploymentStep);

    protected override string DisplayText => "Exchange rates";

    protected override string Description => "Exports every exchange rate.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class ExchangeRatesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ExchangeRatesDeploymentStep.StepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(ExchangeRatesDeploymentStep.StepName).Description("Recipe step discriminator. Must be 'ExchangeRates'.")),
                (ExchangeRatesDeploymentStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The rate's 26-character id. When it matches a stored rate, that rate is updated.")),
                            ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ISO 4217 code of the currency converted, such as EUR.")),
                            ("BaseCurrency", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ISO 4217 code of the currency converted to, such as USD.")),
                            ("Rate", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("How many units of the base currency one unit of the currency is worth.")),
                            ("Date", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The day the rate takes effect, such as 2026-11-14.")),
                            ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Where the rate came from: manual, or a provider's name.")))
                        .Required("Currency", "BaseCurrency", "Rate", "Date")
                        .AdditionalProperties(true))
                    .Description("The exchange rates to create or update.")))
            .Required("name", ExchangeRatesDeploymentStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

/// <summary>
/// Describes <see cref="ExchangeRateSettings"/> for the Settings recipe step.
/// </summary>
internal sealed class ExchangeRateSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(ExchangeRateSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The currency the store reports in, the currencies a provider fetches rates for and the provider.")
            .Properties(
                ("BaseCurrency", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The ISO 4217 code of the currency the store reports in, such as USD.")),
                ("Currencies", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The currencies the provider fetches rates for each day.")),
                ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The name of the provider that fetches the rates each day, or null to enter them by hand.")))
            .AdditionalProperties(true);
}
