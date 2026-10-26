---
sidebar_label: Built-in Data Sources
title: Report Builder Data Sources
description: The data sources and data sets the Report Builder offers, the features that register them, the permissions they check, and the relationships they declare.
user_manual:
  - user-manual/report-builder/data-sources
  - user-manual/report-builder/joins
---

A **data source** is an Orchard Core `IDataSource` (namespace `OrchardCore.DataSources`), registered with the **Data Pipelines Core** feature (`OrchardCore.DataPipelines.Core`) of Orchard Core 4.0. It offers one or more **data sets**, each with typed fields. The builder lists every data source `IDataSourceManager` knows and the data sets the principal may read, so the same sources serve reports and Orchard Core's data pipelines. To add your own, see [Add your own data source](custom-data-sources.md).

| Data source | Technical name | Provided by | Registered when these are enabled |
| --- | --- | --- | --- |
| **Report views** (saved views) | `ReportViews` | Report Builder | Report Builder |
| **Content items** (content types) | `Contents` | Orchard Core | Data Pipelines Core and `OrchardCore.Contents` |
| **Users** (user accounts and their roles) | `Users` | Orchard Core | Data Pipelines Core and `OrchardCore.Users` (a dependency of Reports) |
| **Queries** (saved Orchard Core queries) | `Queries` | Orchard Core | Data Pipelines Core and `OrchardCore.Queries` |
| **Omnichannel** (activities, dispositions, campaigns, batches) | `Omnichannel` | CrestApps Omnichannel | Data Pipelines Core and Omnichannel Activities |
| **Contact Center** (interactions, calls, queues, agents, dialer, recordings, voicemail) | `ContactCenter` | CrestApps Contact Center | Data Pipelines Core and the Contact Center feature that stores each data set |
| **AI chat** (chat sessions, their metrics, and AI usage) | `AIChat` | CrestApps AI Chat | Data Pipelines Core and AI Chat (metrics and usage: AI Chat Session Analytics) |
| **Messaging** (conversations and their messages) | `Messaging` | CrestApps Omnichannel Messaging | Data Pipelines Core and Omnichannel Messaging Workspace |
| **Orders** (orders, lines, refunds, disputes, discounts, taxes, returns, gift cards) | `Orders` | CrestApps Orders | Data Pipelines Core and Orders (returns: Returns; gift cards: Gift Cards) |
| **Products** (products, variants, prices, bundle items, attributes) | `Products` | CrestApps Products | Data Pipelines Core and Products (bundle items: Product Bundles; attributes: Product Listing) |
| **Customers** (customers, loyalty points, store credit, referrals) | `Customers` | CrestApps Customers | Data Pipelines Core and Customers (and Loyalty Points, Store Credit, Referrals) |
| **Companies** (companies, buyers, price lists) | `Companies` | CrestApps Companies | Data Pipelines Core and Companies |
| **Inventory** (stock, movements, reservations, locations, purchase orders, back-in-stock alerts) | `Inventory` | CrestApps Inventory | Data Pipelines Core and Inventory (and Purchase Orders, Back-in-Stock Alerts) |
| **Checkout** (checkout sessions, coupons and their uses, saved payment methods) | `Checkout` | CrestApps Checkout | Data Pipelines Core and Checkout |
| **Carts** (carts, cart lines, wishlists) | `Carts` | CrestApps Carts | Data Pipelines Core and Carts (wishlists: Wishlists) |
| **Transactions** (transactions, payment attempts, refunds) | `Transactions` | CrestApps Transactions | Data Pipelines Core and Transactions (payment attempts and refunds: and Checkout) |
| **Subscriptions** (subscriptions, their lines, installment plans and installments) | `Subscriptions` | CrestApps Subscriptions | Data Pipelines Core and Subscriptions, or Subscriptions - Installment Plans |
| **Shipping** (shipments, shipment lines, zones, methods) | `Shipping` | CrestApps Shipping | Data Pipelines Core and Shipping |
| **Reviews** (product reviews) | `Reviews` | CrestApps Reviews | Data Pipelines Core and Product Reviews |
| **Downloads** (download links) | `Downloads` | CrestApps Downloads | Data Pipelines Core and Digital Downloads |
| **Bookings** (bookings, booking dates, balances) | `Bookings` | CrestApps Bookings | Data Pipelines Core and Bookings |
| **Marketplace** (vendors, earnings, payouts) | `Marketplace` | CrestApps Marketplace | Data Pipelines Core and Marketplace |
| **Point of Sale** (registers, shifts) | `PointOfSale` | CrestApps Point of Sale | Data Pipelines Core and Point of Sale |
| **Phone number verifications** (verification state of phone numbers) | `PhoneNumberVerifications` | CrestApps Phone Number Verifications | Data Pipelines Core and Phone Number Verifications |
| **DNC registry** (local do-not-call lists and their numbers) | `DncRegistry` | CrestApps DNC Registry | Data Pipelines Core and Local Do Not Call Registry |

The Report Builder depends on Data Pipelines Core, so every source above is available as soon as the builder and the feature that stores the data are on. The CrestApps sources are registered by startup classes marked `[RequireFeatures("OrchardCore.DataPipelines.Core")]`, not by the builder: they are also registered when only Orchard Core's Data Pipelines feature is enabled, so data pipelines can read them too. The technical names are stored in designs (`ReportDataSetReference.Source`) and in [field references](custom-data-sources.md#relationships), so they never change.

:::note[Fields that changed with Orchard Core's sources]
The content items and users sources are Orchard Core's, so a few fields of earlier versions of the builder are gone: the display text of the items a content picker picks (`{Part}.{Field}.DisplayText`), the user names of a user picker, the properties found by sampling stored items (the **(more)** groups), the custom user settings (`Properties.*`), and the `Role` field of **User roles**, which is now `RoleName`. A design or view that reads one of them no longer finds the field when it runs: open it in the builder and replace the field, such as by joining the picked content type and using its `DisplayText`.
:::

## Report views

The **Report views** source is part of the builder. It offers each saved view as a data set whose fields are the view's result columns, to every principal who passes `ViewAllReportDesigns` on the view (every designer; see [Permissions](permissions.md)). It runs the view's query through the engine, or reads its stored rows when the view is scheduled (see [Scheduled views](large-data.md#scheduled-views)). It guards recursion through `DataSourceContext.Properties` (see `ReportViewRunner.EnterAsync`): a view that reads itself is refused, and views read other views at most 8 levels deep. Since it is an Orchard Core data source, a data pipeline can read a view too.

## Content items

When `OrchardCore.Contents` is enabled, Orchard Core's **Content items** data source offers one data set per content type the principal holds `ViewContent` for, grouped by the content type's stereotype (types without one under **Content**). A principal that also holds `EditContent` for the type reads the latest version of each item, drafts included; any other principal reads the published versions. The main date (`DefaultDateField`) is `CreatedUtc`.

| Field | Name |
| --- | --- |
| Common fields (group **Content item**) | `ContentItemId` (identifier), `ContentItemVersionId`, `ContentType`, `DisplayText`, `Owner` (identifier, references `Users.UserId`), `Author`, `Published`, `Latest`, `CreatedUtc`, `ModifiedUtc`, `PublishedUtc` |
| A value of a part | `{Part}.{Property}`, such as `TitlePart.Title`; a value of an object the part holds is `{Part}.{Object}.{Property}` |
| A content field with one value | `{Part}.{Field}`, such as `Product.Price` |
| A content field with several values | `{Part}.{Field}.{Property}`, such as `Product.Website.Url` and `Product.Website.Text` |

The values come from the values described in Orchard Core's `ContentDataSourceOptions` (by default the common parts and field types, such as `TitlePart.Title`, `AutoroutePart.Path`, `ContainedPart.ListContentItemId`, a text field's `Text` or a link field's `Url` and `Text`), from the public properties of the part and field classes registered in code with `AddContentPart<TPart>()` and `AddContentField<TField>()` (nested objects up to 3 levels deep), and from the content fields added to each part in the admin. A value that holds a list, such as the `ContentItemIds` of a content picker or the `UserIds` of a user picker, is one text with the items separated by commas. Lists of objects, dictionaries, raw JSON, properties marked `[JsonIgnore]`, and properties whose name names a secret (such as `Password`, `Secret`, `ApiKey`, `ConnectionString`, or a name ending with `Token`) are left out.

For example, a `Customer` type with an `Email` text field and an `Order` type with a `Customer` content picker give `Customer.Email` and `Order.Customer`, the picked ids. Join the order to the customer on `Order.Customer` = `ContentItemId`.

The source pushes down lists of `ContentItemId` and `Owner` values (so both filter join keys exactly; see [Large data](large-data.md#joins-read-only-what-can-match)), `Published`, and ranges of `CreatedUtc`, `ModifiedUtc` and `PublishedUtc`. It reads items in pages and releases each page from the session before reading the next.

To read a part or a field type differently, such as with another type or a value no class describes, see [Content parts and fields](custom-data-sources.md#content-parts-and-fields). The full description of the source is in Orchard Core's [Data Sources](https://docs.orchardcore.net/en/latest/reference/modules/DataSources/) documentation.

### Relationships

The only relationship the content items source declares is the owner: `Owner` references `Users.UserId`, so a content type joins **Users** automatically. Content pickers, user pickers and list containment declare no relationship; join them explicitly, such as `Order.Customer` = `ContentItemId` or `ContainedPart.ListContentItemId` = `ContentItemId` of the list's type. A picker that holds several items gives a comma-separated list, which matches no single key.

## Queries

When `OrchardCore.Queries` is enabled, Orchard Core's **Queries** data source offers every saved query (SQL, Lucene, Elasticsearch, or any other query source) as a data set, grouped by its source, so a query someone wrote once can be joined, filtered and charted like any other data.

- A query is listed and read only for a principal allowed to execute it: `ExecuteApi_{QueryName}` (**Execute Api -** *query name*), implied by `ExecuteApiAll` and `ManageQueries`.
- A query that returns content items (**Return content items**) has the common content item fields listed above, without the values of their parts and fields.
- The fields of any other query are inferred from a sample of its results, run without parameters: one field per property, typed from the values; nested objects and arrays are JSON text.
- The builder passes no query parameters (`DataSourceQuery.Parameters`), so write queries whose parameters have defaults. The query runs once per report run, as a whole; the report then keeps at most `MaxRowsPerDataSet` rows. Use the query itself to limit large results.

## Users

When `OrchardCore.Users` is enabled (the Reports feature depends on it), Orchard Core's **Users** data source is listed only for principals with the **View Users** permission (`ViewUsers`) and offers two data sets:

| Data set | Rows | Fields |
| --- | --- | --- |
| `Users` (**Users**) | One per user account | `UserId` (identifier), `UserName`, `Email`, `EmailConfirmed`, `PhoneNumber`, `PhoneNumberConfirmed`, `IsEnabled`, `TwoFactorEnabled`, `IsLockoutEnabled`, `LockoutEndUtc`, `AccessFailedCount`, `Roles` (comma separated) |
| `UserRoles` (**User roles**) | One per user and role | `UserId` (references `Users`), `UserName`, `RoleName` |

Password hashes, security stamps and other secrets are never exposed. Content item owners and the CrestApps sources' user fields reference `Users.UserId`, so they join to it automatically, and `UserId` filters join keys exactly.

## Business records

CrestApps modules expose their own records as data sources, registered when Data Pipelines Core is enabled with the feature that stores them. Each data set reads its newest records first, a page at a time, and narrows the read to the date range a report's filters put on its main date and to the values its filters allow for the fields its index holds, such as a status or a channel. Identifier fields reference the data sets they point to, so related data sets join automatically: an activity's assigned user joins **Users**, its disposition joins **Dispositions**, an interaction's activity joins **Activities**, and so on across sources.

| Source | Data sets | Permission |
| --- | --- | --- |
| **Omnichannel** | **Activities** (tasks, calls, messages and the dialer's attempts, with disposition and campaign names and minutes to complete), **Dispositions** (with their **Outcome**, which groups them), **Campaigns**, **Campaign groups**, **Activity batches** (bulk loads and their skip counts) | **View Omnichannel reports** (`ViewOmnichannelReports`) |
| **Contact Center** | **Interactions** (with wait, talk and wrap-up seconds and the dialer's `Dialer.*` fields: attempt, outcome, answering machine result, pacing), **Interaction events**, **Call sessions**, **Call quality**, **Call recordings**, **Callback requests**, **Dialer profiles**, **Queues**, **Queue groups**, **Queue items**, **Agent profiles**, **Agent sessions**, **Shared voicemails** | **View Contact Center reports** (`ViewContactCenterReports`); call recordings also need **Listen to anyone's call recordings** (`ListenToAllCallRecordings`), and shared voicemails need **Access shared queue voicemail for entitled queues** (`AccessContactCenterSharedVoicemail`) and show only the queues the reader may answer |
| **AI chat** | **Chat sessions** (with the AI profile name), **Chat session metrics** (messages, handle time, tokens, ratings, resolution, conversion), **Completion usage** (one row per metered AI request: provider, connection, deployment and model, profile, session, user, and every billing unit such as input, output, cached and reasoning tokens, audio and images, for cost reports), **Voice usage** (one row per voice session: talk time, turns, outcome, engine and model, tokens) | **View AI Chat Analytics** (`ViewChatAnalytics`) |
| **Messaging** | **Conversations** (assignment, unread count, first response time), **Messages** (direction, delivery status, length) | **View all messaging conversations** (`ViewAllMessagingConversations`) |
| **Orders** | **Orders** (totals in the order's currency, status, sales channel, dates, recovery, customer, addresses without street lines, fulfillment, register and staff, quote and approval), **Order lines**, **Order refunds**, **Order disputes**, **Order discounts**, **Order taxes** (by jurisdiction), **Returns**, **Gift cards** (never their codes) | **Manage orders** (`ManageOrders`); gift cards need **Manage gift cards** (`ManageGiftCards`) |
| **Products** | **Products** (one row per product, whatever its versions), **Product variants**, **Product prices**, **Product bundle items**, **Product attributes** | The content permissions of each product type: **View** reads the published products, **Edit** also the drafts |
| **Customers** | **Customers**, **Loyalty accounts** and **Loyalty activity**, **Store credit accounts** and **Store credit activity**, **Referrals** and **Referral rewards** (never referral codes) | **View customers** (`ViewCustomers`); referrals need **Manage customers** (`ManageCustomers`) |
| **Companies** | **Companies**, **Company buyers**, **Price lists**, **Price list entries** | **Manage companies** (`ManageCompanies`) |
| **Inventory** | **Stock items**, **Stock levels** (one row per item and location), **Stock movements**, **Stock reservations**, **Locations**, **Purchase orders**, **Purchase order lines**, **Back-in-stock alerts** (counts only, never the shoppers) | **View inventory** (`ViewInventory`) |
| **Checkout** | **Checkout sessions** (status, amounts, current step, expiry, the order they made), **Coupons**, **Coupon uses**, **Saved payment methods** (kind, brand and expiry only) | Checkout sessions: **Manage orders** (`ManageOrders`); coupons: **Manage coupons** (`ManageCoupons`); saved payment methods: **Manage payments and refunds** (`ManageRefunds`) |
| **Carts** | **Carts**, **Cart lines**, **Wishlists**, **Wishlist items** | **Manage orders** (`ManageOrders`) |
| **Transactions** | **Transactions** (outstanding ones by status), **Payment attempts**, **Payment refunds** | Transactions: **Manage transactions** (`ManageTransactions`); payment attempts and refunds: **Manage payments and refunds** (`ManageRefunds`) |
| **Subscriptions** | **Subscriptions**, **Subscription lines**, **Installment plans**, **Installments** | **Manage subscriptions** (`ManageSubscriptions`); installment plans need **Manage installment plans and charge customers' saved cards** (`ManageInstallmentPlans`) |
| **Shipping** | **Shipments**, **Shipment lines**, **Shipping zones**, **Shipping methods** | Shipments: **Ship orders and update their shipments** (`FulfillOrders`); zones and methods: **Manage shipping zones, methods and settings** (`ManageShipping`) |
| **Reviews** | **Product reviews** (rating, status, verified purchase; never the review text) | **Moderate product reviews** (`ManageProductReviews`) |
| **Downloads** | **Download entitlements** (never file locations or links) | **Manage digital files and downloads** (`ManageDigitalDownloads`) |
| **Bookings** | **Bookings**, **Booking dates**, **Booking balances** | **Manage bookings** (`ManageBookings`) |
| **Marketplace** | **Vendors**, **Vendor earnings**, **Vendor payouts** | **Manage the marketplace** (`ManageMarketplace`) |
| **Point of Sale** | **Registers**, **Register shifts** (opening and closing cash, expected and counted, sales) | **Manage registers and shifts** (`ManageRegisters`) |
| **Phone number verifications** | **Verifications** (one row per content item with a verified phone number part) | **Run 'Phone Number Verifications' Report** (`RunPhoneNumberVerificationsReport`) |
| **DNC registry** | **DNC lists**, **DNC entries** (the numbers of the local lists) | **Manage DNC registry settings** (`ManageDncRegistrySettings`) |

A dialer attempt is an activity plus the interaction that placed the call, so dialer reports join **Activities** to **Interactions** on the activity ID. Agent fields named `AgentId` hold agent profile IDs and join **Agent profiles**; fields holding user IDs join **Users**. Secrets, storage locations, IP addresses and message text are never exposed.

The store's sources point at each other the same way: an order joins its **Customers** row, its company, its checkout session and its cart; an order line joins **Products** and **Product variants**; a stock item, a review, a download or a booking joins its product; returns, refunds, shipments and gift cards join **Orders**; and a subscription joins its owner in **Users** and **Customers**. Every money field has a `Currency` field beside it and says in its description which currency it is in, since a store can sell in several. Each module's page lists its data sets and their fields: [Orders](../carts-and-orders.md#report-builder-data-source), [Carts](../carts-and-orders.md#carts-report-builder-data-source), [Products](../products.md#report-builder-data-source), [Customers](../customers.md#report-builder-data-source), [Companies](../companies.md#report-builder-data-source), [Inventory](../inventory.md#report-builder-data-source), [Checkout](../checkout.md#report-builder-data-source), [Transactions](../transactions.md#report-builder-data-source), [Subscriptions](../subscriptions.md#report-builder-data-source), [Installment plans](../installment-plans.md#report-builder-data-source), [Shipping](../shipping.md#report-builder-data-source), [Reviews](../reviews.md#report-builder-data-source), [Downloads](../downloads.md#report-builder-data-source), [Bookings](../bookings.md#report-builder-data-source), [Marketplace](../marketplace.md#report-builder-data-source), [Point of Sale](../point-of-sale.md#report-builder-data-source), [Phone number verifications](../phone-number-verifications.md#report-builder-data-source), [DNC registry](../dnc-registry.md#report-builder-data-source) and [AI usage](../../ai/usage-analytics.md#report-builder-data-source). The [store reports recipe](../carts-and-orders.md#store-reports-to-adapt) adds ready-made reports over the orders to start from.

The Omnichannel **Activities** data set and the Contact Center data sets group simple reports in the database with `ReportIndexAggregator`, for the fields their index holds, and filter their key columns for joins; see [Large data](large-data.md).
