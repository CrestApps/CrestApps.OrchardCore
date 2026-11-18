using CrestApps.OrchardCore.Core.Deployments;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Transactions.Core.Services;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Transactions.Recipes;

/// <summary>
/// Imports the ledger a recipe carries (<see cref="TransactionLedgerImporter"/>): transactions with their timelines,
/// payment attempts, refunds and payment issues, written as they are, never posted again.
/// </summary>
internal sealed class TransactionLedgerRecipeStep : NamedRecipeStepHandler
{
    private readonly TransactionLedgerImporter _importer;

    public TransactionLedgerRecipeStep(TransactionLedgerImporter importer)
        : base(TransactionLedgerStep.Name)
    {
        _importer = importer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var result = await _importer.ImportAsync(context.Step);

        foreach (var error in result.Errors)
        {
            context.Errors.Add(error);
        }
    }
}

/// <summary>
/// Exports the whole ledger: every transaction with its timeline, and the payment attempts, refunds and payment issues.
/// </summary>
public sealed class TransactionsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TransactionsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TransactionsDeploymentStep>("Transactions");

    /// <inheritdoc/>
    public override string Name => TransactionLedgerStep.Name;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class TransactionsDeploymentSource : DeploymentSourceBase<TransactionsDeploymentStep>
{
    private readonly TransactionLedgerExporter _exporter;

    public TransactionsDeploymentSource(TransactionLedgerExporter exporter)
    {
        _exporter = exporter;
    }

    protected override async Task ProcessAsync(TransactionsDeploymentStep step, DeploymentPlanResult result)
        => result.Steps.Add(await _exporter.ExportAllAsync());
}

internal sealed class TransactionsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(TransactionsDeploymentStep);

    protected override string DisplayText => "Transactions";

    protected override string Description => "Exports the whole ledger: every transaction with its timeline, and the payment attempts, refunds and payment issues.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class TransactionLedgerRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => TransactionLedgerStep.Name;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        static JsonSchemaBuilder Records(string description)
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Array)
                .Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The record's 26-character id. A record the site already has is kept as it is.")))
                    .Required("ItemId")
                    .AdditionalProperties(true))
                .Description(description);

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TransactionLedgerStep.Name).Description($"Recipe step discriminator. Must be '{TransactionLedgerStep.Name}'.")),
                (TransactionLedgerStep.Transactions, Records("The transactions, as a Transactions deployment step writes them, with their timelines (Events), amounts, statuses, payer and reference. They are written as they are: nothing is posted, numbered or announced.")),
                (TransactionLedgerStep.PaymentAttempts, Records("The payment attempts. Needs the Checkout feature.")),
                (TransactionLedgerStep.PaymentRefunds, Records("The refunds. Needs the Checkout feature.")),
                (TransactionLedgerStep.PaymentIssues, Records("The payment issues staff follow up.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

/// <summary>
/// The ledger travels with deployment plans.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class TransactionsDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<TransactionsDeploymentSource, TransactionsDeploymentStep, SummaryDeploymentStepDisplayDriver<TransactionsDeploymentStep>>();
}

/// <summary>
/// Describes the ledger's recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class TransactionLedgerRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TransactionLedgerRecipeStepSchema>();
        services.AddDeploymentStepSchema<TransactionsDeploymentStepSchema>();
    }
}
