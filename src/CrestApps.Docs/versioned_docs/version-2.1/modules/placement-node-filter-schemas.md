---
sidebar_label: Placement Node Filter Schemas
sidebar_position: 6.12
title: Placement Node Filter Schemas
description: Describe every placement node filter so the Placements recipe step produces a complete, self-documenting JSON schema.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Recipes |
| **Feature ID** | `CrestApps.OrchardCore.Recipes` |

The `Placements` recipe step updates the display and editor placement rules. Its `Placements` value is a dictionary keyed by shape type, and each value is an array of placement nodes. A placement node carries a `place` location and optional members such as `displayType`, `shape`, `alternates` and `wrappers`, and it can be narrowed by **filters**. A filter is an open, polymorphic member: the `path` filter limits the placement to a request path, `contentType` limits it to a content type, `contentPart` limits it to a content part, and each additional filter provider contributes its own key. Without extra metadata, a JSON schema can only say "a placement node is an object" — it cannot tell you that `path` accepts a string or an array of strings, or that `contentType` matches by prefix.

**Placement node filter schema definitions** close that gap. They follow the same extensibility model already used by `IWorkflowActivitySchemaDefinition`, `IRuleConditionSchemaDefinition`, `ISitemapSourceSchemaDefinition`, `IDeploymentStepSchemaDefinition`, `IAdminNodeSchemaDefinition`, `IQuerySourceSchemaDefinition` and `IRewriteRuleSourceSchemaDefinition`: each filter contributes its own schema fragment, and the `Placements` recipe step composes all of them into the placement node schema.

## Feature gating

The placement schema service and the built-in `path` filter are registered by the Recipes module only when the `OrchardCore.Placements` feature is enabled. The `contentType` and `contentPart` filters are described only when `OrchardCore.Contents` is also enabled, because the content features contribute them, so a filter is described when the feature that owns it is enabled.

## What the schema describes

For every placement node the composed schema declares the shared members every node carries, including:

- **`place`** — the placement location, such as `Content:1`, a zone and position, or `-` to hide the shape. This member is required.
- **`displayType`**, **`differentiator`**, **`shape`**, **`alternates`** and **`wrappers`** — the members shared by every placement node.

For every described filter the schema adds the filter under its key, with the value schema derived from the filter's behavior. The `path`, `contentType` and `contentPart` filters each accept a single string or an array of strings.

Display text is not emitted into the JSON Schema. It is carried on the `PlacementNodeFilterDescriptor` returned by `IPlacementSchemaService`, so other features can build their own listings.

### The placement node stays open

Every placement node allows additional properties, so a node exported from a tenant with extra modules enabled still validates. Unknown or custom filters are accepted, which is what makes the schema extensible.

## Describe a custom placement node filter

Derive from `PlacementNodeFilterSchemaDefinitionBase`, name the filter through `Key`, and return the schema of the value the filter accepts:

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Placements;
using Json.Schema;

namespace MyModule;

public sealed class CulturePlacementNodeFilterSchema : PlacementNodeFilterSchemaDefinitionBase
{
    public override string Key => "culture";

    protected override string DisplayText => "Culture";

    protected override string Description => "Applies the placement only for a matching request culture.";

    protected override JsonSchemaBuilder GetValueSchema(PlacementNodeFilterSchemaContext context)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The culture name that activates the placement, such as en-US.");
}
```

Register the definition through the Recipes module's placements feature:

```csharp
services.AddPlacementNodeFilterSchema<CulturePlacementNodeFilterSchema>();
```

The `Key` must match the `IPlacementNodeFilterProvider.Key` the filter provider exposes.
