using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class SettingsSchemaStep : IRecipeStep
{
    private JsonSchema _schema;

    public string Name => "Settings";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_schema != null)
        {
            return new ValueTask<JsonSchema>(_schema);
        }

        var builder = new JsonSchemaBuilder();
        builder
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("settings")
                )
            )
            .Required("name")
            .MinProperties(2) // at least "name" plus one other key
            .AdditionalProperties(true); // allow any other keys of any type

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
