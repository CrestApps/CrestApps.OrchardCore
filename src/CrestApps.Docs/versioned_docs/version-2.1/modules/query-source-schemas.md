---
sidebar_label: Query Source Schemas
sidebar_position: 6.10
title: Query Source Schemas
description: Describe every query source so the Queries recipe step produces a complete, self-documenting JSON schema.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Recipes |
| **Feature ID** | `CrestApps.OrchardCore.Recipes` |

The `Queries` recipe step creates or updates queries. Each entry of its `Queries` array is polymorphic: a `Source` discriminator selects the query source, and the well known members of the query depend on that value. A SQL query carries a `Template`, a Lucene query carries an `Index` and a `Template`, and each additional source contributes its own members. Without extra metadata, a JSON schema can only say "a query is an object" — it cannot tell you that a `Sql` query needs a `Template`, or that a `Lucene` query needs both an `Index` and a `Template`.

**Query source schema definitions** close that gap. They follow the same extensibility model already used by `IWorkflowActivitySchemaDefinition`, `IRuleConditionSchemaDefinition`, `ISitemapSourceSchemaDefinition`, `IDeploymentStepSchemaDefinition` and `IAdminNodeSchemaDefinition`: each query source contributes its own schema fragment, and the `Queries` recipe step composes all of them into a single, fully described schema.

## Feature gating

The query schema service is registered by the Recipes module only when the `OrchardCore.Queries` feature is enabled. Each built-in source is described only when the feature that owns it is enabled: the `Sql` source when `OrchardCore.Queries.Sql` is enabled, the `Lucene` source when `OrchardCore.Lucene` is enabled, and the `Elasticsearch` source when `OrchardCore.Elasticsearch` is enabled. A source is described when the feature that owns it is enabled.

## What the schema describes

For every entry of the `Queries` array the composed schema declares the shared members every query carries, including:

- **`Name`** — the technical name of the query, unique on the tenant.
- **`Source`** — the query source provider name, for example `Sql`, `Lucene` or `Elasticsearch`. The suggestions list every source available on the tenant. The well known members of the query depend on this value.
- **`Schema`** — the optional return schema used to shape results.
- **`ReturnContentItems`** — whether the query returns full content items.

For every described source the schema narrows the members with a conditional keyed on `Source`, emitting each member the source persists as a real JSON Schema member with its type and a description derived from the source's editor view. Unlike other recipe steps, the source members are flattened onto the query object itself rather than nested under a child object, which matches how Orchard Core serializes queries.

Display text is not emitted into the JSON Schema. It is carried on the `QuerySourceDescriptor` returned by `IQuerySchemaService`, so other features can build their own listings.

### The query stays open

Every query allows additional properties, and the `Queries` array item accepts unknown `Source` values, so a query exported from a tenant with extra modules enabled still validates. Unknown or custom query sources are accepted as long as they supply a `Source`, which is what makes the schema extensible.

## Describe a custom query source

Derive from `QuerySourceSchemaDefinitionBase`, name the source through `Name`, and return the members the source accepts beyond the shared ones:

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;
using Json.Schema;

namespace MyModule;

public sealed class WeatherQuerySourceSchema : QuerySourceSchemaDefinitionBase
{
    public override string Name => "Weather";

    protected override string DisplayText => "Weather";

    protected override string Description => "Runs a query against a weather service.";

    protected override IEnumerable<string> RequiredProperties => ["City"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(QuerySourceSchemaContext context)
    {
        yield return ("City", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The city whose forecast the query returns."));
    }
}
```

Register the definition through the Recipes module's queries feature:

```csharp
services.AddQuerySourceSchema<WeatherQuerySourceSchema>();
```

The `Name` must match the query source name Orchard Core serializes into the `Source` member.
