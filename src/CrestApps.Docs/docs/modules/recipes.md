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
dotnet run --project .\utilities\CrestApps.OrchardCore.RecipeSchemaExporter\CrestApps.OrchardCore.RecipeSchemaExporter.csproj --framework net10.0 --no-build
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

The `content` step now enumerates `ContentType` from the tenant's available content definitions and, for each enum value, contributes the known attached part names plus any configured field names nested under those parts. Common content item metadata such as `ContentItemId`, `DisplayText`, `Published`, `CreatedUtc`, and related properties remain available for every item, while the selected `ContentType` drives the extra IntelliSense hints for part and field payloads. Built-in part payloads such as `TitlePart.Title`, `MarkdownBodyPart.Markdown`, `HtmlBodyPart.Html`, `AutoroutePart.Path`, `AutoroutePart.SetHomepage`, `AliasPart.Alias`, `ContainedPart.ListContentItemId`, `PublishLaterPart.ScheduledPublishUtc`, `LayerMetadata.Zone`, `HtmlMenuItemPart.Html`, and `SeoMetaPart.PageTitle` now surface their expected properties alongside built-in field payloads such as `TextField.Text`, `MediaField.Paths`, `MediaField.MediaTexts`, `TaxonomyField.TermContentItemIds`, `GeoPointField.Latitude`, and `MarkdownField.Markdown` instead of falling back to untyped empty objects.

The `ContentDefinition` and `ReplaceContentDefinition` steps now also include built-in field and part schema contributors for Orchard Core settings. This covers the standard `OrchardCore.ContentFields` field types plus feature-gated contributors for `MediaField`, `MarkdownField`, `TaxonomyField`, `GeoPointField`, `LocalizationSetContentPickerField`, `PublishLaterPart`, `LayerMetadata`, and `HtmlMenuItemPart`, along with editor settings such as `HtmlBodyPartMonacoEditorSettings`, `HtmlBodyPartTrumbowygEditorSettings`, `MarkdownBodyPartWysiwygEditorSettings`, `BagPartBlocksEditorSettings`, `TextFieldHeaderDisplaySettings`, and `TextFieldMonacoEditorSettings`, so recipe authors get discoverable settings objects such as `TextFieldSettings`, `MediaFieldSettings`, `TaxonomyFieldSettings`, `GeoPointFieldSettings`, `MarkdownFieldSettings`, `HtmlBodyPartSettings`, `MarkdownBodyPartSettings`, and `HtmlMenuItemPartSettings` alongside the generic `ContentPartFieldSettings`.

The generic `Settings` step now composes feature-gated site settings schema fragments in the same way `ContentDefinition` composes part and field settings. Base site settings such as `HomeRoute`, `CacheMode`, and `ResourceDebugMode` are modeled directly, while feature-specific settings objects such as `AdminSettings`, `GeneralAISettings`, `DisplayNameSettings`, `DncRegistrySettings`, `UsaFtcDncRegistrySettings`, `CanadaDnclRegistrySettings`, and other contributed settings only appear when their owning feature registers a schema definition. Built-in login settings now also include `LoginSettings.AllowRememberMe` with its default `true` behavior.

The exported set covers Orchard Core's built-in settings, identity, and localization recipe steps, including `Users`, `custom-user-settings`, `AzureADSettings`, `MicrosoftAccountSettings`, `FacebookCoreSettings`, `FacebookLoginSettings`, `GitHubAuthenticationSettings`, `TwitterSettings`, `OpenIdApplication`, `OpenIdClientSettings`, `OpenIdScope`, `OpenIdServerSettings`, `OpenIdValidationSettings`, `Translations`, and `DynamicDataTranslations`.

## AI profile creation from templates

The AI module contributes a `CreateAIProfileFromTemplate` recipe step for creating or updating profiles from reusable AI templates whose source is **Profile**.

```json
{
  "name": "CreateAIProfileFromTemplate",
  "Profiles": [
    {
      "TemplateId": "customer-support",
      "Name": "customer-support-prod",
      "DisplayText": "Customer Support",
      "ChatDeploymentName": "gpt-4o"
    }
  ]
}
```

Behavior:

- `TemplateId` must point to an AI template whose source is **Profile**
- the step loads that profile template first and uses it to generate the starting `AIProfile`
- any fields you include in the recipe object then override the generated profile values
- any fields you do **not** include keep the values copied from the selected profile template
- if `TemplateId` points to a missing template, or to a template whose source is not **Profile**, the step adds a recipe error for that item and skips creating the profile

This means the step behaves like: **create profile from profile template, then apply explicit recipe overrides**.

The schema for this step intentionally mirrors the regular `AIProfile` step for profile fields such as `Name`, `DisplayText`, `Description`, `Type`, `PromptTemplate`, `PromptSubject`, `OrchestratorName`, `ChatDeploymentName`, `UtilityDeploymentName`, `Properties`, and `Settings`, while adding the required `TemplateId` selector that must resolve to a Profile template.

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

## Extending content definition schemas

`IContentSchemaDefinition` is the shared extension point that contributes schema fragments to the `ContentDefinition` and `ReplaceContentDefinition` recipe steps.

Use it when your feature adds a custom content part or content field and you want recipe validation, editor tooling, and AI-generated recipes to understand that feature's `Settings` object.

Use `PartSchemaDefinitionBase` when the schema belongs to a **content part**. It already marks the definition as `ContentDefinitionSchemaType.Part`, caches both the part settings schema and the content-item payload schema, and lets the implementation focus on the part-specific fragments.

Use `FieldSchemaDefinitionBase` when the schema belongs to a **content field**. It marks the definition as `ContentDefinitionSchemaType.Field`, caches both the field settings schema and the content-item payload schema, and lets the implementation describe the field-specific fragments.

Implement `IContentSchemaDefinition` directly only when the contribution does **not** fit either base class.

### Why these types exist

The `ContentDefinition` and `ReplaceContentDefinition` recipe steps have two nested settings surfaces:

- `ContentTypePartDefinitionRecords[].Settings` for settings that apply when a part is attached to a content type
- `ContentParts[].Settings` for the reusable content part definition itself

The Recipes module gathers all registered `IContentSchemaDefinition` services and merges their schema fragments into those nested `Settings` objects only when the owning feature is enabled. Definitions are grouped by `Name`, so if multiple contributors target the same part or field type, their schema fragments are combined into the final schema for that single Orchard name instead of replacing each other. That keeps validation aligned with the actual Orchard Core features available to the tenant.

The separate `IContentPartSchemaDefinition` and `IContentFieldSchemaDefinition` interfaces are still used by the `content` recipe step. They provide the part and field payload schemas for actual content item JSON, while `IContentSchemaDefinition` remains the shared contract for content-definition settings schemas.

### Implementing a part settings schema

This example adds schema support for a fictional `ContactCardPart`:

```csharp
using Json.Schema;

namespace MyModule.Schemas;

public sealed class ContactCardPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "ContactCardPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ContactCardPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ShowPhoneNumber", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("Layout", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Compact", "Full")
                            .Default("Compact")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }
}
```

Register it from the feature startup so it only contributes schema when the part is actually available:

```csharp
services.AddScoped<IContentSchemaDefinition, ContactCardPartSchema>();
```

### Defining the reusable part and attaching it to a type

`ContentParts` defines the reusable part definition. `ContentTypes[].ContentTypePartDefinitionRecords` attaches that part to a content type and adds placement or per-type part settings.

```json
{
  "name": "ContentDefinition",
  "ContentParts": [
    {
      "Name": "ContactCardPart",
      "Settings": {
        "ContactCardPartSettings": {
          "ShowPhoneNumber": true,
          "Layout": "Compact"
        }
      },
      "ContentPartFieldDefinitionRecords": []
    }
  ],
  "ContentTypes": [
    {
      "Name": "PersonPage",
      "DisplayName": "Person Page",
      "Settings": {
        "ContentTypeSettings": {
          "Creatable": true,
          "Listable": true,
          "Draftable": true,
          "Versionable": true,
          "Securable": false
        }
      },
      "ContentTypePartDefinitionRecords": [
        {
          "PartName": "ContactCardPart",
          "Name": "ContactCardPart",
          "Settings": {
            "ContentTypePartSettings": {
              "Position": "3"
            },
            "ContactCardPartSettings": {
              "ShowPhoneNumber": true,
              "Layout": "Full"
            }
          }
        }
      ]
    }
  ]
}
```

In that example:

- `ContentParts[].Settings.ContactCardPartSettings` describes the reusable part definition
- `ContentTypePartDefinitionRecords[].Settings.ContentTypePartSettings` controls Orchard placement metadata for that type attachment
- `ContentTypePartDefinitionRecords[].Settings.ContactCardPartSettings` contributes the part-specific settings object validated by `ContactCardPartSchema`

## How schemas stay accurate

Orchard Core recipe steps are implemented by classes inheriting from `NamedRecipeStepHandler`. Each handler converts the incoming JSON into a specific model (for example `ContentStepModel`) and then processes it.

This module mirrors that contract by ensuring each `IRecipeStep` schema includes the known properties that Orchard Core expects (including correct property names and types).
