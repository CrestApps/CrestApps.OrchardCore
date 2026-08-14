using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "command" recipe step — executes CLI commands.
/// </summary>
public sealed class CommandRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "command";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("command").Description("Recipe step discriminator. Must be 'command'.")),
                ("Commands", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .MinItems(1)
                    .Description("Commands to execute in order. Each entry is a single command line string.")))
            .Required("name", "Commands")
            .AdditionalProperties(true)
            .Build();
    }
}
