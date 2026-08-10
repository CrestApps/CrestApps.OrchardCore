---
sidebar_label: Admin Menu Node Schemas
sidebar_position: 6.9
title: Admin Menu Node Schemas
description: Describe every admin menu node so the AdminMenu recipe step produces a complete, self-documenting JSON schema.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Recipes |
| **Feature ID** | `CrestApps.OrchardCore.Recipes` |

The `AdminMenu` recipe step creates or updates admin menus. Each menu owns a `MenuItems` array of nodes, and every node is polymorphic: a `$type` discriminator selects the node kind and the members it accepts, and nodes nest child nodes through their own `Items` array. Without extra metadata, a JSON schema can only say "a node is an object" — it cannot tell you that a `LinkAdminNode` needs a `LinkText` and `LinkUrl`, or that a `ContentTypesAdminNode` carries a `ContentTypes` list.

**Admin menu node schema definitions** close that gap. They follow the same extensibility model already used by `IWorkflowActivitySchemaDefinition`, `IRuleConditionSchemaDefinition`, `ISitemapSourceSchemaDefinition` and `IDeploymentStepSchemaDefinition`: each admin menu node contributes its own schema fragment, and the `AdminMenu` recipe step composes all of them into a single, fully described schema.

## Feature gating

The admin menu schema services are registered by the Recipes module only when the `OrchardCore.AdminMenu` feature is enabled. The built-in `LinkAdminNode` and `PlaceholderAdminNode` are described whenever admin menus are available. The `ContentTypesAdminNode` is described only when `OrchardCore.Contents` is also enabled, and the `ListsAdminNode` only when `OrchardCore.Lists` is also enabled, so a node is described when the feature that owns it is enabled.

## What the schema describes

For every entry of a menu's `MenuItems` array the composed schema declares the shared members every admin menu node carries, including:

- **`$type`** — the polymorphic node type discriminator, for example `OrchardCore.AdminMenu.AdminNodes.LinkAdminNode, OrchardCore.AdminMenu`. The suggestions list every node available on the tenant. The well known members of the node depend on this value.
- **`UniqueId`**, **`Enabled`**, **`Position`**, **`Priority`**, **`LinkToFirstChild`**, **`LocalNav`**, **`Culture`**, **`Classes`** and **`MenuName`** — the members shared by every node through the underlying menu item.
- **`Items`** — the recursive array of child nodes, described with the same node schema down to a bounded depth.

For every described node the schema narrows the members with a conditional keyed on `$type`, emitting each member the node persists as a real JSON Schema member with its type and a description derived from the node's editor view.

Display text is not emitted into the JSON Schema. It is carried on the `AdminNodeDescriptor` returned by `IAdminMenuSchemaService`, so other features can build their own listings.

### The node stays open

Every node allows additional properties, and the `MenuItems` array item allows unknown `$type` values, so a menu exported from a tenant with extra modules enabled still validates. Unknown or custom admin menu nodes are accepted as long as they supply a `$type`, which is what makes the schema extensible.

## Describe a custom admin menu node

Derive from `AdminNodeSchemaDefinitionBase`, name the node through `Name`, set its `$type` through `TypeDiscriminator`, and return the members the node accepts beyond the shared ones:

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu;
using Json.Schema;

namespace MyModule;

public sealed class WeatherAdminNodeSchema : AdminNodeSchemaDefinitionBase
{
    public override string Name => "WeatherAdminNode";

    public override string TypeDiscriminator => "MyModule.AdminNodes.WeatherAdminNode, MyModule";

    protected override string DisplayText => "Weather";

    protected override string Description => "Adds a weather widget entry to the admin menu.";

    protected override IEnumerable<string> RequiredProperties => ["City"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(AdminNodeSchemaContext context)
    {
        yield return ("City", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The city whose forecast is shown."));
    }
}
```

Register the definition through the Recipes module's admin menu feature:

```csharp
services.AddAdminNodeSchema<WeatherAdminNodeSchema>();
```

The `Name` must match the node type name, and the `TypeDiscriminator` must match the `$type` Orchard Core serializes for the node, which is its fully qualified type name followed by the owning assembly name.
