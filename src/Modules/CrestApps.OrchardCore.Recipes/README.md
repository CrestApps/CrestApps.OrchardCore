# CrestApps Recipes

**CrestApps Recipes** adds JSON-Schema support for Orchard Core recipes.

It exposes strongly-typed, per-step JSON schema definitions (one schema per recipe step) that match how Orchard Core actually parses and executes each step (i.e., the classes inheriting from `NamedRecipeStepHandler`).

This is especially useful when recipes are generated programmatically (e.g., by AI agents) because it enables:

- **Validation before execution**: catch invalid or incomplete recipe payloads early.
- **Better AI guidance**: provide the model with an explicit contract for each step.
- **Safer automation**: reduce trial-and-error imports and unpredictable runtime failures.

At runtime the feature can compose all known step schemas into a single “recipe” schema for an object with a `steps` array.

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

* **Retrieve available recipe steps:** Use the `RecipeSchemaService` to get a list of all registered recipe step names.
* **Validate and execute recipes:** Use the `RecipeExecutionService` alongside the step schemas to validate a recipe payload before importing.

## How schemas stay accurate

Orchard Core recipe steps are implemented by classes inheriting from `NamedRecipeStepHandler`. Each handler converts the incoming JSON into a specific model (for example `ContentStepModel`) and then processes it.

This module mirrors that contract by ensuring each `IRecipeStep` schema includes the known properties that Orchard Core expects (including correct property names and types).
