---
sidebar_label: Deployment Step Schemas
sidebar_position: 6.8
title: Deployment Step Schemas
description: Describe every deployment step so the deployment recipe step produces a complete, self-documenting JSON schema.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Recipes |
| **Feature ID** | `CrestApps.OrchardCore.Recipes` |

The `deployment` recipe step creates or updates deployment plans. Each plan owns a `Steps` array, and every entry has a `Type` discriminator naming a deployment step and a `Step` object holding that step's payload. Without extra metadata, a JSON schema can only say "`Step` is an object" — it cannot tell you that a `ContentDeploymentStep` accepts a `ContentTypes` list and an `ExportAsSetupRecipe` flag, or that a `CustomFileDeploymentStep` needs a `FileName` and `FileContent`.

**Deployment step schema definitions** close that gap. They follow the same extensibility model already used by `IWorkflowActivitySchemaDefinition`, `IRuleConditionSchemaDefinition` and `ISitemapSourceSchemaDefinition`: each deployment step contributes its own schema fragment, and the `deployment` recipe step composes all of them into a single, fully described schema.

## Feature gating

The deployment schema services are registered by the Recipes module only when the `OrchardCore.Deployment` feature is enabled. The composition service enumerates the deployment step factories available on the tenant, so a step is only described when the feature that owns it is also enabled. A step that has no schema definition still appears as a suggestion for `Type`, and its `Step` payload stays open.

## What the schema describes

For every entry of a plan's `Steps` array the composed schema declares:

- **`Type`** — the deployment step type name, which is the step's CLR type name, for example `ContentDeploymentStep`. The suggestions list every step available on the tenant. The well known payload of `Step` depends on this value.
- **`Step`** — the deployment step payload. Its well known members depend on `Type`. Marker steps that export everything of a kind, such as `AllRolesDeploymentStep`, use an empty object.

For every described step the schema narrows `Step` with a conditional keyed on `Type`, emitting each member the step persists as a real JSON Schema member with its type and a description derived from the step's editor view.

Display text is not emitted into the JSON Schema. It is carried on the `DeploymentStepDescriptor` returned by `IDeploymentSchemaService`, so other features can build their own listings.

### The step stays open

Every `Step` payload allows additional properties, and the `Steps` array item allows unknown `Type` values, so a recipe exported from a tenant with extra modules enabled still validates. Unknown or custom deployment steps are accepted as long as they supply a `Type` and a `Step`, which is what makes the schema extensible.

## Describe a custom deployment step

Derive from `DeploymentStepSchemaDefinitionBase`, name the step through `StepType`, and return the members its `Step` payload accepts:

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace MyModule;

public sealed class WeatherDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => "WeatherDeploymentStep";

    protected override string DisplayText => "Weather";

    protected override string Description => "Exports the configured weather locations.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("Cities", new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description("The cities whose forecasts are exported."));
    }
}
```

Register the definition through the Recipes module's deployment feature:

```csharp
services.AddDeploymentStepSchema<WeatherDeploymentStepSchema>();
```

The `StepType` must match the `IDeploymentStepFactory.Name` Orchard Core registers for the step, which is the deployment step's CLR type name.
