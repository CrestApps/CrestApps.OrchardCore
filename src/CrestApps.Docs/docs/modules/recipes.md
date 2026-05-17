---
sidebar_label: Recipes
sidebar_position: 6
title: CrestApps Recipes
description: JSON-Schema support for Orchard Core recipes with per-step validation and AI guidance.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Recipes |
| **Feature ID** | `CrestApps.OrchardCore.Recipes` |

Provides recipe steps for all modules.

## Overview

**CrestApps Recipes** adds JSON-Schema support for Orchard Core recipes.

It exposes strongly-typed, per-step JSON schema definitions (one schema per recipe step) that match how Orchard Core actually parses and executes each step (i.e., the classes inheriting from `NamedRecipeStepHandler`).

This is especially useful when recipes are generated programmatically (e.g., by AI agents) because it enables:

- **Validation before execution**: catch invalid or incomplete recipe payloads early.
- **Better AI guidance**: provide the model with an explicit contract for each step.
- **Safer automation**: reduce trial-and-error imports and unpredictable runtime failures.

At runtime the feature can compose all known step schemas into a single "recipe" schema for an object with a `steps` array.

## Exporting recipe step schemas for AI skills

This repository also includes an exporter utility for synchronizing the runtime recipe schemas into the Orchard Core AgentSkills repository:

```powershell
dotnet run --project .\tests\CrestApps.OrchardCore.RecipeSchemaExporter\CrestApps.OrchardCore.RecipeSchemaExporter.csproj --framework net10.0 --no-build
```

By default, the tool first locates the `CrestApps.OrchardCore` repository root by walking up from the running process directory until it finds a marker such as `global.json`, `NuGet.config`, `CrestApps.OrchardCore.sln`, or `CrestApps.OrchardCore.slnx`. It then writes:

- one `<step>.schema.json` file per `IRecipeStep`
- `recipe.schema.json` for the root recipe contract
- `index.json` for step-to-file lookup

to the sibling AgentSkills repository path:

`..\CrestApps.AgentSkills\src\CrestApps.AgentSkills\orchardcore\orchardcore-recipes\references\recipe-schemas`

This keeps the `orchardcore-recipes` skill aligned with the runtime JSON schema definitions used by `RecipeSchemaService`.

If the sibling `CrestApps.AgentSkills` repository is missing, the exporter throws a `DirectoryNotFoundException` that tells contributors to clone `https://github.com/CrestApps/CrestApps.AgentSkills.git` next to `CrestApps.OrchardCore`, or to pass an explicit output directory manually.

The `ReplaceContentDefinition` step uses the same expanded `ContentTypes` and `ContentParts` schema structure as `ContentDefinition`, so AI-generated replacement steps can rely on the same predefined nested shape instead of loose object arrays.

The generic `Settings` step now composes feature-gated site settings schema fragments in the same way `ContentDefinition` composes part and field settings. Base site settings such as `HomeRoute`, `CacheMode`, and `ResourceDebugMode` are modeled directly, while feature-specific settings objects such as `AdminSettings`, `GeneralAISettings`, `DisplayNameSettings`, and other contributed settings only appear when their owning feature registers a schema definition.

The exported set covers Orchard Core's built-in settings, identity, and localization recipe steps, including `Users`, `custom-user-settings`, `AzureADSettings`, `MicrosoftAccountSettings`, `FacebookCoreSettings`, `FacebookLoginSettings`, `GitHubAuthenticationSettings`, `TwitterSettings`, `OpenIdApplication`, `OpenIdClientSettings`, `OpenIdScope`, `OpenIdServerSettings`, `OpenIdValidationSettings`, `Translations`, and `DynamicDataTranslations`.

## Creating a Recipe Step

To define a recipe step, implement the `IRecipeStep` interface and register your implementation as a service.

Here's an example of a recipe step for a **Settings** step. This step requires a JSON object with a `"name"` property set to `"settings"` and can compose contributed site settings definitions.

```csharp
internal sealed class SettingsSchemaStep : IRecipeStep
{
    private readonly IEnumerable<ISiteSettingsSchemaDefinition> _schemaDefinitions;
    private JsonSchema _schema;

    public SettingsSchemaStep(IEnumerable<ISiteSettingsSchemaDefinition> schemaDefinitions)
    {
        _schemaDefinitions = schemaDefinitions;
    }

    public string Name => "Settings";

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
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
                ),
                ("HomeRoute", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Area", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Controller", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Action", new JsonSchemaBuilder().Type(SchemaValueType.String))
                    )
                    .AdditionalProperties(true)
                )
            )
            .Required("name")
            .AdditionalProperties(true);

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
```

Feature-specific site settings can contribute additional schema definitions by registering `ISiteSettingsSchemaDefinition` from feature-gated startups, which keeps the `Settings` step aligned with the actual `SiteDisplayDriver<TSettings>` registrations enabled for the tenant.

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
