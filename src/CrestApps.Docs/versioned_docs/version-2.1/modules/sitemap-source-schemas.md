---
sidebar_label: Sitemap Source Schemas
sidebar_position: 6.7
title: Sitemap Source Schemas
description: Describe every sitemap source so the Sitemaps recipe step produces a complete, self-documenting JSON schema.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Recipes |
| **Feature ID** | `CrestApps.OrchardCore.Recipes` |

The `Sitemaps` recipe step imports sitemaps and sitemap indexes. Each sitemap carries a `SitemapSources` array. A source can add content items by content type, add a single custom URL, or reference other sitemaps to build an index. Every source stores a polymorphic `$type` discriminator, and the sitemap object itself stores a `$type` that distinguishes a standard sitemap from a sitemap index. Without extra metadata, a JSON schema can only say "sources are objects" — it cannot tell you that a `ContentTypesSitemapSource` accepts an `IndexAll` flag and a `ContentTypes` list, or that a `CustomPathSitemapSource` needs a `Path`.

**Sitemap source schema definitions** close that gap. They follow the same extensibility model already used by `IWorkflowActivitySchemaDefinition` and `IRuleConditionSchemaDefinition`: each source contributes its own schema fragment, and the `Sitemaps` recipe step composes all of them into a single, fully described schema.

## Feature gating

The sitemap schema services are registered by the Recipes module only when the `OrchardCore.Sitemaps` feature is enabled. When Sitemaps is enabled, every built-in source is described, and the `Sitemaps` step embeds the composed sitemap schema.

## What the schema describes

For every sitemap the composed schema declares:

- **`$type`** — the polymorphic sitemap type discriminator, either `OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions` for a standard sitemap or `OrchardCore.Sitemaps.Models.SitemapIndex, OrchardCore.Sitemaps` for a sitemap index. Required, because the `Sitemaps` step deserializes each entry as a polymorphic `SitemapType`.
- **`SitemapId`** — a stable identifier. Generated when the recipe runs if omitted.
- **`Name`**, **`Enabled`**, **`Path`** — the sitemap name, whether it participates in routing, and the public path it is served from.
- **`SitemapSources`** — the array of sources, each described with its own members.

For every registered source the schema declares:

- **`$type`** — the polymorphic source type discriminator, for example `OrchardCore.Sitemaps.Models.ContentTypesSitemapSource, OrchardCore.Sitemaps.Abstractions`. Required.
- **`Id`** — a stable identifier. Generated when the recipe runs if omitted.
- **Per-source members** — every member the source persists, each emitted as a real JSON Schema member with its type and a description derived from the source's editor view.

Display text is not emitted into the JSON Schema. It is carried on the `SitemapSourceDescriptor` returned by `ISitemapSchemaService`, so other features can build their own listings.

### The source stays open

Every sitemap and every source object allows additional properties, so a recipe exported from a tenant with extra modules enabled still validates. Unknown or custom sources are accepted as long as they supply a `$type`, which is what makes the schema extensible.

## Built-in sources

When `OrchardCore.Sitemaps` is enabled the following are described out of the box.

| Source | Purpose |
| --- | --- |
| `ContentTypesSitemapSource` | Adds content items to the sitemap, either every indexable content type or a selected list. |
| `CustomPathSitemapSource` | Adds a single custom URL to the sitemap. |
| `SitemapIndexSource` | References other sitemaps by their identifier to build a sitemap index. |

## Describe a custom source

Register a sitemap source schema definition to describe a custom source contributed by your module. Derive from `SitemapSourceSchemaDefinitionBase`, which assembles the shared `$type` and `Id` members for you.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;
using Json.Schema;

namespace MyModule;

public sealed class WeatherSitemapSourceSchema : SitemapSourceSchemaDefinitionBase
{
    public override string Name { get; } = "WeatherSitemapSource";

    public override string TypeDiscriminator { get; } = "MyModule.Models.WeatherSitemapSource, MyModule";

    protected override string DisplayText => "Weather";

    protected override string Description => "Adds a weather forecast page to the sitemap.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(SitemapSourceSchemaContext context)
    {
        yield return ("City", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The city the forecast page covers."));
    }
}
```

Register the definition behind the feature that owns the source:

```csharp
services.AddSitemapSourceSchema<WeatherSitemapSourceSchema>();
```

The `TypeDiscriminator` must match the `$type` Orchard Core serializes for the source, which is the fully qualified type name followed by the short assembly name.
