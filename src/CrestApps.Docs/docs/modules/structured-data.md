---
sidebar_label: Structured Data
title: Structured Data (schema.org JSON-LD)
description: Publish schema.org structured data (JSON-LD) for the site and its content items, from one engine that pages, search engines and AI agents all read.
user_manual:
  - user-manual/administration/structured-data
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.Seo` |
| **Feature** | Structured Data (`CrestApps.OrchardCore.Seo`) |
| **Category** | Content Management |
| **Depends on** | `OrchardCore.Contents`, `OrchardCore.Liquid` |

The module describes the site and its content items with the [schema.org](https://schema.org) vocabulary, in JSON-LD. It has one engine, `IStructuredDataManager`, with two outputs:

- **Pages.** Every front-end page gets a single `<script type="application/ld+json">` in its head. The script is one `@graph`: the site's publisher (`Organization` or `Person`), the `WebSite`, and the content item the page displays.
- **Agents.** The same engine builds the schema.org object of a content item for an answer. [NLWeb](nlweb.md) returns these objects, so an agent always gets the data the page publishes.

It builds on Orchard Core's SEO module and does not replace it. Orchard Core keeps the meta tags, Open Graph, Twitter cards and `robots.txt`. This module takes over the JSON-LD.

The site settings and the content type settings are described in the User Manual under [Structured Data](../user-manual/administration/structured-data.md).

## One feature, with the rest switching on by itself

The module has one feature. Integrations with other modules are startups marked `[RequireFeatures]`, so they switch on when the other module is enabled:

| Enabled module | What it adds |
| --- | --- |
| Orchard Core **SEO** (`OrchardCore.Seo`) | Uses the SEO part's meta description and social image. Merges the JSON-LD an editor types in the part's **Google Schema** field into the page's graph, and stops Orchard Core from writing it a second time. |
| Orchard Core **Search** (`OrchardCore.Search`) | Adds a `SearchAction` to the `WebSite` node that points at `/search?terms={search_term_string}`. |
| **Products** | Describes products as `Product` nodes with their offer. See [Products](#products). |
| **Reviews** | Adds the `AggregateRating` of a product's approved reviews. |

## How a content item is described

`StructuredDataManager` creates the content item's node, then runs every `IStructuredDataProvider` in ascending `Order`. A provider that runs later overrides a property an earlier one set.

| Order (`StructuredDataProviderOrder`) | Provider | What it sets |
| --- | --- | --- |
| `Defaults` (-1000) | Content item defaults | `name`, `url`, `dateCreated`, `datePublished`, `dateModified`, `inLanguage` (with a `LocalizationPart`), and a `description` from the body (`BodyAspect`) |
| `Parts` (0) | Module providers | Domain data, such as a product's offers |
| `SeoMeta` (500) | Orchard Core SEO | `description` from the meta description, and `image` from the Open Graph or Twitter image |
| `ContentTypeMapping` (1000) | Content type mapping | The Liquid property map of the content type |
| `EditorJsonLd` (2000) | Editor's JSON-LD | The **Google Schema** field of the SEO part |
| 3000 | Site | Publisher and `WebSite` nodes; `publisher` and `isPartOf` on articles and web pages, when they are not set yet |

After the providers run:

- A node without a type becomes a `WebPage`, or a `Thing` when the content item has no URL.
- A node without an `@id` gets `{url}#{type}`.

URLs are absolute. They are built from the site's **Base URL** setting, or from the request when that setting is empty.

### Content type settings

`StructuredDataTypeSettings` is stored on the content type definition, so the `ContentDefinition` recipe step sets it:

```json
{
  "name": "ContentDefinition",
  "ContentTypes": [
    {
      "Name": "BlogPost",
      "Settings": {
        "StructuredDataTypeSettings": {
          "SchemaType": "BlogPosting",
          "Properties": "headline: {{ ContentItem.DisplayText }}\nauthor.name: {{ ContentItem.Content.BlogPost.Author.Text }}"
        }
      }
    }
  ]
}
```

| Setting | What it does |
| --- | --- |
| `Disabled` | When true, the content items of the type publish no structured data. |
| `SchemaType` | The schema.org type. When it is empty, a module provider can choose one through `SuggestType`, otherwise `WebPage` is used. |
| `Properties` | One `name: Liquid` per line. The Liquid gets `ContentItem` (also the model), `Url` and `BaseUrl`. A dotted name such as `offers.price` sets a nested property. A rendered value that is a JSON object or array is stored as JSON. Lines that start with `#` are comments. |

### Editor JSON-LD

When the SEO part's **Google Schema** field holds JSON-LD, the module parses it after processing its shortcodes:

- A node that has no `@id`, and either has the content item's type or is the only node, is merged into the content item's node. The editor's values win.
- Every other node is added to the page's graph.

Invalid JSON is logged and ignored.

`SeoAspectJsonLdHandler` clears `SeoAspect.GoogleSchema` while this module writes the page's JSON-LD, so Orchard Core's SEO driver does not write a second script. It does not clear it when **Add structured data to pages** is off, or when the content type's **Publish no structured data** is on.

## Site settings

`StructuredDataSettings` is a site setting, so the `Settings` recipe step sets it:

```json
{
  "name": "Settings",
  "StructuredDataSettings": {
    "RenderOnPages": true,
    "PublisherType": "Organization",
    "PublisherName": "Contoso",
    "PublisherLogoUrl": "/media/logo.png",
    "SameAs": [ "https://www.linkedin.com/company/contoso" ],
    "IncludeWebSite": true,
    "IncludeSearchAction": true
  }
}
```

Changing the settings needs the **Manage the structured data settings** permission (`ManageStructuredDataSettings`). Administrators have it by default.

## Products

When **Products** is enabled, `ProductStructuredDataProvider` describes a content item with a `ProductPart`:

- **Type and identifiers.** The type is `Product`, unless the content type sets another one. The node gets `sku`, and, from the product feed part, `brand`, `gtin` and `mpn`.
- **One offer.** A product without variants gets an `Offer` with:
  - the price anyone pays: the sale price while a sale is on, otherwise the regular price;
  - `priceCurrency`;
  - `availability`, from the inventory providers (`InStock`, `LimitedAvailability`, `BackOrder`, `PreOrder` or `OutOfStock`);
  - `itemCondition`;
  - `priceValidUntil` when the sale has an end date.
- **Variants.** A product with variants gets an `AggregateOffer` with `lowPrice`, `highPrice` and `offerCount`.
- **No offer** when the product's price is hidden or on request, or when the product has a recurring price.

When **Reviews** is enabled, a product with approved reviews also gets an `aggregateRating`.

## Extension points

| Type | Use it to |
| --- | --- |
| `IStructuredDataProvider` | Describe your own parts or content types. Register it in a startup marked `[RequireFeatures("CrestApps.OrchardCore.Seo")]`, so it switches on with the feature. |
| `ContentPartStructuredDataProvider<TPart>` | A base class for a provider that only runs for content items with a given part. |
| `IStructuredDataManager` | Build the JSON-LD document of a page, or the schema.org object of a content item for an agent (`StructuredDataPurpose.Answer`). |
| `IPageStructuredData` | Add nodes to the current page, such as a `BreadcrumbList` built by a theme, or turn off the page's script (`Suppressed`). |
| `JsonLd`, `JsonLdGraph` | Create, read, merge and safely write JSON-LD nodes. |

```csharp
internal sealed class EventStructuredDataProvider : ContentPartStructuredDataProvider<EventPart>
{
    protected override Task BuildAsync(StructuredDataContext context, EventPart part)
    {
        context.SuggestType("Event");
        context.MainEntity["startDate"] = part.StartUtc.ToString("o");

        if (context.Purpose == StructuredDataPurpose.Page)
        {
            context.Graph.Add(JsonLd.CreateNode("Place", $"{context.BaseUrl}/#venue"));
        }

        return Task.CompletedTask;
    }
}
```

The script is written with the encoder that escapes `<`, `>` and `&`, so values cannot close the script element.
