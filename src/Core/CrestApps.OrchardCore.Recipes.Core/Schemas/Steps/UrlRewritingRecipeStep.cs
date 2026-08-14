using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "UrlRewriting" recipe step — creates or updates URL rewrite rules such as redirect or
/// rewrite rules.
/// </summary>
public sealed class UrlRewritingRecipeStep : IRecipeStep
{
    private readonly IRewriteRuleSchemaService _rewriteRuleSchemaService;

    private JsonSchema _cached;

    /// <inheritdoc />
    public string Name => "UrlRewriting";

    /// <summary>
    /// Initializes a new instance of the <see cref="UrlRewritingRecipeStep"/> class.
    /// </summary>
    /// <param name="rewriteRuleSchemaService">The service that composes the rewrite rule source schemas.</param>
    public UrlRewritingRecipeStep(IRewriteRuleSchemaService rewriteRuleSchemaService)
    {
        _rewriteRuleSchemaService = rewriteRuleSchemaService;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= await CreateSchemaAsync(cancellationToken);

        return _cached;
    }

    private async ValueTask<JsonSchema> CreateSchemaAsync(CancellationToken cancellationToken)
    {
        var ruleSchema = await _rewriteRuleSchemaService.GetRuleSchemaAsync(cancellationToken);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("UrlRewriting")
                    .Description("Recipe step discriminator. Must be 'UrlRewriting'.")),
                ("Rules", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(ruleSchema)
                    .MinItems(1)
                    .Description("The URL rewrite rules to create or update.")))
            .Required("name", "Rules")
            .AdditionalProperties(true)
            .Build();
    }
}
