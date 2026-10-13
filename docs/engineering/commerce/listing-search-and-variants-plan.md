# Listing search, merchandising and variants plan

Status: in progress on `claude/listing-search-variants`, stacked on carts and orders (#835), which is stacked on the
product listing (#834).

## Goal

Make the product listing complete enough to build any store on: search that scales, product variants, every common
field type, better filters and sorts, merchandising, multilingual sites, infinite scroll, and a tidier editor. Every
page part stays a shape, and every piece stays extensible. Stock and availability come later with the e-commerce
modules; SEO stays with Orchard Core's SEO module.

## Decisions (agreed 2026-10-13)

| # | Decision | Why |
| --- | --- | --- |
| V-D1 | **Variants are options on the product**: a product defines its options (Size, Color) and a table of variants, one per combination, each with its own SKU, price, compare-at price, image and an on/off switch. | One content item per product keeps editing, indexing and translation simple; it is the model shoppers and merchants know. |
| V-D2 | **Search providers: Elasticsearch, Azure AI Search and Lucene**, each an optional module on Orchard Core Indexing. The SQL search stays the default. | Orchard Core Indexing lets a module register its own index type and push documents; querying (filter counts, ranges, sorts) is written per provider behind `IProductSearchService`. |
| V-D3 | **Featured and Position live on the Product part** for every product. Listings offer *Featured first* and *Recommended* for any type. The plan-only Position field from #834 is removed; a plan's stored `Sort` still counts when its product part has no position. | One merchandising model for every product. |
| V-D4 | **Paging is chosen per listing**: numbered pages, a *Load more* button, or infinite scroll. Real page links stay underneath. | Works without JavaScript and keeps every page reachable by crawlers. |

## Slices

### V1. Multilingual

- The search record and index carry the item's culture (from `LocalizationPart`, empty when the item is not localized).
- A listing shows the items of the request's culture plus items that are not localized. A translated listing page
  is an ordinary localized content item.
- Taxonomy terms and labels come from the culture's own items.

### V2. Merchandising

- `ProductPart` gains `Featured` and `Position`. Both are indexed as built-in attributes.
- Every listing can offer *Featured first* (featured products, then the default order) and *Recommended* (by
  position, then featured, then newest). `IProductPositionProvider` is replaced by the product part's position; the
  Subscriptions contributor keeps reporting a plan's stored `Sort` as its position when the product part has none.
- A *Featured* toggle filter is available on every listing.

### V3. Fields, filters and sorts

- New field providers: Time, Link (text and URL), HTML and Markdown (searchable text), User picker (user names), and
  date-only versus date-and-time handling (normalized to UTC dates and times).
- Range filters on dates and times (from/to), with ready-made ranges such as *Last 7 days* or by year.
- Text sorts (A to Z, Z to A) on single-value text and taxonomy fields.
- `IProductAttributeValueProvider` stays the seam for any other field.

### V4. Paging modes

- `ProductListingPart.Paging`: Pages, LoadMore or InfiniteScroll.
- The results shape renders page links in every mode; the script appends the next page's cards and moves the link
  for *Load more* and infinite scroll, and keeps the address in step so Back returns to the same spot.

### V5. A tidier listing editor

- Each filter and sort row shows its main settings; the rest open in a details row.
- Rows reorder by dragging, with the order still posted as numbers.
- Sections become collapsible cards with short summaries.

### V6. Search providers

- `ProductSearchIndexer` raises an indexing pipeline (`IProductSearchIndexHandler`): the SQL index is one handler,
  each provider module another, so one product change reaches every enabled index.
- `CrestApps.OrchardCore.Products.Elasticsearch`, `.AzureAI` and `.Lucene`: each registers a *Product search* index
  type with Orchard Core Indexing, writes its mapping, maps `ProductSearchDocument` to `DocumentIndex`, and implements
  `IProductSearchService` with keyword relevance, filters, disjunctive filter counts (aggregations or facets), ranges,
  sorts and paging.
- A listing setting (or site setting) picks the index profile; without one the SQL search is used.

### V7. Variants

- `ProductVariantsPart`: options (name, values) and variants (id, option values, SKU, price, compare-at price, image,
  enabled). Editor generates the combinations.
- Snapshot and price resolution take the variant (`ProductSnapshotContext.VariantId`), so the SKU and price are the
  variant's.
- Indexing: each variant option is an attribute row (a product matches a filter when any enabled variant matches);
  the product's price range is indexed for sorting and price filters.
- Listing: cards show *from* the lowest variant price; the product page shows an option picker that updates the price,
  SKU and image. Add to cart sends the variant; cart lines and order lines carry the variant id and its options.

## Out of scope

Stock and availability, structured data for search engines, customer-specific pricing.
