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

The schema for this step intentionally mirrors the regular `AIProfile` step for profile fields such as `Name`, `DisplayText`, `Description`, `Type`, `PromptTemplate`, `PromptSubject`, `OrchestratorName`, `ChatDeploymentName`, `UtilityDeploymentName`, `CreatedUtc`, `OwnerId`, `Author`, `Properties`, and `Settings`, while adding the required `TemplateId` selector that must resolve to a Profile template.

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

Use `PartSchemaDefinitionBase` when the schema belongs to a **content part**. It already marks the definition as `ContentDefinitionSchemaType.Part`, caches the part settings schema, and lets the implementation focus on the part-specific fragments.

Use `FieldSchemaDefinitionBase` when the schema belongs to a **content field**. It marks the definition as `ContentDefinitionSchemaType.Field`, caches the field settings schema, and lets the implementation describe the field-specific fragments.

Implement `IContentSchemaDefinition` directly only when the contribution does **not** fit either base class.

### Why these types exist

The `ContentDefinition` and `ReplaceContentDefinition` recipe steps have two nested settings surfaces:

- `ContentTypePartDefinitionRecords[].Settings` for settings that apply when a part is attached to a content type
- `ContentParts[].Settings` for the reusable content part definition itself

The Recipes module gathers all registered `IContentSchemaDefinition` services and merges their schema fragments into those nested `Settings` objects only when the owning feature is enabled. Definitions are grouped by `Name`, so if multiple contributors target the same part or field type, their schema fragments are combined into the final schema for that single Orchard name instead of replacing each other. That keeps validation aligned with the actual Orchard Core features available to the tenant.

The separate `IContentPartSchemaDefinition` and `IContentFieldSchemaDefinition` interfaces are still used by the `content` recipe step. They now receive `ContentPartSchemaContext` and `ContentFieldSchemaContext`, so payload contributors can inspect the concrete `ContentTypePartDefinition` and `ContentPartFieldDefinition` for the exact definition instance being rendered. `IContentSchemaDefinition` remains the shared contract for content-definition settings schemas.

In this repository, feature-gated contributors now cover custom field payloads such as `PhoneField` and custom part payloads/settings such as `AIProfilePart`, `RolePickerPart`, `UserFullNamePart`, `OmnichannelContactPart`, and `RolePickerPartContentAccessControlSettings`.

If a part only contains content fields, do not add a dedicated part payload contributor just to restate those field shapes. The content-item schema already composes each attached field through its registered field schema definitions, so dedicated part contributors are only needed when the part also exposes non-field C# properties or other part-level payload that fields alone cannot describe.

### Implementing a part schema

This example adds schema support for a fictional `ContactCardPart`, including both the part settings envelope used by `ContentDefinition` and the content item payload used by the `Content` recipe step:

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
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

    protected override JsonSchemaBuilder BuildPartSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Heading", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("PhoneNumber", new JsonSchemaBuilder().Type(SchemaValueType.String)))
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

The same schema definition also shapes the `Content` recipe step payload for the part itself:

```json
{
  "name": "Content",
  "data": [
    {
      "ContentType": "PersonPage",
      "DisplayText": "Jane Doe",
      "Published": true,
      "Latest": true,
      "ContactCardPart": {
        "Heading": "Primary contact",
        "PhoneNumber": "+1-555-0100"
      }
    }
  ]
}
```

In that payload, `ContactCardPart.Heading` and `ContactCardPart.PhoneNumber` come from `BuildPartSchemaCore()`, while `ContactCardPartSettings` still belongs only to the `ContentDefinition` and `ReplaceContentDefinition` recipe steps.

For simple parts, the context can be ignored.

For container-style parts such as `BagPart` or `FlowPart`, implement the optional `IContainedContentPartSchemaDefinition` interface alongside `PartSchemaDefinitionBase`. That secondary interface lets the contributor declare which payload property contains nested items and which content types are allowed for the current attachment, while `ContentItemSchemaService` stays responsible for recursive schema composition and cycle protection.

### Implementing a field schema

Use `FieldSchemaDefinitionBase` when you need both:

- a field settings envelope under `ContentPartFieldDefinitionRecords[].Settings`
- a field value schema for the field payload inside a content item

This example adds schema support for a fictional `SocialLinkField`:

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using Json.Schema;

namespace MyModule.Schemas;

public sealed class SocialLinkFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "SocialLinkField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("SocialLinkFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("AllowedHost", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("OpenInNewTab", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Text", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Url", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
    }
}
```

Register it the same way:

```csharp
services.AddScoped<IContentSchemaDefinition, SocialLinkFieldSchema>();
```

Then the field settings and content payload can be expressed like this:

```json
{
  "name": "ContentDefinition",
  "ContentParts": [
    {
      "Name": "ProfilePart",
      "ContentPartFieldDefinitionRecords": [
        {
          "Name": "SupportLink",
          "DisplayName": "Support link",
          "ContentFieldDefinition": {
            "Name": "SocialLinkField"
          },
          "Settings": {
            "SocialLinkFieldSettings": {
              "AllowedHost": "example.com",
              "OpenInNewTab": true
            }
          }
        }
      ]
    }
  ]
}
```

```json
{
  "name": "Content",
  "data": [
    {
      "ContentType": "PersonPage",
      "ProfilePart": {
        "SupportLink": {
          "Text": "Contact support",
          "Url": "https://example.com/support"
        }
      }
    }
  ]
}
```

## How schemas stay accurate

Orchard Core recipe steps are implemented by classes inheriting from `NamedRecipeStepHandler`. Each handler converts the incoming JSON into a specific model (for example `ContentStepModel`) and then processes it.

This module mirrors that contract by ensuring each `IRecipeStep` schema includes the known properties that Orchard Core expects (including correct property names and types).
