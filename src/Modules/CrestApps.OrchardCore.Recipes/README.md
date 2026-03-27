# CrestApps Recipes

**CrestApps Recipes** provides a structured way to define and retrieve recipe steps. This feature is particularly useful in the AI Suite, where it helps guide AI to generate valid Orchard Core recipes.

## Creating a Recipe Step

To define a recipe step, implement the `IRecipeStep` interface and register your implementation as a service.

Here's an example of a recipe step for a **Settings** step. This step requires a JSON object with a `"name"` property set to `"settings"` and allows any other properties of any type.

```csharp
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
            .MinProperties(2) // at least "name" plus one other property
            .AdditionalProperties(true); // allow any other properties of any type

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
```

## Registering Your Recipe Step

Register your implementation as a scoped service in your Orchard Core module or startup:

```csharp
services.AddScoped<IRecipeStep, SettingsSchemaStep>();
```

## Using Recipe Steps

* **Retrieve available recipe steps:** Use the `RecipeStepsService` to get a list of all registered recipe step names.
* **Execute a recipe step:** Use the `RecipeExecutionService` to execute any registered recipe step.
