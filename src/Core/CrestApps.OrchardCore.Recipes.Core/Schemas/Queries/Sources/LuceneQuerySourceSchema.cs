using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Queries.Sources;

/// <summary>
/// Describes the recipe schema for the <c>Lucene</c> query source contributed by the Lucene feature.
/// </summary>
public sealed class LuceneQuerySourceSchema : QuerySourceSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "Lucene";

    /// <inheritdoc />
    protected override string DisplayText => "Lucene";

    /// <inheritdoc />
    protected override string Description => "Runs a Lucene query against a Lucene index.";

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Index", "Template"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(QuerySourceSchemaContext context)
    {
        yield return ("Index", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .WithSuggestions(context.Examples.IndexProfileNames)
            .Description("The name of the Lucene index the query runs against."));

        yield return ("Template", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The Lucene query JSON executed against the index. It supports Liquid and file expressions such as [file:text('Snippets/query.json')]."));
    }
}
