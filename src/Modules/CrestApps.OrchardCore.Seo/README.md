# CrestApps.OrchardCore.Seo

Advanced SEO for Orchard Core. It publishes schema.org structured data (JSON-LD) for the site and its content items. One engine, `IStructuredDataManager`, writes the JSON-LD script of every front-end page and builds the schema.org object of a content item for AI agents, such as the NLWeb module's answers, so pages and agents always see the same data.

## Feature

One feature, **Advanced SEO** (`CrestApps.OrchardCore.Seo`). It depends on Contents and Liquid. Integrations switch on by themselves through `[RequireFeatures]` startups:

| Enabled module | What it adds |
| --- | --- |
| Orchard Core SEO | The SEO part's description and social image; merges the part's **Google Schema** JSON-LD into the page's graph instead of writing it twice. |
| Orchard Core Search | A `SearchAction` on the `WebSite` node. |
| AI | The `getSchemaOrgOptions`, `configureContentTypeSchemaOrg` and `configureSiteStructuredData` tools. |
| Products, Reviews | `Product` nodes with their offers, and the `AggregateRating` of approved reviews (registered by those modules). |

## Configuration

- **Settings > Search > Search Engine Optimization > Advanced SEO**: the publisher (organization or person), its logo and profiles, the `WebSite` node and the site search action.
- **Schema.org part**: add it to a content type to describe its items. Its settings editor lists the schema.org types, and for the chosen one its properties (marked required or recommended by Google) with a suggested source for each: a field, a value every item has, a fixed value, a value editors enter on each item, or Liquid.

## Code

- `IPageStructuredData.MainEntity` and `Add` describe a page that is not a content item, such as a controller's page.
- `<structured-data>` (tag helper) and `structured_data` (Liquid filter) do the same from a view.
- `IStructuredDataProvider` (or `ContentPartStructuredDataProvider<TPart>`) describes your own parts; register it in a startup marked `[RequireFeatures("CrestApps.OrchardCore.Seo")]`. Contracts live in `CrestApps.OrchardCore.Seo.Abstractions`.

See the [Advanced SEO](https://orchardcore.crestapps.com/docs/modules/advanced-seo) documentation.
