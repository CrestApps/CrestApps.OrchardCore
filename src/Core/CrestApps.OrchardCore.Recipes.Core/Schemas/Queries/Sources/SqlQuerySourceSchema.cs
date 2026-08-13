using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Queries.Sources;

/// <summary>
/// Describes the recipe schema for the <c>Sql</c> query source.
/// </summary>
public sealed class SqlQuerySourceSchema : QuerySourceSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "Sql";

    /// <inheritdoc />
    protected override string DisplayText => "SQL";

    /// <inheritdoc />
    protected override string Description => "Runs a SQL query against the tenant database.";

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Template"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(QuerySourceSchemaContext context)
    {
        yield return ("Template", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The SQL query text executed against the tenant database. It supports Liquid expressions and query parameters such as @limit:3."));
    }
}
