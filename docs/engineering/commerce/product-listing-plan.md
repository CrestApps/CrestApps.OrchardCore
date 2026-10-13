# Product listing plan

Status: **planning**. No code yet. Branch `claude/ecommerce-product-listing-38699d`, based on `main` after
PR #6 (Subscriptions and the commerce foundation) merged on 2026-10-12.

This plan covers the next commerce step: listing products on the front end so visitors can browse, filter,
sort and open them, whether or not the products can be bought online. It also records where the commerce
suite stands after PR #6, and why this step comes before Customers, Carts and Orders.

## 1. Where commerce stands after PR #6

PR #6 merged the whole `ma/subscribtions` line: about 1,300 files across 38 projects. In summary:

| Area | State | Notes |
| --- | --- | --- |
| Payments, Checkout engine | **Done** | `ICheckoutEngine` is the only code that moves money. It writes a durable `PaymentAttempt` before calling a provider, completes idempotently through the browser poll, webhooks and a reconciliation sweep, and compensates through refunds. |
| Public checkout UI | **Done** | `/Checkout/{sessionId}/{step}`, with provider panels for Stripe (card, plus Apple Pay and Google Pay buttons that are not yet live-verified) and Pay Later. A hashed guest ownership token replaced the IP and user-agent check. |
| Refunds, payments admin | **Done** | Durable `PaymentRefund`, remote refund reconciliation, a `ManageRefunds` permission, and admin screens for payments and refunds. |
| Recurring payments | **Done** | Recurring provider contract. Stripe and Pay Later adapters; the Pay Later renewal sweep. |
| Subscriptions | **Done** | Durable `Subscription` lifecycle, the **My Plans** portal, admin agreements (cancel, pause, resume), workflow events, entitlements (roles), trials, and tenant onboarding. Subscriptions now run on the checkout engine; the legacy flow is gone. |
| Installment plans | **Done** | Admin-created payment plans. Every installment is a `Transaction`; saved-card charges go through the engine. |
| Transactions | **Done** | Ledger of outstanding obligations, settled online through checkout. Invoice and receipt numbers (`INV-`, `R-`) come from `SequentialFinancialDocumentNumberGenerator`. |
| Coupons | **Done (checkout-level)** | `Coupon`, `ICheckoutDiscountService`, coupon admin. Campaigns, automatic discounts and product or category rules are still open (Promotions). |
| Taxation, tax tables | **Done** | Full engine, effective-dated tables, recipes and deployments. |
| Addresses | **Done** | Geographic hierarchy, resolver, immutable `Address` snapshot. |
| Receipts, Reports | **Done** | Reusable on-demand receipts; report framework with CSV and OpenXml export. |
| Products | **Partial** | `ProductPart` holds one price, a currency and a SKU. `ProductPricePart` offers several prices: one-time or recurring, effective windows, custom amounts, quantity. **No front-end display at all**: Products ships editors only, no `ProductPart` display shape, listing, filters or detail view. Price is required, so a product cannot be "listed but not sold". |
| Customers | **Contracts only** | `CustomerOwner` and `ICustomerContactResolver` exist. There is no profile, address book, guest merge or customer module. |
| Product listing, storefront browse | **Not started** | This plan. |
| Carts, Orders | **Not started** | |
| Inventory, Shipping, Promotions (beyond coupons), Digital/Service fulfillment, Reviews | **Not started** | |

### Why Orders is still needed alongside Transactions

A `Transaction` records **money owed**: who owes it, the amount, tax, how much is paid and refunded, the
due date, the invoice number, and a `ReferenceType`/`ReferenceId` pointing at whatever it is owed *for*. It
has no line items, quantities, product snapshots, addresses, shipping method or fulfillment state.

An Order records **what was bought**: the lines with frozen product and price snapshots, quantities, per-line
discounts and tax, billing and shipping addresses, the shipping method, and fulfillment and return state.
The order is what a `Transaction` references, the same way an `InstallmentPlan` or a `Subscription` is
today:

- A cart of three items paid later is one order and one transaction. Shipping two items and refunding the
  third changes the order. The transaction only records the refunded amount.
- A large order on a payment plan is one order and several transactions.
- A `CheckoutSession` carries invoice lines too, but it expires and only orchestrates payment. It is not
  where fulfillment or order history lives.

So Orders stays in the roadmap as a thin record of lines, addresses and fulfillment, with all money in
Checkout and Transactions, as the roadmap already requires. It is needed only for cart-based selling.
Listing-only sites, subscriptions and installment plans never need it.

[`docs/ecommerce-plan.md`](../../ecommerce-plan.md) holds the long-range roadmap. Its inventory table was
written before the checkout engine, the checkout UI, coupons and the guest token landed. This branch
refreshes that table.

## 2. Why product listing comes next

The roadmap orders the work as Customers → catalog completion → Carts → Orders → … → Storefront (Phase 10).
That order makes sense for *buying*. It is the wrong order for *showing*:

- A large class of sites only lists items: real estate, car dealerships, equipment rental, and showrooms
  that take orders by phone. These sites need browse, filter, sort and a detail page. They need no cart,
  no order and no customer record.
- A store that does sell online needs the same browse experience first. Carts and checkout start from a
  product page, so the product page and the listing have to exist before them.
- Browsing reads published content only. It writes no financial records, so building it now cannot
  conflict with the Orders or Carts designs. Its one contract with selling is **purchasability**
  (section 4.1). Checkout must enforce that contract on the server.

So this plan pulls the browse part of roadmap Phase 10, plus a small slice of Phase 2 (catalog
attributes), ahead of Phase 1. Customers, Carts and Orders keep their place after it.

## 3. Goals and non-goals

**Goals**

1. Any content type carrying `ProductPart` can be listed on the front end, whether or not it can be bought.
2. Site builders define what visitors can filter and sort by without writing code. Filters use the
   product type's own fields and Orchard taxonomies.
3. Filtering works like the big marketplaces: options with counts, several options in one filter, several
   filters at once, ranges, category drill-down, dependent filters (Model under Make), and active-filter
   chips.
4. Several listing pages can exist side by side (*Cars for sale*, *Homes for rent*), each with its own
   types, base filter, facets, sorts and views.
5. Grid and list views out of the box, rendered with **Bootstrap 5.3 markup only** so OrchardCore's
   **TheTheme** renders them unchanged. Every piece is a shape that a theme can override.
6. URLs are shareable and bookmarkable. The page works without JavaScript, and JavaScript makes it faster.
7. The search service has a provider seam, so a SQL default works on every database Orchard supports and
   an Elasticsearch provider can be added later for large catalogs.

**Non-goals for this plan:** carts, checkout from a listing, variants (Phase 2), reviews and ratings,
inventory counts, map view, saved searches and alerts, product comparison. Section 10 lists them as later
slices.

## 4. Design

### 4.1 Listed is not the same as purchasable

Today `ProductPart` means "this can be sold", and its editor requires a price. A listing site needs:

| Need | Example |
| --- | --- |
| Show the item without selling it | A house for sale, a car on the lot |
| Show a price, hide it, or replace it with text | "$349,000", "Call for price", nothing |
| Sell some items and only list others of the same type | A dealer that takes online deposits on some cars |

**Proposed changes (Products.Core).** The decisions live on `ProductPartSettings`, set once per content
type when the Product part is attached. The item carries a value only when the type allows it:

| `ProductPartSettings` (type level) | Values | Item-level counterpart on `ProductPart` |
| --- | --- | --- |
| `SalesMode` | `Purchasable` (the default, matching today's behavior), `ListingOnly`, `PerItem` | `IsPurchasable`, shown and honored only when the mode is `PerItem` |
| `PriceDisplay` | `Show` (the default), `OnRequest`, `Hidden` | `PriceDisplay`, shown only when `AllowPriceDisplayOverride` is set |
| `OnRequestLabel` | Text such as "Call for price" | none |
| `AllowPriceDisplayOverride` | Off by default | none |

A real estate *Property* type is `ListingOnly` with `Show`, and no item-level control appears in its
editor. A dealer's *Vehicle* type is `PerItem`, so editors can tick **Sell online** on the cars that take
deposits.

- `ProductPart.CompareAtPrice` (optional): the "was" or list price behind the strike-through and the
  "-10%" badge seen on large marketplaces.
- The editor requires a price only when the item is purchasable or its price is shown.
- **Server-side enforcement.** `IProductSnapshotResolver` returns no sellable snapshot for an item that is
  not purchasable. Every checkout path goes through that resolver, so a crafted request cannot buy a
  listing-only item. A regression test locks this in.
- **Migration.** Existing types keep `Purchasable` and `Show`, so subscription plans and current recipes
  do not change. `ProductPartSchemaDefinition` gains the new properties.

### 4.2 Product types are content types; attributes are fields

Large marketplaces keep two separate things:

| Marketplace | Category structure | Product attributes | Filters come from |
| --- | --- | --- | --- |
| Amazon | Browse-node tree | A product-type definition per type (Brand, Size, Color…) | Per-node "refinements" over attributes, with counts |
| eBay | Leaf categories | "Item specifics" (aspects) per category: required, recommended, allowed values | Aspect filters per category |
| Shopify | Collections | Options (variants) and metafields | Filters configured over options and metafields |
| WooCommerce | Product categories | Global attributes (`pa_color`), stored as taxonomies | Layered navigation over attributes |

Orchard already has both halves:

- **A content type is a product type.** *Vehicle* has Year (`NumericField`), Mileage (`NumericField`),
  Transmission (`TextField` with a predefined list), Make/Model (`TaxonomyField`) and Photos
  (`MediaField`). *Property* has Bedrooms, Bathrooms, Square feet, Property type and Listing status. This
  matches Amazon's product-type definitions and eBay's item specifics, and editors get typed fields,
  validation, localization and versioning for free.
- **Taxonomies are the category tree and the controlled vocabularies.** Categories are hierarchical. Make
  → Model is a two-level taxonomy, which gives dependent filters for free. Colors, Fuel type and Condition
  are flat vocabularies. A term can carry extra fields, such as a swatch color or an icon.

**Recommendation:** use fields and taxonomies. Do not invent a separate attribute store. Section 9 lists
the alternative (a generic attribute bag per category, the eBay model) as an open decision.

### 4.3 Product attributes: marking which fields matter

Not every field should be filterable. A new **field setting**, added through a
`ContentPartFieldDefinitionDisplayDriver` so it works for any field type, marks a field as a catalog
attribute:

| Setting | Effect |
| --- | --- |
| Filterable | The field can be used as a facet on a listing page. |
| Sortable | The field can be offered as a sort option (numeric and date fields only). |
| Searchable | The field's text is included in keyword search. |
| Show on card | Shown as a spec chip on the grid card, like the *AWD · White · 235,000 mi* chips on a dealer site. |
| Show in specifications | Shown in the specification table on the list view and the detail page. |
| Label, group, order | How the attribute is labeled and grouped in the specification table. |

**Supported field types.** One `IProductAttributeValueProvider` per field type turns a field into
attribute values:

| Field | Values | Facet kinds |
| --- | --- | --- |
| `TextField` | The text. Labels and order come from the predefined-list options when that editor is used. | Checkbox list, radio, dropdown |
| `MultiTextField` | Each value | Checkbox list |
| `NumericField` | The number | Range (min/max), buckets |
| `BooleanField` | true / false | Toggle |
| `DateField`, `DateTimeField` | The date | Range, or year buckets |
| `TaxonomyField` | Term ids **plus every ancestor term id**, with the term label and slug | Checkbox list, hierarchical drill-down, level-based (Make, then Model), swatch |
| `ContentPickerField` | Linked item ids and their display text | Checkbox list (for example Agent or Dealer location) |

**Built-in attributes** need no field: content type, price (from `ProductPart`), product type,
purchasable, SKU (keyword search), published date ("Newest", "Days in stock") and title.

### 4.4 The search document: one search document per listed item

Querying a dozen generic Orchard field-index tables per request with dynamic joins does not scale and
cannot produce facet counts cheaply. Instead, keep a denormalized **search document** per published listed
item:

```text
ProductSearchDocument (own YesSql collection)
  ContentItemId, ContentItemVersionId, ContentType, Culture
  Title, Sku, Price, CompareAtPrice, Currency, PriceDisplay, IsPurchasable, ProductType
  PublishedUtc, CreatedUtc, SearchText
  Attributes: [ { Key, Text, Number, Date, Label, Slug, Order } ]   // VariantId reserved for Phase 2
```

Two indexes over it:

- `ProductSearchIndex` has one row per entry: type, culture, price, purchasable, dates, title and SKU.
- `ProductAttributeIndex` has one row per attribute value: `ContentItemId`, `ContentType`, `Key`,
  `TextValue`, `NumberValue`, `DateValue`. Its composite indexes are `(Key, TextValue, ContentItemId)`
  and `(Key, NumberValue, ContentItemId)`.

**Keeping entries current.** A content handler rebuilds the entry on `Published` and removes it on
`Unpublished` and `Removed`. It always reads the published version, never a draft. Entries are built by
`IProductSearchContributor`s: Products contributes price and purchasability, the field providers contribute
attributes, and a later Variants feature contributes variant rows.

**Rebuilds.** A full rebuild is needed when product-attribute settings change on a type, when a taxonomy
term is renamed or moved (labels and slugs are denormalized), and on demand from admin. A rebuild runs as a
background task in chunks, with a fresh scope per chunk. It must never run inside a migration, because on
SQLite a second connection inside a migration deadlocks. A rebuild also must not reload a document after
`SaveChangesAsync` in the middle of a batch.

The same entry is the document an Elasticsearch provider would index later. Both providers read one shape.

### 4.5 The search service

```csharp
public interface IProductSearchService
{
    Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
}
```

- `ProductSearchQuery` contains the catalog definition (types, base filters, facets, sorts), the visitor's
  selections, keyword, sort, page, page size and culture.
- `ProductSearchResult` contains the ordered content item ids, the total, and per-facet value lists with
  counts and selected state.
- **SQL provider (default).** YesSql maintains the documents and indexes. Filtering and facet counting use
  parameterized, dialect-aware SQL over the two index tables, honoring the tenant table prefix. Values are
  never concatenated into SQL. The provider is aimed at catalogs up to tens of thousands of items.
- **Elasticsearch provider (later).** Uses aggregations for counts, plus relevance scoring and typo
  tolerance for keyword search, for large catalogs.

**Facet semantics** (the behavior visitors expect from large marketplaces):

- Options within one facet combine with **OR** (Toyota *or* Honda). Different facets combine with
  **AND** (Toyota or Honda, *and* 2018 or newer).
- **Disjunctive counts.** A facet's counts are computed with every *other* facet applied but not itself.
  Picking Toyota then still shows how many Hondas there are, so the visitor can add one.
- Zero-count options are hidden, except options that are already selected.
- A hierarchical facet shows the children of the selected term (Amazon's department drill-down). Selecting
  a parent matches all descendants, because entries store ancestor term ids.
- A dependent facet (Model) stays hidden until its parent facet (Make) has a selection, and then offers
  only that parent's children.
- Range facets accept min and max (invariant culture) or predefined buckets ("Under $10,000",
  "$10,000–$20,000").

**Sorting.** Each catalog offers a configured list of sorts, one of them the default: Featured (a manual
order field), Newest, Price low to high, Price high to low, Title A–Z, and any attribute marked Sortable
in either direction (Year, Mileage). Empty values sort last. Keyword searches add Relevance once a provider
supports it.

**Visibility.** Only published entries appear, and only for content types the visitor may view
(`ViewContent` for that type, checked once per type, not per item). When a type is localized, entries are
filtered by the current culture.

### 4.6 Listing pages

A **Product Listing** content type (`TitlePart`, `AutoroutePart`, `ProductListingPart`, optional `HtmlBodyPart` for an
introduction) is created by migration and by recipes. A site can have as many as it needs, add them to
menus, and give each its own URL. This follows how Orchard does Blog and `ListPart`.

`ProductListingPart` settings:

| Setting | Example |
| --- | --- |
| Content types | *Vehicle* (any type with `ProductPart`) |
| Base filters | Category = Passenger Vehicles; Listing status ≠ Sold |
| Facets (ordered) | Attribute, label, URL key, kind (checkbox, radio, dropdown, range, buckets, toggle, swatch, hierarchical), depends on, collapsed by default, visible values before "See more", value order (count, label, taxonomy order, numeric), show counts |
| Sorts (ordered) | Newest (default), Price ↑, Price ↓, Year ↓, Mileage ↑ |
| Views | Grid (default), List |
| Page sizes | 12, 24 (default), 48 |
| Keyword search | On or off, with placeholder text |
| Card image | Which `MediaField` supplies the card image (default: the first image field) |

The facet builder in the editor offers only attributes marked Filterable on the selected types. Rows are
reordered with the drag-and-drop library already shipped in the Orchard admin, without jQuery.

Taxonomy term pages (`/categories/suvs`) can later render the same catalog shape with a term pre-applied
(slice C6). The first release uses a category facet or a listing page per category.

### 4.7 URL contract

```text
/vehicles?q=diesel&make=<term-id>&make=<term-id>&year_min=2016&year_max=2020&price_max=20000&sort=price-asc&view=list&page=2&page-size=24
```

- Facet keys are short slugs chosen in the listing editor (`make`, `year`), not internal ids.
- A facet with several values repeats its key, which is exactly what a GET form produces, so the page works
  without JavaScript. Comma-separated values are accepted too.
- Range facets use `{key}_min` and `{key}_max`, read with the invariant culture. Either end is optional.
- Text values are readable as they are. Taxonomy terms currently use their content item id. Readable term
  slugs (D6) are a follow-up.
- Unknown keys are ignored. Values per facet are capped at 50. Page size must be one of the configured
  sizes, and page numbers are bounded.
- **SEO.** Uses Orchard Core's SEO module (`OrchardCore.Seo`) rather than tags of our own. The SEO part on
  the Product Listing type sets the canonical URL to the listing itself for every state. A content handler,
  active only when the SEO feature is enabled, contributes `noindex, follow` to the `SeoAspect` when the
  address carries a filter or a keyword. Paging, sorting, page size and layout stay indexable. Detail pages
  use the item's Autoroute URL and its own SEO part.

### 4.8 Front-end rendering (Bootstrap 5.3, TheTheme-ready)

Everything is a shape with alternates. A theme overrides any piece without forking the module.

| Shape | Renders | Bootstrap 5.3 pieces |
| --- | --- | --- |
| `ProductListing` | Page layout: filters column, toolbar, results, pager | `row`, `col-lg-3` / `col-lg-9` |
| `ProductListingFilters` | The facet form | `offcanvas-lg offcanvas-start`: a sidebar on large screens and a drawer behind a "Filters (3)" button on small ones; `accordion` per facet group |
| `ProductFacet__{Kind}` | One facet | `form-check`, `form-select`, `input-group` for min/max, `collapse` for "See more", swatches as `btn-check` with a background color |
| `ProductListingToolbar` | Result count, sort, page size, view switcher | `form-select`, `btn-group` with `btn-check` radios (visually hidden labels) |
| `ProductListingActiveFilters` | Removable chips and "Clear all" | `btn btn-sm btn-outline-secondary` with a close icon |
| `ProductListingResults__Grid` | Cards | `row row-cols-1 row-cols-sm-2 row-cols-lg-3 row-cols-xxl-4 g-3`, `card h-100` |
| `ProductListingResults__List` | Horizontal cards with a spec table | `card` with `row g-0`, `table table-sm` or `dl row` |
| `ProductPrice` | Price, compare-at, discount badge, or "Call for price" | `badge text-bg-danger`, `text-decoration-line-through` |
| `ProductSpecifications` | Spec table on list rows and detail pages | `dl row` / `table` |

**Per-type cards.** Results render each item with the content display manager in the `Summary` display
type (grid) and a `SummaryList` display type (list). A dealer can therefore ship
`Content-Vehicle.Summary.cshtml` and change the card without touching the module. The default card shows
the image, the title linked to the detail page, `ProductPrice`, spec chips and a **Details** button.

**Without JavaScript**, the filter form is a GET form with an **Apply** button, and every link is a real
URL. **With JavaScript** (a small vanilla module, no jQuery):

- the form auto-submits on change;
- results and facets are fetched as a partial and swapped in;
- the URL is updated with `history.pushState`;
- focus is kept and an `aria-live` region announces "23 results".

Back and forward navigation restores state from the URL.

### 4.9 Detail page

The detail page is the content type's own `Detail` display, so each type controls its layout. This plan
adds parts to it:

- `ProductPart` gets a display shape: the price block from `ProductPrice` plus an action zone.
- `ProductSpecifications` shows the attributes marked "Show in specifications", grouped.
- Gallery: the existing `MediaField` display. A Bootstrap carousel alternate ships with the module.
- **Action zone (`ProductActions`).** For a listing-only item this is a configurable inquiry button, such
  as a link to a contact form with the item id. Selling sites later fill the same zone with **Add to cart**
  from the Carts module, so nothing on the detail page has to change when selling arrives. Turning an
  inquiry into a CRM lead (the Omnichannel `LeadPart` exists) is a follow-up, not part of this plan.
- A breadcrumb (Listing › Category › Item), following the admin breadcrumb convention on the front end.

### 4.10 Admin, recipes and deployment

- Field-level product-attribute settings (4.3) and the `ProductListingPart` editor (4.6).
- **Commerce → Product index**: entry counts per type, the last rebuild time, and a **Rebuild** action.
- Recipe schema definitions for `ProductListingPart` and the field settings, as the repository requires for every
  new part. Content definitions and catalog items deploy through the standard content steps.
- **Starter recipes** that double as docs demos:
  - **Vehicle dealership**: a *Vehicle* type, Make → Model, Body style, Fuel, Drivetrain, Colors and
    Condition taxonomies, and a *Vehicles* catalog.
  - **Real estate**: a *Property* type, Property type, Listing status and Area taxonomies, beds/baths/sq ft
    ranges, and *Homes for sale* and *Homes for rent* catalogs.
  - **Apparel**: a purchasable *Shirt* type with Size and Color swatches, so filtering is shown on items
    that can be bought.

### 4.11 Performance and caching

- The SQL provider issues one query for the page of ids, one for the total, and one per facet for counts.
  They are batched into one round trip where the dialect allows.
- Facet results are cached per catalog and normalized query for a short time. The cache is invalidated by
  a signal raised whenever any search document changes. Anonymous listing pages can use Orchard's dynamic
  cache with a per-catalog tag.
- Cards load their content items in one batch (`GetAsync(ids)`), not one query per item.

### 4.12 Security

- Only published content is listed, and only for types the visitor may view. Hidden prices are never
  written to markup, partial responses or a future JSON API.
- All query-string input is bound to the catalog's own configuration: unknown facets are ignored, values
  are parsed with the invariant culture and capped, and every SQL value is a parameter.
- The partial-results endpoint runs the same authorization as the page. It can be rate limited through
  Orchard's rate-limit feature if abused.
- Purchasability is enforced in the snapshot resolver (4.1), not in the UI.

## 5. Project layout

Listing lives in the **Products** module. Products already has no dependency on Checkout, Payments or any
provider: its only commerce reference is the shared Commerce admin-menu shell, and its Taxation bridge is a
startup that runs only when Taxation is enabled. Checkout depends on Products, not the other way round.
That direction stays: Products lists, and Checkout, Carts and Orders build on Products to sell.

| Project | Contents |
| --- | --- |
| `src/Core/CrestApps.OrchardCore.Products.Core` | Purchasability, price display and compare-at price (4.1). Listing contracts: `ProductListingPart`, facet and sort definitions, `ProductAttributeSettings`, `ProductPageSettings`, `ProductSearchDocument`, `ProductSearchQuery`, `ProductSearchResult`, `IProductSearchService`, `IProductAttributeValueProvider`. |
| `src/Modules/CrestApps.OrchardCore.Products` | Feature `CrestApps.OrchardCore.Products` (existing): the parts, editors and currencies. **New feature `CrestApps.OrchardCore.Products.Listing`**: the Product Listing type and its editor, product-attribute field settings, the field value providers, the search document builder and indexes, the SQL search provider, the URL state, listing shapes and views, the index handler and rebuild task, and the vehicle sample recipe. Taxonomy fields are read from their stored JSON, so no reference to the Taxonomies module is needed. |

Listing is its own feature for one reason: it creates index tables and a background task. A site that uses
products only for subscription plans has no use for them, so it does not pay for them.

The search names avoid the word *catalog*, because CrestApps already uses `CatalogItem` and
`ICatalogEntryHandler` for its generic document stores. An Elasticsearch provider later becomes a third
feature, `CrestApps.OrchardCore.Products.Listing.Elasticsearch`.

The architecture test that guards the foundation gains one assertion: Products and Products.Core reference
no Checkout, Payments, Carts, Orders or provider project.

## 6. Delivery slices

Each slice is mergeable on its own and ends with a build (`-c Release -warnaserror`), its tests, docs and a
browser pass in TheTheme.

| Slice | Scope | Exit gate |
| --- | --- | --- |
| **C0 Purchasability** | 4.1: `SalesMode`, `IsPurchasable`, `PriceDisplay`, `CompareAtPrice`, editor rules, snapshot enforcement, migration, schema | A listing-only product saves without a price. Checkout refuses it on the server. Existing subscription plans behave the same. |
| **C1 Index and search** | 4.3–4.5: field settings, value providers (non-taxonomy), `ProductSearchDocument`, indexes, handlers, rebuild task, SQL search with filters, sorts, paging and disjunctive counts | SQLite integration tests cover OR/AND, disjunctive counts, ranges, sorts, culture and type permissions. Publishing, unpublishing and deleting keep entries in step. |
| **C2 Listing page** | 4.6–4.8: `ProductListingPart` and editor, Product Listing type, URL contract, shapes, grid and list views, filters sidebar and offcanvas, toolbar, chips, pager (no JavaScript yet) | The vehicle recipe catalog filters, sorts and pages correctly in TheTheme on desktop and at phone width with JavaScript turned off. |
| **C3 Taxonomy facets** | Hierarchical and level-based facets, dependent facets, term labels and slugs, re-index on term change, swatches, range buckets | Make → Model and category drill-down work. Renaming a term updates the listing after the rebuild. |
| **C4 Detail page** | 4.9: `ProductPart` display, `ProductPrice`, `ProductSpecifications`, carousel alternate, `ProductActions` zone with an inquiry button, breadcrumb | A vehicle and a property have complete detail pages. A listing-only item shows no buy action. |
| **C5 Enhancement and polish** | Vanilla JavaScript partial swap, `pushState`, `aria-live`; caching; SEO through `OrchardCore.Seo` (done); real estate recipe (vehicle and store samples done); docs pages and a docs video | Lighthouse accessibility ≥ 95 on the listing page; filtering without a full reload; docs build passes. |
| **C6 Later** | Elasticsearch provider; JSON search API for headless use; compact table view; term landing pages; saved searches and "notify me of new listings"; compare; recently viewed; map view (needs a location field); inquiry → CRM lead | Scheduled after Carts or on demand. |

After C5, the roadmap continues: Customers (Phase 1), the rest of catalog completion (variants, which add
`VariantId` attribute rows so a product matches when any of its variants does), Carts (which fill
`ProductActions`), then Orders.

## 7. Tests

- **Unit:** URL parse and serialize round trips, including malformed input; facet semantics; range
  parsing in several cultures; value providers per field type; purchasability in the snapshot resolver.
- **Integration (SQLite, plus the SQL Server and PostgreSQL CI legs where available):** entry maintenance
  through publish, unpublish, delete and rebuild; search correctness against a seeded catalog; disjunctive
  counts; permission and culture filtering; table prefix.
- **Architecture:** Products and Products.Core reference no checkout, payment, cart, order or provider project.
- **Playwright:** listing page with JavaScript off and on, mobile offcanvas, view switch, chips, deep links,
  back and forward.

## 8. Documentation

- A listing section on the `modules/products` page and a user-manual section: setting up a listing page, marking attributes,
  building facets, and the three starter recipes.
- The Products page and README: listing-only products, price display, compare-at price.
- A theme guide: the shape list, alternates, and overriding a card per content type.
- The changelog for the current `VersionPrefix`.

## 9. Open decisions

Each decision has a recommendation. The slices above assume the recommended answer.

| # | Decision | Recommendation | Alternative |
| --- | --- | --- | --- |
| D1 | Where listing lives | **Decided:** a `Products.Listing` feature in the Products module. Checkout keeps extending Products. | A separate Catalog module (the first draft of this plan). It added a module without adding a boundary, since Products is already checkout-free. |
| D2 | Sequence | Product listing **before** Customers, Carts and Orders | Keep the roadmap order and reach listing in Phase 10. |
| D3 | Purchasability | `SalesMode` and `PriceDisplay` on `ProductPartSettings`, with an item-level value only when the type allows it, enforced in the snapshot resolver | A separate `ListingPart` for listed items. This duplicates price and SKU and splits "what is a product". |
| D4 | Attribute model | Content fields and taxonomies, marked with product-attribute field settings | A generic attribute bag defined per category (the eBay model). More dynamic, but it duplicates Orchard's field system and loses typed editors. |
| D5 | Search engine | Own `ProductSearchDocument` with a SQL provider first; Elasticsearch provider later | Build on Orchard's Lucene or Elasticsearch indexing from the start. This makes a search server mandatory for small sites. |
| D6 | Facet values in URLs | Readable slugs, falling back to ids | Opaque ids only (simpler, but worse for SEO and sharing). |
| D7 | What can be listed | Content types carrying `ProductPart` | Any content type (job boards, directories). Out of scope: listing is a Products feature. |
