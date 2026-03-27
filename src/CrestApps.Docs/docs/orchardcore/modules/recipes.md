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

Provides recipe steps for CrestApps modules.

:::note Note
This feature is enabled by dependency only.
:::

## Overview

**CrestApps Recipes** adds JSON-Schema support for Orchard Core recipes.

It exposes strongly-typed, per-step JSON schema definitions (one schema per recipe step) that match how Orchard Core actually parses and executes each step (i.e., the classes inheriting from `NamedRecipeStepHandler`).

This is especially useful when recipes are generated programmatically (e.g., by AI agents) because it enables:

- **Validation before execution**: catch invalid or incomplete recipe payloads early.
- **Better AI guidance**: provide the model with an explicit contract for each step.
- **Safer automation**: reduce trial-and-error imports and unpredictable runtime failures.

At runtime the feature can compose all known step schemas into a single "recipe" schema for an object with a `steps` array.

## Exporting recipe step schemas for AI skills

This repository also includes a temporary exporter utility for synchronizing the runtime recipe schemas into the Orchard Core AgentSkills repository:

```powershell
dotnet run --project .\tests\CrestApps.OrchardCore.RecipeSchemaExporter\CrestApps.OrchardCore.RecipeSchemaExporter.csproj --framework net10.0 --no-build
```

By default, the tool first locates the `CrestApps.OrchardCore` repository root by walking up from the running process directory until it finds a marker such as `global.json`, `NuGet.config`, or `CrestApps.OrchardCore.slnx`. It then writes:

- one `<step>.schema.json` file per `IRecipeStep`
- `recipe.schema.json` for the root recipe contract
- `index.json` for step-to-file lookup

to the sibling AgentSkills repository path:

`..\CrestApps.AgentSkills\src\CrestApps.AgentSkills\orchardcore\orchardcore-recipes\references\recipe-schemas`

This keeps the `orchardcore-recipes` skill aligned with the runtime JSON schema definitions used by `RecipeSchemaService`.

If the sibling `CrestApps.AgentSkills` repository is missing, the exporter now fails with an actionable `DirectoryNotFoundException` that tells contributors to clone `https://github.com/CrestApps/CrestApps.AgentSkills.git` next to `CrestApps.OrchardCore`, or to pass an explicit output directory manually.

The `ReplaceContentDefinition` step now reuses the same expanded `ContentTypes` and `ContentParts` schema structure as `ContentDefinition`, so AI-generated replacement steps can rely on the same predefined nested shape instead of loose object arrays.

The exported set now also covers Orchard Core's built-in settings, identity, and localization recipe steps that were previously missing from CrestApps, including `Users`, `custom-user-settings`, `AzureADSettings`, `MicrosoftAccountSettings`, `FacebookCoreSettings`, `FacebookLoginSettings`, `GitHubAuthenticationSettings`, `TwitterSettings`, `OpenIdApplication`, `OpenIdClientSettings`, `OpenIdScope`, `OpenIdServerSettings`, `OpenIdValidationSettings`, `Translations`, and `DynamicDataTranslations`.

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
