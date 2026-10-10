---
sidebar_label: Report Builder
sidebar_position: 8
title: Report Builder
description: A drag-and-drop report builder with pluggable data sources, joins, formulas, filters, charts, pivots, reusable views, and secure sharing.
user_manual:
  - user-manual/report-builder
---

The **Report Builder** feature of the [Reports](reports.md) module lets people build their own reports in the browser. A designed report reads data sets from one or more **data sources**, joins them, adds calculated fields, filters, groups and aggregates the rows, and renders tables, charts, headline metrics and pivot tables through the same renderer and exporters as every other report. Three data sources are added automatically when their Orchard Core feature is enabled with the builder: **Content items** (with `OrchardCore.Contents`), **Queries** (with `OrchardCore.Queries`) and **Users**. Any module can add more sources.

| | |
| --- | --- |
| **Feature Name** | Report Builder |
| **Feature ID** | `CrestApps.OrchardCore.Reports.Builder` |
| **Dependency** | `CrestApps.OrchardCore.Reports` |

| Data source | Registered when these are enabled |
| --- | --- |
| **Report views** (saved views) | Report Builder |
| **Content items** (content types) | Report Builder and `OrchardCore.Contents` |
| **Queries** (saved Orchard Core queries) | Report Builder and `OrchardCore.Queries` |
| **Users** (user accounts and their roles) | Report Builder |
| **Omnichannel** (activities, dispositions, campaigns, batches) | Report Builder and Omnichannel Activities |
| **Contact Center** (interactions, calls, queues, agents, dialer, recordings, voicemail) | Report Builder and the Contact Center feature that stores each data set |
| **AI chat** (chat sessions and their metrics) | Report Builder and AI Chat (metrics: AI Chat Session Analytics) |
| **Messaging** (conversations and their messages) | Report Builder and the Messaging Workspace |

The built-in sources need no feature of their own: each is registered by a startup class marked with `[RequireFeatures]`, so it appears as soon as its Orchard Core feature is enabled alongside the builder.

How people use the builder is described in the User Manual: [Report Builder](../user-manual/report-builder.md).

## Architecture

The builder is split over the three Reports layers, so the engine can be reused outside Orchard Core and data sources can be added from any module:

- **`CrestApps.OrchardCore.Reports.Abstractions`** holds the data source contract (`IReportDataSource`, `IReportDataSourceManager`, `ReportDataType`, `ReportFieldDescriptor`, `ReportDataSetDescriptor`, `ReportDataSetSchema`, `ReportDataSourceQuery`, `ReportDataCondition`, `ReportDataTable`, `ReportDataValues`) in the `CrestApps.OrchardCore.Reports.DataSources` namespace, and the designed query model (`ReportQueryDefinition`, `ReportDataSetReference`, `ReportJoinDefinition`, `ReportCalculatedField`, `ReportFilterDefinition`, `ReportColumnDefinition`, `ReportSortDefinition`, `ReportVisualDefinition`) in `CrestApps.OrchardCore.Reports.Builder`.
- **`CrestApps.OrchardCore.Reports.Core`** holds the engine: `ReportQueryPlanner` checks a query against the live schemas and compiles it, `ReportQueryEngine` runs it, the formula language lives in `Designer/Expressions`, and `ReportDesignDocumentBuilder` turns a result into a `ReportDocument`.
- **`CrestApps.OrchardCore.Reports`** holds the Orchard Core side: the stores of designed reports, views and share links (document catalogs), permissions and the authorization handler, the builder pages and JSON endpoints, the admin menu, and the built-in **Report views** data source.

A run goes through these steps:

1. **Plan.** Every data set's schema is read with the principal the run reads data for. A data set the principal may not read fails the plan. Joins, calculated fields, columns, filters, sorts and the limit are checked, and every problem is reported at once.
2. **Read.** Each data source returns only the fields the query uses, up to the row limit, newest first. Row filters on data sets that no outer join can fill with empty values are offered to the source as `ReportDataCondition`s, which it may apply to read less; the engine always applies every filter again. Data sets are read in join order: a joined data set is read only for the keys the rows before it hold (see [Large data](#large-data)).
3. **Join.** Data sets are hash-joined in order (inner, left, right or full). Keys of different types (text and number) are compared as text; empty keys never match.
4. **Calculate, filter, group.** Row-level calculated fields are evaluated, then fixed filters, then the exposed filters with the viewer's values. When any column is a measure, rows are grouped by the dimension columns.
5. **Shape.** Result filters, sorts, the row limit and the result-size limit are applied.
6. **Render.** Visuals regroup the rows behind the result by fewer dimensions (charts, metrics, pivot tables, totals), so non-additive measures such as averages and distinct counts stay correct.

Processing runs in memory over the rows the sources return, so every source gets joins, formulas and aggregation without implementing them. Sources can take on part of the work to handle large data, as described next.

## Large data

The engine reads each data set up to `MaxRowsPerDataSet` rows (newest first) and warns when a data set has more. Four mechanisms keep large data sets fast and complete:

1. **A date filter from the start.** Data sets name their main date (`ReportDataSetDescriptor.DefaultDateField`, such as when a record was created). When the first data set of a new report has one, the builder adds a filter on it that viewers see as a **recent period** (today, the last 7, 30 or 90 days, the last 12 months, or all time), starting at the last 30 days. Relative date filters (in the last or next N days) are passed to the sources as a date range, so they read only that period.
2. **Grouping in the data source.** A report over one data set that groups by fields or date periods and counts, sums, averages, or takes the smallest or largest values can be answered by its source (`IReportAggregateDataSource`). The source returns per group the row count and the count, sum, smallest and largest value of each measured field; the engine merges these partial aggregates wherever it would aggregate rows, so sorting, result filters, totals, charts and pivots give the same result as reading every row. Row filters are sent as exact conditions and date periods as UTC boundaries of local hours, days or months. Whatever a source cannot answer exactly (a computed field, a text "contains" filter, a median or distinct count, a calculated field, an exposed filter that lists options) is read row by row as before. `ReportIndexAggregator` answers such a query with one `GROUP BY` statement over a YesSql index table; Omnichannel activities and the Contact Center data sets use it for the fields their index holds.
3. **Joins read only what can match.** For an inner or left join on a field the source can filter exactly (`ReportFieldDescriptor.IsKeyFilterable`), the engine sends the distinct keys of the rows before it as an `IN` condition marked `IsJoinKey`, in batches of `JoinKeyBatchSize` up to `MaxJoinKeys` keys. The joined data set then reads the records that can match instead of its newest records. Users, content items, Omnichannel activities, Contact Center records, AI chat sessions and messaging conversations and messages filter their key columns this way; a join with more keys, or on another field, reads the joined data set in full.
4. **Scheduled views.** A view can refresh every 15 minutes, hour, 6 hours or day instead of running every time a report reads it (see [Scheduled views](#scheduled-views)).

## Scheduled views

A view's **Refresh** setting (`ReportView.RefreshIntervalMinutes`, `0` for live) stores its result:

- The **Report view refresh** background task runs every 5 minutes and refreshes each due view in its own scope, with a distributed lock so two nodes never refresh the same view. A failed refresh keeps the previous rows and records the error. Tenants can turn the task off under **Background Tasks**.
- A scheduled view runs with its **owner's** current access, since there is no reader at refresh time; it does not refresh when its owner is deleted or disabled or can no longer read it. Reports that read the view still need to be allowed to read it and to plan its query, but they get the rows the owner's access produced. Schedule only views whose data everyone who can read the view may see.
- Reports read the stored rows (at most `MaxRowsPerDataSet`) while the stored fields match the view. Editing the view's query, switching it to live, or importing a changed view drops the stored rows, and reports run the view live until the next refresh.
- **Refresh now** (`POST /Admin/reports/views/{id}/refresh`) refreshes a view at once and returns its status.

### Time zones

A data source returns `DateTime` field values in UTC. The engine converts them to the tenant time zone before anything else, so transforms, formulas, filters and display all work in local time. `Date` fields carry no time zone and are never shifted. Filter values written without a time (`2026-01-31`) cover the whole day.

## Drafts, versions and editing together

A designed report has a published version, which is what people run, and a **draft**, which the builder saves into a moment after each change. Nobody sees the draft until someone publishes it.

- **Autosave.** Each change is sent to `POST /Admin/reports/builder/{id}/draft` with the revision the page has, a moment after it is made. The first change to a new report creates its draft (`POST /Admin/reports/builder/drafts`), which gets the report's identifier at once and keeps it when first published; until then the report exists only as that draft, is listed under **Not published yet** for its owner and for people who manage every report, and can be continued or deleted. Saves are sent with `keepalive`, so a change made right before the page is closed or refreshed is still saved, and the page does not ask before leaving.
- **Revisions.** Every draft save, publish, discard and restore increases the report's revision, kept in its `ReportDesignDraft` document. A change based on an older revision is refused with `409 Conflict` and the name of the person who changed it last; the page offers to reload their changes or to keep its own (which sends the change again with `force`). Changes to one report are serialized with Orchard Core's `IDistributedLock`, so the check holds on several nodes.
- **Publishing** saves the report as before (title, sharing and data rules are checked), adds an immutable `ReportDesignVersion` only when the report's content differs from the latest version, and clears the draft. A report published before versions existed first gets its earlier state as version 1.
- **Versions** can be previewed and **restored**: restoring copies a version into the draft, to be checked and published; the new version records which version it was restored from. Drafts and versions are separate YesSql documents with their own index tables (`ReportDesignDraftIndex`, `ReportDesignVersionIndex`), so autosaving one report never rewrites another. Deleting a report deletes its draft and versions.
- **Retention.** At most `MaxVersions` versions are kept per report (see [Configuration](#configuration)).

### Real time

When `OrchardCore.SignalR` is enabled, the builder connects to `ReportsHub` (at `/Communication/Hub/ReportsHub` under the tenant's path). No separate feature is needed: the hub is registered by a startup class marked with `[RequireFeatures("OrchardCore.SignalR")]`.

- **Presence.** A page subscribes to the report it edits; the hub checks that the user may edit it. Pages tell each other who arrived and left, and the builder shows the others' initials and a notice that changes can conflict. The server only relays these messages and keeps no list, so it works behind the Redis or Azure SignalR backplane.
- **Changes.** After a draft save, publish, discard, restore or delete is committed, `SignalRReportDesignNotifier` sends `ReportDesignChanged` (`kind`, `designId`, `revision`, `versionNumber`, `userName`) to the report's group. A page that is behind offers to reload. Group names are qualified by tenant with `TenantSignalRGroupName`.
- Without SignalR, the revision check still prevents silent overwrites; people find out when their next save is refused.

Other modules can react to report changes by replacing `IReportDesignNotifier`.

## Security model

- A designed report or view is authorized like a content item: the broad permission is checked with the item as the resource, and `ReportDesignAuthorizationHandler` grants it to the owner and to the people the report is shared with.

  | Permission | Key | Granted by the handler to |
  | --- | --- | --- |
  | Manage all custom reports and views | `ManageAllReportDesigns` | The owner, when they hold `ManageOwnReportDesigns`. |
  | Build reports and manage own custom reports and views | `ManageOwnReportDesigns` | (implied by `ManageAllReportDesigns`) |
  | View all custom reports | `ViewAllReportDesigns` | The owner; the shared users and roles; everyone for the `Anonymous` role; every signed-in person for the `Authenticated` role. Any builder, for a view. |
  | Share custom reports publicly and through share links | `ShareReportsPublicly` | Nobody: needed to share with the `Anonymous` role and to create share links. |

  Administrators get all four through the default stereotype.

- **A saved report reads data with its owner's current access**, whoever runs it. The owner vouches for what the report shows; viewers need no access to the underlying data. When the owner is deleted or disabled, or loses access to a data set, the report stops running. The builder preview reads with the builder's access.
- A data source must hide every data set the principal may not read. The **Content items** source lists a content type only when the principal holds `ViewContent` for it, including the type-specific permission of a securable type. Because Orchard Core grants `ViewContent` broadly by default, make content types that hold sensitive data **Securable** to control which report builders can report on them.
- Exposed filter values from the query string override only filters marked as exposed. Fixed filters cannot be changed or removed by a viewer.
- Share link tokens hold 256 random bits. Only their SHA-256 hash is stored, the full link is shown once, and links can expire, be revoked, require sign-in, and allow or deny export. Shared pages send `noindex` and `no-referrer`. Every link opening, export, creation and revocation is logged.
- Designer payloads are limited to 1 MB, formulas are nested at most 64 levels, views at most 8 levels, and a view that reads itself is refused.

## Pages and URLs

| URL | What it is |
| --- | --- |
| `/Admin/reports/designs` | The designed reports the user can run (Report Builder, or Shared Reports for non-report builders). |
| `/Admin/reports/designs/{id}` | Runs a designed report in the admin. |
| `/Admin/reports/builder/create`, `/Admin/reports/builder/edit/{id}` | The builder. |
| `/Admin/reports/views`, `/Admin/reports/views/create`, `/Admin/reports/views/edit/{id}` | Reusable views. |
| `/reports/view/{id}` | A report shared with users or roles, outside the admin, for people without admin access. |
| `/reports/shared/{token}` | A report opened through a share link. |

Run and export pages read exposed filter values from the query string: `applied=1`, then `f.{filterId}` (repeatable) or `f.{filterId}.from` and `f.{filterId}.to` for ranges.

## Configuration

The size limits of one run can be changed in the host's `appsettings.json` (a tenant's own `App_Data/Sites/{tenant}/appsettings.json` takes the same section without the `OrchardCore` wrapper):

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Reports": {
        "Builder": {
          "Limits": {
            "MaxRowsPerDataSet": 50000,
            "MaxJoinedRows": 250000,
            "MaxResultRows": 10000,
            "MaxFilterOptions": 500,
            "MaxJoinKeys": 10000,
            "JoinKeyBatchSize": 500
          },
          "Versions": {
            "MaxVersions": 50
          }
        }
      }
    }
  }
}
```

As environment variables:

```text
OrchardCore__CrestApps__Reports__Builder__Limits__MaxRowsPerDataSet=50000
OrchardCore__CrestApps__Reports__Builder__Limits__MaxJoinedRows=250000
OrchardCore__CrestApps__Reports__Builder__Limits__MaxResultRows=10000
OrchardCore__CrestApps__Reports__Builder__Limits__MaxFilterOptions=500
OrchardCore__CrestApps__Reports__Builder__Limits__MaxJoinKeys=10000
OrchardCore__CrestApps__Reports__Builder__Limits__JoinKeyBatchSize=500
OrchardCore__CrestApps__Reports__Builder__Versions__MaxVersions=50
```

| Setting | Default | What it limits |
| --- | --- | --- |
| `MaxRowsPerDataSet` | `50000` | Rows read from one data set. A report that hits it shows a warning. |
| `MaxJoinedRows` | `250000` | Rows the joins may produce. |
| `MaxResultRows` | `10000` | Result rows kept after sorting. |
| `MaxFilterOptions` | `500` | Values listed by a drop-down filter. |
| `MaxJoinKeys` | `10000` | Distinct keys a join sends to the data set it joins; with more, that data set is read in full. |
| `JoinKeyBatchSize` | `500` | Keys sent in one read of a joined data set. |
| `Versions:MaxVersions` | `50` | Published versions kept per report; the oldest are deleted. `0` keeps them all. |

## Adding a data source

Implement `IReportDataSource` and register it as a scoped service. The builder lists it as soon as the feature that registers it is enabled.

```csharp
public sealed class InvoicesReportDataSource : IReportDataSource
{
    public string Name => "Invoices";

    public LocalizedString DisplayName => S["Invoices"];

    public LocalizedString Description => S["Invoices from the billing system."];

    public async Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        // List only what context.User may read.
        return await CanReadAsync(context.User)
            ? [new ReportDataSetDescriptor("Invoice", S["Invoice"])]
            : [];
    }

    public async Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (dataSet != "Invoice" || !await CanReadAsync(context.User))
        {
            return null; // The engine refuses the data set.
        }

        return new ReportDataSetSchema
        {
            DataSet = new ReportDataSetDescriptor("Invoice", S["Invoice"]),
            Fields =
            [
                new ReportFieldDescriptor("Number", S["Number"], ReportDataType.Text) { IsIdentifier = true },
                new ReportFieldDescriptor("CustomerId", S["Customer"], ReportDataType.Text) { IsIdentifier = true },
                new ReportFieldDescriptor("Amount", S["Amount"], ReportDataType.Decimal),
                new ReportFieldDescriptor("IssuedUtc", S["Issued"], ReportDataType.DateTime),
            ],
        };
    }

    public async Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        // Read query.Fields only, at most query.MaxRows rows (set Truncated when there are more). Applying
        // query.Conditions is optional: never drop a row the full filter would keep.
    }
}
```

```csharp
services.AddScoped<IReportDataSource, InvoicesReportDataSource>();
```

The contract:

| Member | Rule |
| --- | --- |
| `Name`, data set names, field names | Stable: designs store them. Field names may contain dots; aliases cannot. |
| `GetDataSetsAsync`, `GetSchemaAsync` | Return only what `context.User` may read; `GetSchemaAsync` returns `null` otherwise. This is the security boundary. |
| Values | One CLR type per `ReportDataType`: `string`, `long`, `decimal`, `bool`, `DateTime` (date only, no time zone) and `DateTime` in UTC. Use `ReportDataValues.Coerce` to normalize. |
| `IsIdentifier` | Marks keys, which the builder suggests when joining. |
| `References` | On a field, the data sets (and their key field) it points to; on a data set, every data set its fields point to. The builder joins on them automatically and lists referenced data sets under **Related**. Declare them only when the source knows them without reading data, for example from definitions. |
| `ReportDataSourceContext.Properties` | A bag shared by one run, for example to detect recursion. |

A repository development skill under `.agents/skills/crestapps-report-data-source` walks through every step.

## Content items

When `OrchardCore.Contents` is enabled, the **Content items** data source offers one data set per content type the user may view, reading published items with YesSql. Types with the `Widget` stereotype are left out: widgets are pieces of pages, not records.

| Field | Name |
| --- | --- |
| Metadata | `ContentItemId` (identifier), `ContentItemVersionId`, `DisplayText`, `ContentType`, `Owner` (identifier), `Author`, `CreatedUtc`, `ModifiedUtc`, `PublishedUtc`, `Published` |
| Content field | `{PartName}.{FieldName}`, with `.{Suffix}` for extra values |
| Part data | `TitlePart.Title`, `AutoroutePart.Path`, `ContainedPart.ListContentItemId`, and other common parts |

For example, a `Customer` type with an `Email` text field and an `Order` type with a `Customer` content picker give `Customer.Email` and `Order.Customer` (the first picked id, an identifier), `Order.Customer.ContentItemIds` and `Order.Customer.DisplayText`. Join the order to the customer on `Order.Customer` = `ContentItemId`.

### Relationships

Relationships come from the content definitions, so nothing about a site's types is hard-coded:

| Relationship | Declared by | Field that points to the other data set |
| --- | --- | --- |
| A content picker | The picker's **Displayed content types** setting | `{PartName}.{FieldName}` → `ContentItemId` of each listed type |
| A list | A `ListPart` whose **Contained content types** include the type | `ContainedPart.ListContentItemId` (**Container ID**) → `ContentItemId` of the list's type |
| The owner | Every content item | `Owner` → `Users.UserId` |
| A user picker | Every user picker field | `{PartName}.{FieldName}` → `Users.UserId` |

For example, when an `Account` type has a `ListPart` that contains `Contact`, the `Contact` data set gets a **Container ID** field, and adding `Contact` to a report that has `Account` joins them on it. A content picker that allows any type declares no relationship, because the builder cannot know which type is picked.

Built-in providers cover the Orchard Core text, numeric, boolean, date, date-time, time, HTML, Markdown, multi-text, link, content picker, user picker, media, taxonomy, localization set and YouTube fields, and the CrestApps phone field. An unknown field type is read as text from its `Text` or `Value` property. Date range filters on the content item index columns are passed down to the query; text filters are not, because database collations may compare case differently from the engine.

### Extending the content items source

- **A content field type:** implement `IContentReportFieldProvider` (keyed by `FieldType`; the provider registered last wins), or register a one-property provider:

  ```csharp
  services.AddContentReportFieldProvider("RatingField", ReportDataType.Integer, ContentReportValueMode.Single, "Value");
  ```

- **A content part:** implement `IContentReportPartProvider`. Every provider is asked about every part; their fields are combined.
- A field derives from `ContentReportField`. Override `PrepareAsync` for work done once per query (it runs only when a report uses the field), and `GetValue` to read the value.

## Queries

When `OrchardCore.Queries` is enabled, the **Queries** data source offers every saved query (SQL, Lucene, Elasticsearch, or any other query source) as a data set, so a query someone wrote once can be joined, filtered and charted like any other data.

- A query is listed and read only for a principal allowed to execute it: `ExecuteApi_{QueryName}`, or **Execute Queries API (all)**.
- A query declares no columns, so its fields are found in its results. Each result item is written as JSON; nested objects become dotted fields (`TitlePart.Title`, `Customer.Balance.Value`), lists of plain values are joined with commas, and each field's type is inferred from the first 200 items (whole numbers, decimals, booleans, ISO dates and date-times, otherwise text). Fields ending in `Id` are offered as join keys.
- A query that returns content items (**Return content items**) gives its content item properties and part and field values the same way.
- The query runs once per report run, without parameters, so write queries whose parameters have defaults. Its own limits apply first; the report then keeps at most `MaxRowsPerDataSet` rows.
- A query that fails stops the report with the query's name and error message, and the failure is logged.

## Users

The **Users** data source is part of the builder. It is listed only for principals with the **View Users** permission and offers two data sets:

| Data set | Rows | Fields |
| --- | --- | --- |
| **Users** | One per user account | `UserId` (identifier), `UserName`, `Email`, `EmailConfirmed`, `PhoneNumber`, `PhoneNumberConfirmed`, `IsEnabled`, `TwoFactorEnabled`, `IsLockoutEnabled`, `LockoutEndUtc`, `AccessFailedCount`, `Roles` (comma separated), and `Properties.*` for the custom user settings stored with the users |
| **User roles** | One per user and role | `UserId` (references **Users**), `UserName`, `Role` |

Password hashes, security stamps, tokens and external login keys are never exposed. The `Properties.*` fields are found the same way as query fields: nested objects become dotted names and types are inferred from the stored values. Content item owners and user picker fields reference **Users**, so they join to it automatically.

## Business records

Modules expose their own records as data sources, registered only when the Report Builder is enabled with the feature that stores them. Each data set reads its newest records first, up to `MaxRowsPerDataSet`, and narrows the read to the date range a report's filters put on its main date. Identifier fields reference the data sets they point to, so related data sets join automatically: an activity's assigned user joins **Users**, its disposition joins **Dispositions**, an interaction's activity joins **Activities**, and so on across sources.

| Source | Data sets | Permission |
| --- | --- | --- |
| **Omnichannel** | **Activities** (tasks, calls, messages and the dialer's attempts, with disposition and campaign names and minutes to complete), **Dispositions** (with their **Outcome**, which groups them), **Campaigns**, **Campaign groups**, **Activity batches** (bulk loads and their skip counts) | View Omnichannel reports |
| **Contact Center** | **Interactions** (with wait, talk and wrap-up seconds and the dialer's `Dialer.*` fields: attempt, outcome, answering machine result, pacing), **Interaction events**, **Call sessions**, **Call quality**, **Call recordings**, **Callback requests**, **Dialer profiles**, **Queues**, **Queue groups**, **Queue items**, **Agent profiles**, **Agent sessions**, **Shared voicemails** | View Contact Center reports; call recordings also need Listen to all call recordings, and shared voicemails need the shared voicemail permission and show only the queues the reader may answer |
| **AI chat** | **Chat sessions** (with the AI profile name), **Chat session metrics** (messages, handle time, tokens, ratings, resolution, conversion) | View AI chat analytics |
| **Messaging** | **Conversations** (assignment, unread count, first response time), **Messages** (direction, delivery status, length) | View all messaging conversations |

A dialer attempt is an activity plus the interaction that placed the call, so dialer reports join **Activities** to **Interactions** on the activity ID. Agent fields named `AgentId` hold agent profile IDs and join **Agent profiles**; fields holding user IDs join **Users**. Secrets, storage locations, IP addresses and message text are never exposed.

Modules add sources the same way with `ReportRecordDataSource` and `ReportRecordDataSet<T>` from `CrestApps.OrchardCore.Reports.Abstractions`: declare the fields with the function that reads each from a record, the permission check, and how records are loaded; `ReportDateRange` reads the date range of a report's filters.

## Formula reference

Fields are written in square brackets: `[alias.Field]` for a data set field, `[Name]` for a calculated field, and `[$count]` for the built-in row count. Text is quoted with single or double quotes, numbers use a dot, and `TRUE`, `FALSE` and `NULL` are keywords. Operators, from the lowest precedence: `OR ||`, `AND &&`, `NOT !`, comparisons (`= == != <> < <= > >=`), `&` (joins text), `+ -`, `* / %`, and unary `- +`. `+` joins text when either side is text; adding a number to a date adds days, and subtracting dates gives days.

A missing operand gives a missing result, dividing by zero gives a missing result, and integer arithmetic that overflows continues in decimals. A formula with an aggregate function is evaluated once per group and may not mix aggregated values with row-level fields.

| Function | Category | What it does |
| --- | --- | --- |
| `AVERAGE(number)` | Aggregate | Same as AVG. |
| `AVG(number)` | Aggregate | Averages the values of the group. |
| `COUNT([value])` | Aggregate | Counts the rows of the group, or the rows where the value is present. |
| `COUNT([value])` | Aggregate | Counts the rows of the group, or the rows where the value is present. |
| `COUNTD(value)` | Aggregate | Counts the distinct values of the group. |
| `MAX(value)` | Aggregate | Returns the largest value of the group. |
| `MEDIAN(number)` | Aggregate | Returns the middle value of the group. |
| `MIN(value)` | Aggregate | Returns the smallest value of the group. |
| `SUM(number)` | Aggregate | Adds the values of the group. |
| `DATE(year, month, day) or DATE(value)` | Date | Builds a date from its parts, or converts a value to a date. |
| `DATEADD('part', number, date)` | Date | Adds a number of years, quarters, months, weeks, days, hours, minutes, or seconds to a date. |
| `DATEDIFF('part', start, end)` | Date | Counts the years, quarters, months, weeks, days, hours, minutes, or seconds from start to end. |
| `DATETIME(value)` | Date | Converts a value to a date-time. |
| `DATETRUNC('part', date)` | Date | Truncates a date to the start of its year, quarter, month, week, day, hour, or minute. |
| `DAY(date)` | Date | Returns the day of the month of a date. |
| `HOUR(date)` | Date | Returns the hour of a date-time (0 to 23). |
| `MINUTE(date)` | Date | Returns the minute of a date-time (0 to 59). |
| `MONTH(date)` | Date | Returns the month of a date (1 to 12). |
| `NOW()` | Date | Returns the current date and time. |
| `QUARTER(date)` | Date | Returns the quarter of a date (1 to 4). |
| `TODAY()` | Date | Returns the current date. |
| `WEEK(date)` | Date | Returns the ISO week number of a date. |
| `WEEKDAY(date)` | Date | Returns the day of the week of a date, from 1 (Monday) to 7 (Sunday). |
| `YEAR(date)` | Date | Returns the year of a date. |
| `COALESCE(value1, value2, ...)` | Logical | Returns the first value that is not missing. |
| `IF(condition, then, [else])` | Logical | Returns one value when the condition is true and another when it is not. |
| `IFNULL(value, fallback)` | Logical | Returns the fallback when the value is missing. |
| `IFS(condition1, value1, condition2, value2, ..., [else])` | Logical | Returns the value of the first condition that is true. |
| `IIF(condition, then, else)` | Logical | Same as IF. |
| `IN(value, option1, option2, ...)` | Logical | Returns true when the value equals any option. |
| `ISBLANK(value)` | Logical | Returns true when the value is missing or empty text. |
| `ISNULL(value)` | Logical | Returns true when the value is missing. |
| `NOT(condition)` | Logical | Returns true when the condition is not true. |
| `SWITCH(value, match1, result1, match2, result2, ..., [else])` | Logical | Returns the result paired with the first match of the value. |
| `ABS(number)` | Number | Returns the absolute value of a number. |
| `CEILING(number)` | Number | Rounds a number up to a whole number. |
| `FLOOR(number)` | Number | Rounds a number down to a whole number. |
| `GREATEST(value1, value2, ...)` | Number | Returns the largest of the values. |
| `INTEGER(value)` | Number | Converts a value to a whole number, dropping any fraction. |
| `LEAST(value1, value2, ...)` | Number | Returns the smallest of the values. |
| `MOD(number, divisor)` | Number | Returns the remainder of a division. |
| `NUMBER(value)` | Number | Converts a value to a decimal number. |
| `POWER(number, exponent)` | Number | Raises a number to a power. |
| `ROUND(number, [decimals])` | Number | Rounds a number to a number of decimals (0 by default). |
| `SIGN(number)` | Number | Returns -1, 0, or 1 for a negative, zero, or positive number. |
| `SQRT(number)` | Number | Returns the square root of a number. |
| `CONCAT(value1, value2, ...)` | Text | Joins values into one text. Missing values are skipped. |
| `CONTAINS(text, search)` | Text | Returns true when the text contains the search text, ignoring case. |
| `ENDSWITH(text, search)` | Text | Returns true when the text ends with the search text, ignoring case. |
| `FIND(text, search)` | Text | Returns the position of the search text (starting at 1), or 0 when it is not found. |
| `LEFT(text, count)` | Text | Returns the first characters of the text. |
| `LEN(text)` | Text | Returns the number of characters in the text. |
| `LOWER(text)` | Text | Converts text to lower case. |
| `MID(text, start, [count])` | Text | Returns characters from the middle of the text. The first character is at position 1. |
| `PROPER(text)` | Text | Capitalizes the first letter of each word. |
| `REPLACE(text, find, replacement)` | Text | Replaces every occurrence of a text, ignoring case. |
| `RIGHT(text, count)` | Text | Returns the last characters of the text. |
| `SPLIT(text, separator, index)` | Text | Splits the text and returns the part at the index (starting at 1). |
| `STARTSWITH(text, search)` | Text | Returns true when the text starts with the search text, ignoring case. |
| `SUBSTRING(text, start, [count])` | Text | Same as MID. |
| `TEXT(value, [format])` | Text | Converts a value to text, optionally with a .NET format such as 'N2' or 'yyyy-MM'. |
| `TRIM(text)` | Text | Removes leading and trailing spaces. |
| `UPPER(text)` | Text | Converts text to upper case. |

## Recipes and deployment

Add the **Designed Reports and Views** step to a deployment plan to export every designed report and view. Each item is written with its owner's user name, and share links are never exported. The `ReportDesigns` recipe step imports them: items are matched by `itemId` and replaced, or created with that id, and the owner is matched by `OwnerUserName` when a user with that name exists.

```json
{
  "steps": [
    {
      "name": "ReportDesigns",
      "Views": [
        {
          "itemId": "4z1f5b0c6t0m9z2k8w3d7r5q1v",
          "displayText": "Revenue by region",
          "query": { "dataSets": [], "columns": [] },
          "OwnerUserName": "admin"
        }
      ],
      "Reports": [
        {
          "itemId": "4z1f5b0c6t0m9z2k8w3d7r5q1r",
          "displayText": "Top customers",
          "category": "Sales",
          "showInAdminMenu": true,
          "query": { "dataSets": [], "columns": [] },
          "visuals": [],
          "sharedRoles": [ "Sales" ],
          "OwnerUserName": "admin"
        }
      ]
    }
  ]
}
```

Enum values are written by name (for example `"aggregate": "Sum"` or `"type": "Chart"`). Export a plan from a site where the report works to get the full query shape.
