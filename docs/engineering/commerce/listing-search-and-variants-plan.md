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

- Each filter row shows use, field, label, kind and style; URL key, Shown after, Visible options, Term level,
  Collapsed and Ranges open under *More settings*, with a one-line summary beside the field name.
- Rows reorder by dragging their handle (Orchard Core's `Sortable` resource) or with the arrow keys on it, announced
  to screen readers. Each row still posts its Order number: a move hands the group's own numbers out again, so rows
  of other groups keep their place between them.
- Filters and sorts fold to a *Choose filters (4 of 9 in use)* button, and open when the form comes back with an error.
- The editor script is its own asset, `product-listing-editor.js`.

### V6. Search providers

- **One record, every index.** The database record (`ProductSearchDocument`) is built once. `ProductSearchIndexer`
  raises `IProductSearchIndexHandler` after it writes or removes records, and after a full rebuild. While Orchard Core
  Indexing is on, the Listing feature adds a handler that writes the same records to every index profile of the
  `ProductSearch` type, through the provider's keyed `IDocumentIndexManager`. A new, reset or rebuilt index is filled
  from the records (`IndexProfileHandlerBase.SynchronizedAsync`). When product types or attributes change, each
  profile is saved again (so a provider that declares fields up front declares the new ones), rebuilt and filled.
- **Shared fields.** `ProductSearchIndexFields` turns a record into fields every provider writes the same way. Fixed
  fields: key, type, culture (`neutral` when not localized), title, title for sorting, SKU, keywords, price,
  featured, position, published date, sequence. Per attribute, under a safe name such as `a_vehicle_year_1a2b3c4d`:
  values (`_v`, for filters), facet tokens (`_f`, `level|value|label|order`, so counts come back with their labels),
  numbers (`_n`, for ranges), smallest and largest number (`_sn`, `_nx`) and a sort text (`_st`). Values longer than
  256 characters are searched but not filtered.
- **Shared query plan.** `ProductSearchIndexQuery` turns a listing query into index-field filters and the same sort as
  the database (ties to featured, then newest, then sequence; empty values last). `FiltersExcept(facetKey)` gives a
  facet its counts without its own selection.
- **Providers**, each an optional module with a keyed `IProductIndexSearchService`:
  - `CrestApps.OrchardCore.Products.Elasticsearch`: explicit mapping plus dynamic templates by suffix; one request
    with a post filter and a filter aggregation per facet; min/max aggregations for ranges.
  - `CrestApps.OrchardCore.Products.Lucene`: no facet support in Orchard Core's Lucene, so counts come from a
    collector over `FieldCache` term ordinals, one pass per facet; bounds from the smallest/largest number fields.
  - `CrestApps.OrchardCore.Products.AzureAI`: fields declared from the attribute catalog; Orchard Core cannot
    declare a list of numbers, so ranges and bounds read each attribute's smallest and largest number (exact for
    single-number attributes); a `_has` companion per sortable field keeps empty values last; one request per facet
    with a selection, top-1 requests for range bounds.
- **Choosing an index.** Each listing picks *Search index* (the database by default). `ProductSearchRouter` answers
  from the index, and from the database when the listing names none, the index or provider is gone, or the server
  fails (logged as a warning).
- **Keyword behavior differs on purpose**: indexes match the start of words and rank by relevance; the database
  matches any part of a word.
- Verified live: Lucene and Elasticsearch give the same products, order, counts and bounds as the database on 15
  listing URLs (sorts, paging, multi-select, ranges, keyword); publish and unpublish reach the index; a deleted
  Elasticsearch index falls back to the database; Orchard Core's Rebuild refills the index. Azure AI Search is covered
  by unit tests of its filters, search text, order and schema only (no service available).

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
