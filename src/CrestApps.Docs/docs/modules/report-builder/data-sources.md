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
| **AI chat** (chat sessions and their metrics) | `AIChat` | CrestApps AI Chat | Data Pipelines Core and AI Chat (metrics: AI Chat Session Analytics) |
| **Messaging** (conversations and their messages) | `Messaging` | CrestApps Omnichannel Messaging | Data Pipelines Core and Omnichannel Messaging Workspace |
| **Orders** (orders, lines, refunds, disputes, discounts, taxes, returns, gift cards) | `Orders` | CrestApps Orders | Data Pipelines Core and Orders (returns: Returns; gift cards: Gift Cards) |

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
| **AI chat** | **Chat sessions** (with the AI profile name), **Chat session metrics** (messages, handle time, tokens, ratings, resolution, conversion) | **View AI Chat Analytics** (`ViewChatAnalytics`) |
| **Messaging** | **Conversations** (assignment, unread count, first response time), **Messages** (direction, delivery status, length) | **View all messaging conversations** (`ViewAllMessagingConversations`) |
| **Orders** | **Orders** (totals in the order's currency, status, sales channel, dates, customer, addresses without street lines, fulfillment, register and staff, quote and approval), **Order lines**, **Order refunds**, **Order disputes**, **Order discounts**, **Order taxes** (by jurisdiction), **Returns**, **Gift cards** (never their codes) | **Manage orders** (`ManageOrders`); gift cards need **Manage gift cards** (`ManageGiftCards`) |

A dialer attempt is an activity plus the interaction that placed the call, so dialer reports join **Activities** to **Interactions** on the activity ID. Agent fields named `AgentId` hold agent profile IDs and join **Agent profiles**; fields holding user IDs join **Users**. Secrets, storage locations, IP addresses and message text are never exposed.

The Omnichannel **Activities** data set and the Contact Center data sets group simple reports in the database with `ReportIndexAggregator`, for the fields their index holds, and filter their key columns for joins; see [Large data](large-data.md).
