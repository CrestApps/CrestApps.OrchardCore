# CrestApps.OrchardCore.Seo

Publishes schema.org structured data (JSON-LD) for the site and its content items. One engine, `IStructuredDataManager`, writes the JSON-LD script of every front-end page and builds the schema.org object of a content item for AI agents, such as the NLWeb module's answers, so pages and agents always see the same data.

## Feature

One feature, **Structured Data** (`CrestApps.OrchardCore.Seo`). It depends on Contents and Liquid. Integrations switch on by themselves through `[RequireFeatures]` startups:

| Enabled module | What it adds |
| --- | --- |
| Orchard Core SEO | The SEO part's description and social image; merges the part's **Google Schema** JSON-LD into the page's graph instead of writing it twice. |
| Orchard Core Search | A `SearchAction` on the `WebSite` node. |
| Products, Reviews | `Product` nodes with their offers, and the `AggregateRating` of approved reviews (registered by those modules). |

## Configuration

- **Settings > Search > Search Engine Optimization > Structured Data**: the publisher (organization or person), its logo and profiles, the `WebSite` node and the site search action.
- **Content types**: the **Structured data** section sets the schema.org type and a `property: Liquid` map per content type.

## Extending

Implement `IStructuredDataProvider` (or `ContentPartStructuredDataProvider<TPart>`) in your module and register it in a startup marked `[RequireFeatures("CrestApps.OrchardCore.Seo")]`. Contracts live in `CrestApps.OrchardCore.Seo.Abstractions`.

See the [Structured Data](https://orchardcore.crestapps.com/docs/modules/structured-data) documentation.
