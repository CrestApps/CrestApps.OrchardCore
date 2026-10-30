---
sidebar_label: Product listings
title: Product listings
description: Publish pages that list your products with filters, sorting, search and product pages, for products you sell online and products you only list, such as homes or cars.
technical_manual:
  - modules/products
---

A **product listing** is a page of your site that lists products: a shop, a page of homes for sale, a dealership's stock, or your subscription plans. Visitors filter, sort, search and page through the products, switch between a grid and a list, and open a product's own page. A listing works the same for products sold online and for products that are only listed, whose visitors call or send an inquiry instead.

| | |
| --- | --- |
| **Menu** | Commerce > Product Listings for the listing pages, and Commerce > Products for the products themselves |
| **Permission** | The content permissions of the types involved: listing, creating, editing and publishing **Product Listing** items, and your product types' items |
| **Feature** | Product Listing (it turns on Products). Turn on SEO as well to control how listings appear in search engines. |

<AskYourAdmin />

Setting up a listing takes three steps: decide which of your products' fields visitors can filter and sort by, create the listing page, and lay out each product's own page.

## Decide what visitors can filter on

<YouTube video="product-listings-and-attributes" title="Choosing what a field is for" start={93} end={142} />

Filters come from the fields of your product types: a car's year and mileage, a home's bedrooms, a shirt's color and size. You choose which fields take part in the field's own settings.

1. Open **Content > Content Definition > Content Types** and edit a product type, that is, a type with the **Product** part.
2. Click **Edit** next to a field.
3. In the **Product listing** section, tick what the field is for, and save.

| Setting | What it does |
| --- | --- |
| **Filterable** | Visitors can filter listings by this field. |
| **Sortable** | Visitors can sort listings by this field: numbers, dates and times both ways, and text and taxonomy fields A to Z. |
| **Searchable** | The listing's keyword search matches this field. |
| **Show on product cards** | Shows the value as a short chip on the product's card, such as *2019 · 42,000 mi*. |
| **Show in specifications** | Shows the value in the specification table of the list layout and the product page. |
| **Listing label** | The label visitors see. Leave it empty to use the field's name. |
| **Number format** and **Unit** | Numbers only: how the number is written, such as *0* for a year, and a unit shown after it, such as *mi* or *sq ft*. |
| **Swatch colors** | Text and taxonomy fields: the color of each value when a filter shows them as color swatches, one per line, such as *Midnight = #191970* or *Sky = light blue*. A value you do not list uses its own name when that is a color, such as *Navy*. |
| **Color field on terms** | Taxonomy fields: the name of a text field on the taxonomy's terms that holds each term's color, so you set a color once on its term. |

The **Product listing** section appears only on fields of product types. Text, number, yes/no, date, date and time, time, link, content picker, user picker and taxonomy fields can take part, and HTML and Markdown fields can be searched. A taxonomy such as *Make and model* can give one filter per level, for example **Make** and then **Model**. A date field filters by a range of dates, with ready-made ranges by year.

The price, the product's type, whether it can be bought online and whether it is featured are always available to filter and sort by, without any field. A product type sold in variants can also filter on its options; see [Products and currencies](products-and-currencies.md#sell-a-product-in-variants).

## Create a listing page

<YouTube video="product-listings-and-attributes" title="Setting up the listing page" start={160} end={211} />

1. Open **Commerce > Product Listings** and click **New**.
2. Enter a **Title**, and a **Permalink** such as *shop* or *homes* for the page's address.
3. Fill in the listing settings below, and click **Publish**.

The page is an ordinary content item, so you can add it to a menu and give it permissions like any other page.

### Products

Choose **Every product type**, which also lists types you add later, or **Only these product types** and tick at least one. The filters and sorts further down are grouped by product type, and only the groups of the types you list are shown.

### Filters

Untick **Show filters** to list the products without any filters, for example a short list of plans. A listing with no filters and no keyword search has no filters panel, and its products take the full width.

Leave **Offer a filter for every filterable field** ticked to offer one filter per filterable field. Untick it to offer only the filters you tick, in the order you give them. Click **Choose filters** to open the list; the button shows how many filters are in use. Each row is one filter:

| Setting | What it does |
| --- | --- |
| **Use** (the tick box) | Offers the filter. |
| **Label** | The filter's name on the page. Leave it empty to use the field's listing label. |
| **Kind** | What visitors pick: **Checkboxes** (several values), **Radio buttons** (one value), **Range** (a minimum and a maximum) or **Toggle** (yes only). |
| **Style** | How the options are drawn: a **List** with counts, **Buttons** for short values such as sizes, or **Color swatches**. Only the styles that suit the kind are offered. |

The settings you change less often open with the **More settings** button at the end of the row. What you set there is summed up under the field's name, such as *?model= · after make · level 2*.

| Setting | What it does |
| --- | --- |
| **URL key** | The short name of the filter in the page's address, such as *make* in *?make=toyota*. Each filter needs its own. |
| **Shown after** | The URL key of another filter that must have a selection first, so **Model** appears once a **Make** is picked. |
| **Visible options** | How many options show before a *See more* link. *0* shows them all. |
| **Term level** | Taxonomy fields: which level of the tree the filter offers, *1* for the top level such as makes, *2* for the next such as models. |
| **Start collapsed** | Starts the filter folded. |
| **Offer ready-made ranges** | Range filters: offers ready-made ranges above the minimum and maximum boxes. Leave the box below it empty for round ranges worked out from your products, such as *up to $50, $50 to $100, ... $200 and up*, or type your own, such as *-25, 25-75, 75+* for up to 25, 25 to 75, and 75 and up. Both ends are included, so *1-2, 3-4, 5+* suits bedrooms. |

To change the order, drag a row by its handle, or focus the handle and press the up and down arrow keys.

Besides your fields, every listing can offer **Price**, **Type**, **Featured** and, when the [Inventory](inventory.md) feature counts stock, **In stock**, which shows only products that can be bought now.

### Sorting

Untick **Let visitors change the sort** to always show the products in the default sort's order.

Leave **Offer the standard sorts and every sortable field** ticked to offer **Recommended**, **Featured first**, *Newest*, the price both ways, *Name* and each sortable field. Untick it to offer only the sorts you tick, which you reorder by dragging like filters. **Recommended** orders products by the **Position** you give each product, with products without one after them, and **Featured first** shows products marked **Featured** first. Both are set on each product's **Product** part; see [Products and currencies](products-and-currencies.md). A subscription plan without a position keeps its **Sort** value.

**Default sort** is the order visitors see before they pick one. When visitors cannot change the sort, it can be any sort, even one you did not tick.

### Layout and paging

| Setting | What it does |
| --- | --- |
| **Layout** | Whether products open as a grid of cards or a list of rows, and **Let visitors switch between grid and list**. |
| **Image shape** | The shape of the product images on the cards: landscape, square, portrait or wide. Images are cropped to fit. |
| **Page sizes** | The numbers of products per page visitors can pick from, such as *12, 24, 48*. |
| **Default page size** | The page size visitors see first. |
| **Paging** | How visitors move through the products: **Numbered pages**, **A Load more button** that adds the next products below, or **Infinite scroll**, which adds them as the visitor scrolls down. Numbered page links stay underneath for search engines and for visitors without JavaScript. |
| **Keyword search** | Adds a search box that matches titles, SKUs and searchable fields. |
| **Search index** | Where the listing searches: **The database**, the default, or a product search index you created; see [Search with Elasticsearch, Lucene or Azure AI Search](#search-with-elasticsearch-lucene-or-azure-ai-search). Shown once such an index exists. |

## Lay out the product pages

Each product can have its own page with its images, price, highlights and actions, and its specification table.

1. Open **Content > Content Definition > Content Types** and edit the product type.
2. Click **Edit** next to the **Product** part.
3. Tick **Show product details on the product page**.
4. For products that cannot be bought online, fill in **Inquiry button**, such as *Schedule a viewing*, and **Inquiry link**, where the button leads. In the link, `{id}` and `{title}` are replaced with the product's identifier and title.
5. Save.

Add the **Autoroute** part to the type as well, so each product has a readable address.

Whether a product can be bought online, and how its price is shown (shown, *Call for price*, or hidden), are set on the type and on each product; see [Products and currencies](products-and-currencies.md). A product that can be bought shows its buy button: a subscription plan's **Sign up**, or **Add to cart** when the Carts feature is on (see [Carts and orders](carts-and-orders.md)).

## What visitors see

<YouTube video="product-listings-and-attributes" title="What visitors see" start={211} end={230} />

- The filters, in a side panel on wide screens and behind a **Filters** button on phones. Each filter shows how many products match each option, and folds open and closed.
- The picked filters as chips above the results, each with a button to remove it, and **Clear all**.
- The number of results, the sort, the page size and the grid or list switch.
- Product cards with the image, the title, the chips, the price (with the *was* price struck through when there is a saving) and the product's actions.
- A breadcrumb, such as *Home › Shop › Footwear › Boots* when a category is picked.

Every combination of filters has its own address, so visitors can bookmark it, share it and use the Back button. Filters apply as soon as they change on wide screens.

## Recommend products

<YouTube video="product-bundles-and-recommendations" title="Recommend products" start={194} end={257} />

With the **Product Recommendations** feature, each product's editor has **Upsells** and **Cross-sells** pickers:

- **Upsells**, better or bigger choices, show on the product's page as *You may also like*.
- **Cross-sells**, things that go with the product, show in the cart as *Frequently bought together* and on the order's confirmation as *Goes well with your order*.

The product's page also shows **Related products** from its categories and the products the visitor **viewed recently**, kept in their browser.

## Let visitors compare products

<YouTube video="product-bundles-and-recommendations" title="Compare products" start={257} end={283} />

With the **Product Comparison** feature, cards and product pages have a **Compare** tick box. Visitors tick up to four products, and a bar at the bottom of the page opens a page that sets them side by side: prices, stock and a row per specification. On a phone the comparison scrolls sideways.

## Sites in several languages

On a site with several languages, translate your products and listing pages with Orchard Core's localization as usual. A listing shows the products of the visitor's language, plus products that are not translated at all, and a product's page links back to the listing in its own language.

## Search with Elasticsearch, Lucene or Azure AI Search

Watch the video for the three steps: the feature, the index and the listing.

<YouTube video="product-search-indexes" title="Product search indexes in CrestApps for Orchard Core" />

Listings search your database by default, which suits most catalogs. A large catalog, or one where keyword search should rank the best matches first, can search a search server instead.

1. Turn on one of **Product Search (Elasticsearch)**, **Product Search (Lucene)** or **Product Search (Azure AI Search)** under **Configuration > Features**. Elasticsearch and Azure AI Search need their connection set up first; Lucene keeps its index on your server and needs nothing else.
2. Open **Search > Indexes**, click **Add** next to **Products (Elasticsearch)**, **Products (Lucene)** or **Products (Azure AI Search)**, enter a name, and save. The index fills with your products in the background.
3. Edit a listing, pick the index under **Search index**, and publish.

Visitors see the same filters, counts, sorts and pages as before. Keyword search matches the start of words, such as *fo* for *Ford*, and ranks the best matches first. Each change to a product reaches the index when it is published, and the index rebuilds itself when you change your product types or their fields. If the search server cannot be reached, the listing searches the database instead, so it keeps working.

## Search engines

With the SEO feature on, add the **SEO** part to the **Product Listing** type and to your product types, then fill it in on each listing and product. Filtered and searched pages of a listing point search engines at the listing's own address and ask not to be indexed, so search results show your listing once rather than every combination of filters.

## Shopping feeds

<YouTube video="product-import-and-feeds" title="Product feeds" start={169} end={211} />

With **Product Feeds** on, your products are published as a feed for Google Merchant Center, Meta (Facebook and Instagram shops) and TikTok, so they show in shopping ads and shops.

1. Set your site's base URL under **Settings > General**: the feed's links and photos need it.
2. Open **Settings > Commerce > Product feeds**. It shows the feed's address, how many products are in it, and why any are left out, such as a product with no photo.
3. Give Google Merchant Center, Meta Commerce Manager or TikTok the feed's address as a scheduled feed. Add a market's currency, such as *?currency=EUR*, for a feed in it.

Each product you sell online is in the feed with its price, sale price, photos and whether it's in stock; a product sold in variants is in it once for each variant. To give a product a brand, a GTIN (the barcode's number) or an MPN, add the **Product Feed** part to its content type, or set the store's **Brand** under the feed's settings. Products whose brand is in a taxonomy named for brands get it from there. Tick **Leave it out of shopping feeds** on a product to keep it out.

Choose **Make a key** under **Feed key** so only the channels you give the address to can read the feed. The feed is rebuilt every 15 minutes.

## Find your products and listings

**Commerce > Products** lists every product of every product type, including subscription plans, with a filter by type; its **New** button offers only product types. **Commerce > Product Listings** lists your listing pages.

## Try a sample

Three recipes under **Configuration > Recipes** set up a complete example you can explore and adapt:

| Recipe | Creates |
| --- | --- |
| **Product listing: online store sample** | 30 products sold online with categories, brands, colors and sizes, a **Shop** page at */shop*, orders, stock (a few products running low and one sold out) under **Commerce > Inventory**, shipping zones for the United States, Canada and the rest of the world under **Commerce > Shipping**, two sample customers with addresses under **Commerce > Customers**, product reviews, and the coupons WELCOME10 and FREESHIP75 |
| **Product listing: real estate sample** | 20 homes that are listed but not sold online, some with the price on request and a *Schedule a viewing* button, and a **Homes for sale** page at */homes* |
| **Product listing: vehicle dealership sample** | 28 vehicles, some sold online and the rest listed, filtered by make, model, year, mileage and more, and a **Vehicles** page at */vehicles* |

Run a sample on a test site first: each one adds content types, taxonomies and content items.

:::note[Your site needs a theme]
Listing pages are styled by your site's theme, which must be built on Bootstrap 5, as **TheTheme** and **The Blog Theme** are. A site set up with the **Blank** recipe has no front-end theme, so its listings show unstyled. Choose one under **Design > Themes** with **Make Current**.
:::
