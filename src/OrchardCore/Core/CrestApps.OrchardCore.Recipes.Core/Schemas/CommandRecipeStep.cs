using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "command" recipe step — executes CLI commands.
/// </summary>
public sealed class CommandRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "command";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("command")),
                ("Commands", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .MinItems(1)))
            .Required("name", "Commands")
            .AdditionalProperties(true)
            .Build();
    }
}
