---
sidebar_label: Advanced SEO
title: Advanced SEO (schema.org JSON-LD)
description: Publish schema.org structured data (JSON-LD) for the site and its content items with the Schema.org part, AI tools and a code API, from one engine that pages, search engines and AI agents all read.
user_manual:
  - user-manual/administration/advanced-seo
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.Seo` |
| **Feature** | Advanced SEO (`CrestApps.OrchardCore.Seo`) |
| **Category** | Content Management |
| **Depends on** | `OrchardCore.Contents`, `OrchardCore.Liquid` |

The module adds to Orchard Core's SEO what search engines and AI agents need to understand the site. Today that is structured data: the site and its content items described with the [schema.org](https://schema.org) vocabulary, in JSON-LD. It has one engine, `IStructuredDataManager`, with two outputs:

- **Pages.** Every front-end page gets a single `<script type="application/ld+json">` in its head. The script is one `@graph`: the site's publisher (`Organization` or `Person`), the `WebSite`, and the content item the page displays or what code said about the page.
- **Agents.** The same engine builds the schema.org object of a content item for an answer. [NLWeb](nlweb.md) returns these objects, so an agent always gets the data the page publishes.

It builds on Orchard Core's SEO module and does not replace it. Orchard Core keeps the meta tags, Open Graph, Twitter cards and `robots.txt`. This module takes over the JSON-LD.

How to set it up is described in the User Manual under [Advanced SEO](../user-manual/administration/advanced-seo.md).

## One feature, with the rest switching on by itself

The module has one feature. Integrations with other modules are startups marked `[RequireFeatures]`, so they switch on when the other module is enabled:

| Enabled module | What it adds |
| --- | --- |
| Orchard Core **SEO** (`OrchardCore.Seo`) | Uses the SEO part's meta description and social image. Merges the JSON-LD an editor types in the part's **Google Schema** field into the page's graph, and stops Orchard Core from writing it a second time. |
| Orchard Core **Search** (`OrchardCore.Search`) | Adds a `SearchAction` to the `WebSite` node that points at `/search?terms={search_term_string}`. |
| **AI** (`CrestApps.OrchardCore.AI`) | Adds the [AI tools](#ai-tools) that configure the structured data. |
| **Products** | Describes products as `Product` nodes with their offer. See [Products](#products). |
| **Reviews** | Adds the `AggregateRating` of a product's approved reviews. |

## Which content items are described

A page describes its content item when:

- the content type has the **Schema.org** part (`SchemaOrgPart`), and the item's **Publish no structured data about this item** is off; or
- the content type has a part a module describes on its own, such as `ProductPart`. Modules add these parts to `StructuredDataOptions.SelfDescribingParts`.

Other content items add nothing about themselves to their page; the page still describes the site. NLWeb answers describe every content item, so an agent always gets at least a name and a URL.

## The Schema.org part

The part's settings on a content type (`SchemaOrgPartSettings`) hold the schema.org type and the property mappings. They are part of the content definition, so the `ContentDefinition` recipe step and deployment plans carry them:

```json
{
  "name": "ContentDefinition",
  "ContentTypes": [
    {
      "Name": "BlogPost",
      "ContentTypePartDefinitionRecords": [
        {
          "PartName": "SchemaOrgPart",
          "Name": "SchemaOrgPart",
          "Settings": {
            "SchemaOrgPartSettings": {
              "SchemaType": "BlogPosting",
              "Properties": [
                { "Name": "headline", "Source": "Content", "Value": "Item:DisplayText" },
                { "Name": "image", "Source": "Content", "Value": "Field:BlogPost.Image" },
                { "Name": "author", "Source": "Content", "Value": "Item:Author" },
                { "Name": "keywords", "Source": "Content", "Value": "Field:BlogPost.Tags" },
                { "Name": "articleSection", "Source": "Fixed", "Value": "News" },
                { "Name": "wordCount", "Source": "Editor" }
              ]
            }
          }
        }
      ]
    }
  ]
}
```

| `Source` | `Value` |
| --- | --- |
| `Content` | A value of the content item: `Item:DisplayText`, `Item:Summary` (start of the body), `Item:Body`, `Item:Url`, `Item:Author`, `Item:PublishedUtc`, `Item:ModifiedUtc`, `Item:CreatedUtc`, `Item:Language`, `Site:Publisher` (a reference to the publisher's node), `Site:Name`, or a field as `Field:{Part}.{Field}`. |
| `Fixed` | The value, the same for every item. |
| `Editor` | None. Editors enter the value on each item, on the part's **Schema.org** tab; it is stored in `SchemaOrgPart.Values`. |
| `Liquid` | A Liquid template. It gets `ContentItem` (also the model), `Url` and `BaseUrl`. |

A dotted name, such as `offers.price`, sets a property of a nested object, which is created with its type (`Offer`) when the vocabulary knows it.

### Values are written in the shape their property expects

`SchemaOrgVocabulary` lists the types the editor offers (Google's rich result types and the common types) and, for each property, its kind. The kind decides how a value is written:

| Kind | Written as |
| --- | --- |
| Text, long text | Text; HTML becomes plain text. A list gives its items joined with commas. |
| Text list | An array; text gives one item per line. |
| URL, image | Absolute URLs. Media fields give the public URL of each file; an image can be several. |
| Date, date and time | ISO 8601. |
| Number, boolean | A JSON number or boolean. |
| Enumeration | The schema.org value, such as `https://schema.org/InStock`. `InStock` and `In stock` are understood too. |
| Object | An object of the property's type: text becomes `{ "@type": "Person", "name": … }`; a content picker gives the items' names and URLs; a reference such as the publisher is kept as it is. |
| JSON | The JSON as it is, such as an FAQ's questions built with Liquid. |

A property or type the vocabulary does not list is written as it is: text, or JSON when the text is a JSON object or array.

Fields are read by their type: `TextField`, `HtmlField`, `MarkdownField`, `NumericField`, `BooleanField`, `DateField`, `DateTimeField`, `TimeField`, `LinkField`, `YoutubeField`, `MediaField`, `ContentPickerField`, `UserPickerField` (user names) and `TaxonomyField` (tag names, or the names of the chosen terms). Other fields give their `Text` or `Value`.

### The editor

The part's settings editor (`schema-org-editor.js`) lists the types by group. Each mapping is a collapsible card styled like a Flow or Bag part item (`widget-editor`), with its property, level and source in the header, move and delete buttons, and an **Add property** menu below the cards. The editor posts the mappings as JSON in a hidden `Properties` input. For the chosen type it lists the properties with their level (required or recommended by Google, or other), and suggests a source for each:

1. the source the vocabulary names for the property, such as `Item:DisplayText` for `headline`;
2. a field named for the property, or with a known other name (`Tags` for `keywords`), whose kind fits;
3. a field of the very kind, for an image or a list;
4. otherwise the Liquid example for a complex value, or the editors.

**Add suggested properties** adds the required and recommended properties not mapped yet, and the optional ones a field is clearly named for. It leaves out what every item already gets (name, URL, description, dates and language) unless a field gives a better value. The AI tools use the same rules, through `SchemaOrgSuggestions`.

## How a node is built

`StructuredDataManager` creates the content item's node, then runs every `IStructuredDataProvider` in ascending `Order`. A provider that runs later overrides a property an earlier one set.

| Order (`StructuredDataProviderOrder`) | Provider | What it sets |
| --- | --- | --- |
| `Defaults` (-1000) | Content item defaults | `name`, `url`, `dateCreated`, `datePublished`, `dateModified`, `inLanguage` (with a `LocalizationPart`), and a `description` from the body (`BodyAspect`) |
| `Parts` (0) | Module providers | Domain data, such as a product's offers |
| `SeoMeta` (500) | Orchard Core SEO | `description` from the meta description, and `image` from the Open Graph or Twitter image |
| `SchemaOrgPart` (1000) | Schema.org part | The part's mappings and the values editors entered |
| `EditorJsonLd` (2000) | Editor's JSON-LD | The **Google Schema** field of the SEO part |
| 3000 | Site | Publisher and `WebSite` nodes; `publisher` and `isPartOf` on articles and web pages, when they are not set yet |

After the providers run:

- What code said about the page (`IPageStructuredData.MainEntity`) is merged into the content item's node.
- A node without a type becomes a `WebPage`, or a `Thing` when it has no URL.
- A node without an `@id` gets `{url}#{type}`.

URLs are absolute. They are built from the site's **Base URL** setting, or from the request when that setting is empty.

The script is built when the theme's `Layout` shape is displayed, after the page's own view has rendered, so views and Liquid templates can still describe the page. It is written with the encoder that escapes `<`, `>` and `&`, so values cannot close the script element.

### Editor JSON-LD

When the SEO part's **Google Schema** field holds JSON-LD, the module parses it after processing its shortcodes:

- A node that has no `@id`, and either has the content item's type or is the only node, is merged into the content item's node. The editor's values win.
- Every other node, and every node of an item that is not described, is added to the page's graph.

Invalid JSON is logged and ignored. `SeoAspectJsonLdHandler` clears `SeoAspect.GoogleSchema` so Orchard Core's SEO driver does not write a second script. It does not clear it when **Add structured data to pages** is off, or when the item's **Publish no structured data about this item** is on.

## Describe a page in code

A page that is not a content item, such as a hard-coded About page or a module's own page, describes itself through `IPageStructuredData`, a scoped service:

```csharp
public sealed class AboutController : Controller
{
    private readonly IPageStructuredData _pageStructuredData;

    public AboutController(IPageStructuredData pageStructuredData)
        => _pageStructuredData = pageStructuredData;

    public IActionResult Index()
    {
        var page = JsonLd.CreateNode("AboutPage");
        page["name"] = "About Contoso";
        page["mainEntity"] = JsonLd.Reference("#founder");

        _pageStructuredData.MainEntity = page;
        _pageStructuredData.Add(new JsonObject
        {
            ["@type"] = "Person",
            ["@id"] = "#founder",
            ["name"] = "Ada Lovelace",
        });

        return View();
    }
}
```

- `MainEntity` describes the page. It gets the page's URL (from its `url`, or the request), the publisher and the web site, like a content item's node. On a page that shows a described content item, its properties are added to that item's node.
- `Add` adds other nodes to the graph, such as a `BreadcrumbList`.
- `Suppressed` turns off the page's script.

Views can do the same without code-behind:

```cshtml
@addTagHelper *, CrestApps.OrchardCore.Seo

<structured-data>
{ "@type": "ContactPage", "name": "Contact us", "telephone": "@Model.Phone" }
</structured-data>
```

```liquid
{% capture page_json_ld %}{ "@type": "ContactPage", "name": "Contact {{ Site.SiteName }}" }{% endcapture %}
{{ page_json_ld | structured_data }}
```

Both read the JSON-LD with `IPageStructuredData.AddJsonLd`: a node without `@id` describes the page (it becomes `MainEntity` or is merged into it), and a node with an `@id` is added to the graph. They write nothing to the page.

To describe pages by their route or another rule, write an `IStructuredDataProvider` that reads the request with `IHttpContextAccessor` and sets `context.MainEntity` when `context.ContentItem` is `null`.

## AI tools

When AI is enabled, the module adds three selectable tools, in the **Search Engine Optimization** category. Add them to an AI profile, such as the one the admin's AI chat uses, to set up structured data by asking for it.

| Tool | What it does | Permission it checks |
| --- | --- | --- |
| `getSchemaOrgOptions` | Without a content type: the content types with their schema.org type, and the types the vocabulary knows. With one: its settings, the values its items can give, a type's properties with their levels, and the suggested mappings. | None (read-only) |
| `configureContentTypeSchemaOrg` | Adds the Schema.org part if needed, sets the type and merges (or replaces) the mappings, then adds the suggested ones unless `addSuggested` is false. Validates property names, sources and Liquid, and returns the required properties still missing. | Edit content types |
| `configureSiteStructuredData` | Reads or changes the publisher, logo, profiles and whether pages describe the web site and its search. | Manage the advanced SEO settings |

Each tool also needs the user's **Access AI tool** permission for it, like every AI tool.

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

Changing the settings needs the **Manage the advanced SEO settings** permission (`ManageAdvancedSeoSettings`). Administrators have it by default.

## Products

When **Products** is enabled, `ProductStructuredDataProvider` describes a content item with a `ProductPart`, with or without the Schema.org part:

- **Type and identifiers.** The type is `Product`, unless the Schema.org part sets another one. The node gets `sku`, and, from the product feed part, `brand`, `gtin` and `mpn`.
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
| `StructuredDataOptions.SelfDescribingParts` | Describe the content items of your part without the Schema.org part. |
| `IStructuredDataManager` | Build the JSON-LD document of a page, or the schema.org object of a content item for an agent (`StructuredDataPurpose.Answer`). |
| `IPageStructuredData` | Describe the current page (`MainEntity`), add nodes (`Add`, `AddJsonLd`), or turn off the page's script (`Suppressed`). |
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

// In the startup marked [RequireFeatures("CrestApps.OrchardCore.Seo")]:
services.AddScoped<IStructuredDataProvider, EventStructuredDataProvider>();
services.Configure<StructuredDataOptions>(options => options.SelfDescribingParts.Add(nameof(EventPart)));
```
