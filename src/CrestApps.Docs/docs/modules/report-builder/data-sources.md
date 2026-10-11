---
sidebar_label: Built-in Data Sources
title: Report Builder Data Sources
description: The data sources and data sets the Report Builder offers, the features that register them, the permissions they check, and the relationships they declare.
user_manual:
  - user-manual/report-builder/data-sources
  - user-manual/report-builder/joins
---

A **data source** (`IReportDataSource`) offers one or more **data sets**, each with typed fields. The builder lists every registered source and the data sets the principal may read. To add your own, see [Add your own data source](custom-data-sources.md).

| Data source | Technical name | Registered when these are enabled |
| --- | --- | --- |
| **Report views** (saved views) | `ReportViews` | Report Builder |
| **Users** (user accounts and their roles) | `Users` | Report Builder |
| **Content items** (content types) | `Contents` | Report Builder and `OrchardCore.Contents` |
| **Queries** (saved Orchard Core queries) | `Queries` | Report Builder and `OrchardCore.Queries` |
| **Omnichannel** (activities, dispositions, campaigns, batches) | `Omnichannel` | Report Builder and Omnichannel Activities |
| **Contact Center** (interactions, calls, queues, agents, dialer, recordings, voicemail) | `ContactCenter` | Report Builder and the Contact Center feature that stores each data set |
| **AI chat** (chat sessions and their metrics) | `AIChat` | Report Builder and AI Chat (metrics: AI Chat Session Analytics) |
| **Messaging** (conversations and their messages) | `Messaging` | Report Builder and Omnichannel Messaging Workspace |

The built-in sources need no feature of their own: each is registered by a startup class marked with `[RequireFeatures]`, so it appears as soon as its Orchard Core feature is enabled alongside the builder. The technical names are stored in designs (`ReportDataSetReference.Source`) and in [field references](custom-data-sources.md#relationships), so they never change.

## Report views

The **Report views** source is part of the builder. It offers each saved view as a data set whose fields are the view's result columns, to every principal who passes `ViewAllReportDesigns` on the view (every designer; see [Permissions](permissions.md)). It runs the view's query through the engine, or reads its stored rows when the view is scheduled (see [Scheduled views](large-data.md#scheduled-views)). It guards recursion through `ReportDataSourceContext.Properties` (see `ReportViewRunner.EnterAsync`): a view that reads itself is refused, and views read other views at most 8 levels deep.

## Content items

When `OrchardCore.Contents` is enabled, the **Content items** data source offers one data set per content type the user may view, reading published items with YesSql. Types with the `Widget` stereotype are left out: widgets are pieces of pages, not records.

| Field | Name |
| --- | --- |
| Metadata | `ContentItemId` (identifier), `ContentItemVersionId`, `DisplayText`, `ContentType`, `Owner` (identifier), `Author`, `CreatedUtc`, `ModifiedUtc`, `PublishedUtc`, `Published` |
| Content field | `{PartName}.{FieldName}`, with `.{Suffix}` for extra values |
| Part data | `TitlePart.Title`, `AutoroutePart.Path`, `ContainedPart.ListContentItemId`, and other common parts |
| Every other part property | `{PartName}.{Path}`, such as `OmnichannelContactPart.DoNotCall` or `AutoroutePart.RouteContainedItems`, in a **(more)** group named after the part |

For example, a `Customer` type with an `Email` text field and an `Order` type with a `Customer` content picker give `Customer.Email` and `Order.Customer` (the first picked id, an identifier), `Order.Customer.ContentItemIds` and `Order.Customer.DisplayText`. Join the order to the customer on `Order.Customer` = `ContentItemId`.

Built-in providers cover the Orchard Core text, numeric, boolean, date, date-time, time, HTML, Markdown, multi-text, link, content picker, user picker, media, taxonomy, localization set and YouTube fields, and the CrestApps phone field. An unknown field type is read as text from its `Text` or `Value` property. Date range filters on the content item index columns are passed down to the query; text filters are not, because database collations may compare case differently from the engine. `ContentItemId` and `Owner` filter join keys exactly (see [Large data](large-data.md#joins-read-only-what-can-match)).

### Every property of every part

Anything a content type stores can be a column, including the properties of parts written in code that no provider describes. When the builder loads the fields of a content type, the source reads its 50 newest published items and adds every property their parts store that is not already a field: each scalar value, nested objects (up to four levels, named with dots, such as `Settings.Mode`), and lists of plain values (joined with commas). Parts an item stores without the content type listing them, such as a part attached in code, are included too. The types come from the stored values; a property that is always empty and ends with `Utc` is taken as a date and time.

When the **CrestApps Recipes** feature is on, the JSON schemas it keeps for parts also describe their properties, typed, even before any item stores them; a property a schema describes keeps the schema's type. Modules can describe the properties of their own parts the same way with `IContentReportPropertySource` (see [Content fields and parts](custom-data-sources.md#content-fields-and-parts)).

The content fields of a part, lists of objects, and properties whose path names a secret (such as a token, password, API key, hash or salt) are never added. The items are read only for the schema and for a report that uses one of these properties.

A content type is listed only when the principal holds `ViewContent` for it (see [Permissions](permissions.md#data-sources-are-the-security-boundary)). To add fields for your own content field types or parts, see [Content fields and parts](custom-data-sources.md#content-fields-and-parts).

### Relationships

Relationships come from the content definitions, so nothing about a site's types is hard-coded:

| Relationship | Declared by | Field that points to the other data set |
| --- | --- | --- |
| A content picker | The picker's **Displayed content types** setting | `{PartName}.{FieldName}` → `ContentItemId` of each listed type |
| A list | A `ListPart` whose **Contained content types** include the type | `ContainedPart.ListContentItemId` (**Container ID**) → `ContentItemId` of the list's type |
| The owner | Every content item | `Owner` → `Users.UserId` |
| A user picker | Every user picker field | `{PartName}.{FieldName}` → `Users.UserId` |

For example, when an `Account` type has a `ListPart` that contains `Contact`, the `Contact` data set gets a **Container ID** field, and adding `Contact` to a report that has `Account` joins them on it. A content picker that allows any type declares no relationship, because the builder cannot know which type is picked.

## Queries

When `OrchardCore.Queries` is enabled, the **Queries** data source offers every saved query (SQL, Lucene, Elasticsearch, or any other query source) as a data set, so a query someone wrote once can be joined, filtered and charted like any other data.

- A query is listed and read only for a principal allowed to execute it: `ExecuteApi_{QueryName}` (**Execute Api -** *query name*), or `ExecuteApiAll` (**Execute Api - All queries**).
- A query declares no columns, so its fields are found in its results. Each result item is written as JSON; nested objects become dotted fields (`TitlePart.Title`, `Customer.Balance.Value`) up to 4 levels deep, lists of plain values are joined with commas, and each field's type is inferred from the first 200 items (whole numbers, decimals, booleans, ISO dates and date-times, otherwise text). At most 300 fields are read. Fields ending in `Id` are offered as join keys.
- A query that returns content items (**Return content items**) gives its content item properties and part and field values the same way.
- The query runs once per report run, without parameters, so write queries whose parameters have defaults. Its own limits apply first; the report then keeps at most `MaxRowsPerDataSet` rows.
- A query that fails stops the report with the query's name and error message, and the failure is logged.

## Users

The **Users** data source is part of the builder. It is listed only for principals with the **View Users** permission (`ViewUsers`) and offers two data sets:

| Data set | Rows | Fields |
| --- | --- | --- |
| **Users** | One per user account | `UserId` (identifier), `UserName`, `Email`, `EmailConfirmed`, `PhoneNumber`, `PhoneNumberConfirmed`, `IsEnabled`, `TwoFactorEnabled`, `IsLockoutEnabled`, `LockoutEndUtc`, `AccessFailedCount`, `Roles` (comma separated), and `Properties.*` for the custom user settings stored with the users |
| **User roles** | One per user and role | `UserId` (references **Users**), `UserName`, `Role` |

Password hashes, security stamps, tokens and external login keys are never exposed. Neither is any `Properties.*` value whose path names a secret, such as an access or refresh token, a client secret, a password or passcode, a credential, an API, access, private, signing or encryption key, a hash, stamp or salt, a one-time or recovery code, an authenticator or a cookie, which modules sometimes store with a user. The `Properties.*` fields are found the same way as query fields: nested objects become dotted names and types are inferred from the stored values. Content item owners and user picker fields reference **Users**, so they join to it automatically. `UserId` filters join keys exactly.

## Business records

Modules expose their own records as data sources, registered only when the Report Builder is enabled with the feature that stores them. Each data set reads its newest records first, up to `MaxRowsPerDataSet`, and narrows the read to the date range a report's filters put on its main date. Identifier fields reference the data sets they point to, so related data sets join automatically: an activity's assigned user joins **Users**, its disposition joins **Dispositions**, an interaction's activity joins **Activities**, and so on across sources.

| Source | Data sets | Permission |
| --- | --- | --- |
| **Omnichannel** | **Activities** (tasks, calls, messages and the dialer's attempts, with disposition and campaign names and minutes to complete), **Dispositions** (with their **Outcome**, which groups them), **Campaigns**, **Campaign groups**, **Activity batches** (bulk loads and their skip counts) | **View Omnichannel reports** (`ViewOmnichannelReports`) |
| **Contact Center** | **Interactions** (with wait, talk and wrap-up seconds and the dialer's `Dialer.*` fields: attempt, outcome, answering machine result, pacing), **Interaction events**, **Call sessions**, **Call quality**, **Call recordings**, **Callback requests**, **Dialer profiles**, **Queues**, **Queue groups**, **Queue items**, **Agent profiles**, **Agent sessions**, **Shared voicemails** | **View Contact Center reports** (`ViewContactCenterReports`); call recordings also need **Listen to anyone's call recordings** (`ListenToAllCallRecordings`), and shared voicemails need **Access shared queue voicemail for entitled queues** (`AccessContactCenterSharedVoicemail`) and show only the queues the reader may answer |
| **AI chat** | **Chat sessions** (with the AI profile name), **Chat session metrics** (messages, handle time, tokens, ratings, resolution, conversion) | **View AI Chat Analytics** (`ViewChatAnalytics`) |
| **Messaging** | **Conversations** (assignment, unread count, first response time), **Messages** (direction, delivery status, length) | **View all messaging conversations** (`ViewAllMessagingConversations`) |

A dialer attempt is an activity plus the interaction that placed the call, so dialer reports join **Activities** to **Interactions** on the activity ID. Agent fields named `AgentId` hold agent profile IDs and join **Agent profiles**; fields holding user IDs join **Users**. Secrets, storage locations, IP addresses and message text are never exposed.

The Omnichannel **Activities** data set and the Contact Center data sets group simple reports in the database with `ReportIndexAggregator`, for the fields their index holds, and filter their key columns for joins; see [Large data](large-data.md).
