---
sidebar_label: Add Your Own Data Source
title: Add Your Own Report Data Source
description: Extend the Report Builder with your own data sources and data sets - record-backed sources, sources for external systems, grouping in the source, join keys, relationships, and content field providers.
user_manual:
  - user-manual/report-builder/data-sources
---

Any module can add data to the Report Builder. A **data source** answers only three questions: which data sets exist for the principal, which typed fields each one has, and which rows it holds. The engine does everything else for every source: joins, formulas, transforms, filters, grouping and aggregation, sorting, limits, time zones, viewer filters and their option lists, visuals, export, sharing and authorization.

This video shows a data source added by a module and how its data sets appear in the builder.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-custom-data-sources.jpg" aria-label="Video: a module's own data source and its data sets in the Report Builder">
  <source src="/img/docs/report-builder-custom-data-sources.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-custom-data-sources.vtt" srcLang="en" label="English" default />
</video>

## Choose an approach

| You want to report on | Build | Reference |
| --- | --- | --- |
| Records your module stores, such as YesSql documents or catalog entries | A `ReportRecordDataSource` with one `ReportRecordDataSet<T>` per record type | `CrestApps.OrchardCore.Reports.Abstractions` |
| Such records, grouped in the database for large data | The same, with `IReportAggregateDataSet` and `ReportIndexAggregator` | Also `CrestApps.OrchardCore.Reports.Core` |
| An external system whose data sets or fields are found at run time, such as a database, a search index or a REST API | `IReportDataSource` | `CrestApps.OrchardCore.Reports.Abstractions` |
| A custom content field type or content part | An `IContentReportFieldProvider` or `IContentReportPartProvider` for the built-in **Content items** source | `CrestApps.OrchardCore.Reports` |
| Data that people can prepare in the browser | Nothing: a [reusable view](../../user-manual/report-builder/views.md) | |

The data source types live in the `CrestApps.OrchardCore.Reports.DataSources` namespace; `ReportsConstants` (feature IDs and the built-in source names) is in `CrestApps.OrchardCore.Reports`.

## Where the code goes

- **A module exposing its own records** (activities, interactions, chat sessions...) keeps the source in that module, in a startup marked with the module's feature and `[RequireFeatures(ReportsConstants.BuilderFeature)]`, so the Reports module never depends on it and the source appears only when both are enabled. The Omnichannel, Contact Center, AI chat and Messaging sources are built this way.
- **A source that wraps an Orchard Core feature** (like **Content items** and **Queries**) needs no feature of its own: a startup marked `[Feature(ReportsConstants.BuilderFeature)]` and `[RequireFeatures("OrchardCore.X")]` registers it.
- **A source for an external system** goes in its own module, with a feature that depends on `ReportsConstants.BuilderFeature` in the `Reporting` category. Connection settings belong in configuration (for example `CrestApps:Reports:{Source}`), never in a design.

Register a source as a scoped `IReportDataSource`. The builder lists it as soon as the feature that registers it is enabled.

## Record-backed data sources

For records kept in a store, derive from `ReportRecordDataSet<TRecord>` and `ReportRecordDataSource` instead of implementing `IReportDataSource` by hand. The base classes check access on every call, keep only the fields the report uses, read one row more than the limit to set `Truncated`, convert each value to its field's type (enums become their name, unspecified and offset date-times become UTC), and add each field's references to its data set.

This example exposes the invoices of a billing module:

```csharp
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using YesSql;
using YesSql.Services;

public sealed class InvoicesReportDataSet : ReportRecordDataSet<Invoice>
{
    public const string Name = "Invoices";

    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;

    public InvoicesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<InvoicesReportDataSet> S)
        : base(new ReportDataSetDescriptor(Name, S["Invoices"], S["One row per invoice."]) { DefaultDateField = "IssuedUtc" })
    {
        _session = session;
        _authorizationService = authorizationService;

        var invoice = S["Invoice"].Value;
        var people = S["People"].Value;

        AddField("InvoiceId", S["Invoice ID"], ReportDataType.Text, record => record.InvoiceId, invoice, isIdentifier: true);
        AddField("Number", S["Number"], ReportDataType.Text, record => record.Number, invoice);
        AddField("Status", S["Status"], ReportDataType.Text, record => record.Status, invoice);
        AddField("Amount", S["Amount"], ReportDataType.Decimal, record => record.Amount, invoice);
        AddField("IssuedUtc", S["Issued"], ReportDataType.DateTime, record => record.IssuedUtc, invoice);
        AddField("PaidUtc", S["Paid"], ReportDataType.DateTime, record => record.PaidUtc, invoice);

        // The customer is a content item of the Customer type; the account manager is a user.
        AddField(
            "CustomerId",
            S["Customer ID"],
            ReportDataType.Text,
            record => record.CustomerContentItemId,
            people,
            isIdentifier: true,
            new ReportFieldReference(ReportsConstants.ContentsDataSource, "Customer", "ContentItemId"));
        AddField(
            "AccountManagerId",
            S["Account manager (user ID)"],
            ReportDataType.Text,
            record => record.AccountManagerUserId,
            people,
            isIdentifier: true,
            new ReportFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));
    }

    // The fields LoadAsync filters exactly when the engine sends join keys.
    protected override IEnumerable<string> KeyFilterableFields => ["InvoiceId", "CustomerId"];

    public override async Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, BillingPermissions.ViewInvoiceReports);
    }

    protected override async Task<IEnumerable<Invoice>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        var invoices = _session.Query<Invoice, InvoiceIndex>();

        // Join keys: read only the invoices a joined report can match. An empty list means none can match.
        if (ReportJoinKeys.For(query.Conditions, "InvoiceId") is { } invoiceIds)
        {
            if (invoiceIds.Count == 0)
            {
                return [];
            }

            var values = invoiceIds.ToArray();
            invoices = invoices.Where(index => index.InvoiceId.IsIn(values));
        }

        if (ReportJoinKeys.For(query.Conditions, "CustomerId") is { } customerIds)
        {
            if (customerIds.Count == 0)
            {
                return [];
            }

            var values = customerIds.ToArray();
            invoices = invoices.Where(index => index.CustomerContentItemId.IsIn(values));
        }

        // The period the report's filters put on the main date, never narrower than the filters.
        var (from, to) = ReportDateRange.For(query.Conditions, "IssuedUtc");

        if (from.HasValue)
        {
            var earliest = from.Value;
            invoices = invoices.Where(index => index.IssuedUtc >= earliest);
        }

        if (to.HasValue)
        {
            var latest = to.Value;
            invoices = invoices.Where(index => index.IssuedUtc <= latest);
        }

        // Newest first, at most `take` records.
        return await invoices
            .OrderByDescending(index => index.IssuedUtc)
            .ThenByDescending(index => index.DocumentId)
            .Take(take)
            .ListAsync(cancellationToken);
    }
}
```

The data source lists its data sets:

```csharp
public sealed class BillingReportDataSource : ReportRecordDataSource
{
    public const string SourceName = "Billing";

    private readonly IReportRecordDataSet[] _dataSets;
    private readonly IStringLocalizer S;

    public BillingReportDataSource(
        InvoicesReportDataSet invoices,
        IStringLocalizer<BillingReportDataSource> stringLocalizer)
    {
        _dataSets = [invoices];
        S = stringLocalizer;
    }

    public override string Name => SourceName;

    public override LocalizedString DisplayName => S["Billing"];

    public override LocalizedString Description => S["Invoices of the billing module."];

    protected override IEnumerable<IReportRecordDataSet> DataSets => _dataSets;
}
```

And a startup registers both when the Report Builder is enabled:

```csharp
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<InvoicesReportDataSet>()
            .AddScoped<IReportDataSource, BillingReportDataSource>();
    }
}
```

`AddField` returns the field's `ReportFieldDescriptor`, so you can set its `Description` (a hint shown in the builder). Override `DefaultDateField` instead of setting it on the descriptor when it depends on the data set.

:::note[Two `ReportDateRange` types]
`CrestApps.OrchardCore.Reports.DataSources.ReportDateRange` reads the date range of a query's conditions. `CrestApps.OrchardCore.Reports.Models.ReportDateRange` is the reporting period of the built-in date-range filter of the Reports area. A file that imports both namespaces must qualify the one it uses.
:::

## Sources for external systems

Implement `IReportDataSource` when the data sets or fields are not known in advance, or the rows do not come from records you load. This example reads tickets from a help desk API:

```csharp
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

public sealed class HelpDeskReportDataSource : IReportDataSource
{
    private const string TicketsDataSet = "Tickets";

    private readonly IHelpDeskClient _client;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStringLocalizer S;

    public HelpDeskReportDataSource(
        IHelpDeskClient client,
        IAuthorizationService authorizationService,
        IStringLocalizer<HelpDeskReportDataSource> stringLocalizer)
    {
        _client = client;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public string Name => "HelpDesk";

    public LocalizedString DisplayName => S["Help desk"];

    public LocalizedString Description => S["Tickets of the help desk."];

    public async Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        // List only what context.User may read.
        return await CanReadAsync(context) ? [Describe()] : [];
    }

    public async Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (dataSet != TicketsDataSet || !await CanReadAsync(context))
        {
            return null; // The engine refuses the data set.
        }

        return new ReportDataSetSchema
        {
            DataSet = Describe(),
            Fields = GetFields(),
        };
    }

    public async Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        // Check again: QueryAsync is the last line of defense.
        if (query.DataSet != TicketsDataSet || !await CanReadAsync(query.Context))
        {
            return new ReportDataTable();
        }

        // Return at least the fields the report uses; skip the work of the others.
        var fields = GetFields()
            .Where(field => query.Fields is null || query.Fields.Count == 0 || query.Fields.Contains(field.Name))
            .ToList();

        // Optional push-down: ask the API for the period the filters allow, newest first, one more than the limit.
        var (from, to) = ReportDateRange.For(query.Conditions, "CreatedUtc");
        var tickets = await _client.GetTicketsAsync(from, to, query.MaxRows + 1, cancellationToken);
        var table = new ReportDataTable { Fields = fields };

        foreach (var ticket in tickets)
        {
            if (table.Rows.Count >= query.MaxRows)
            {
                table.Truncated = true;

                break;
            }

            table.Rows.Add(fields.Select(field => ReportDataValues.Coerce(Read(ticket, field.Name), field.DataType)).ToArray());
        }

        return table;
    }

    private ReportDataSetDescriptor Describe()
    {
        return new ReportDataSetDescriptor(TicketsDataSet, S["Tickets"], S["One row per help desk ticket."])
        {
            DefaultDateField = "CreatedUtc",
        };
    }

    private List<ReportFieldDescriptor> GetFields()
    {
        return
        [
            new ReportFieldDescriptor("TicketId", S["Ticket ID"], ReportDataType.Text) { IsIdentifier = true },
            new ReportFieldDescriptor("Subject", S["Subject"], ReportDataType.Text),
            new ReportFieldDescriptor("Priority", S["Priority"], ReportDataType.Text),
            new ReportFieldDescriptor("RequesterEmail", S["Requester email"], ReportDataType.Text),
            new ReportFieldDescriptor("CreatedUtc", S["Created"], ReportDataType.DateTime),
            new ReportFieldDescriptor("ResolvedUtc", S["Resolved"], ReportDataType.DateTime),
            new ReportFieldDescriptor("ReopenCount", S["Times reopened"], ReportDataType.Integer),
        ];
    }

    // Date-times must be UTC; the engine shows them in the tenant time zone.
    private static object Read(HelpDeskTicket ticket, string field)
    {
        return field switch
        {
            "TicketId" => ticket.Id,
            "Subject" => ticket.Subject,
            "Priority" => ticket.Priority,
            "RequesterEmail" => ticket.Requester?.Email,
            "CreatedUtc" => ticket.CreatedAt.UtcDateTime,
            "ResolvedUtc" => ticket.ResolvedAt?.UtcDateTime,
            "ReopenCount" => ticket.ReopenCount,
            _ => null,
        };
    }

    private async Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, HelpDeskPermissions.ViewHelpDeskReports);
    }
}
```

```csharp
services.AddScoped<IReportDataSource, HelpDeskReportDataSource>();
```

A source whose data sets come from somewhere else, such as the tables of a database, lists them in `GetDataSetsAsync` and describes each in `GetSchemaAsync`. A source that has to read data to learn its fields, like the **Queries** source, can sample rows and infer each field's type with `ReportDataValues.InferType`. A source that cannot describe a data set, for example because an upstream definition is broken, may throw `ReportQueryException` (`CrestApps.OrchardCore.Reports.Designer`, in `CrestApps.OrchardCore.Reports.Core`) with a message the builder shows.

## The contract

| Member | Rule |
| --- | --- |
| `Name` | Stable technical name. Designs store it (`ReportDataSetReference.Source`), so never rename it. |
| `DisplayName`, `Description` | Shown in the builder's data source picker. |
| `GetDataSetsAsync(context)` | The data sets `context.User` may read. When `context.User` is `null`, return nothing. |
| `GetSchemaAsync(dataSet, context)` | The fields of a data set, or `null` when it does not exist or the principal may not read it. The engine reads a data set only after this returns a schema: this is the security boundary. |
| `QueryAsync(query)` | Rows of `query.DataSet` with at least `query.Fields` (every field when the set is empty), at most `query.MaxRows` rows, newest first; set `Truncated` when there are more (read `MaxRows + 1`). Check access again, and honor cancellation. |
| Data set and field names | Stable: designs store them. Field names may contain dots (`Part.Field.Suffix`) and are referenced as `alias.FieldName`. |
| `ReportDataSetDescriptor.DefaultDateField` | The data set's main date. A new report starts filtered on its last 30 days, and the source can read only that period. |
| `ReportDataSetDescriptor.Group`, `ReportFieldDescriptor.Group` | The group a data set or field is listed under in the builder. |
| `ReportFieldDescriptor.IsIdentifier` | Marks keys and foreign keys, which the builder suggests when joining and never sums by default. |
| `ReportFieldDescriptor.IsKeyFilterable` | The source filters join keys on this field exactly; see [Join keys](#join-keys). |
| `References` | On a field, the data sets (and their key field) its values point to; on a data set, every data set its fields point to. See [Relationships](#relationships). |
| `ReportDataSourceContext.Properties` | A bag shared by one run, for example to detect recursion. |

### Values

Each row is an `object[]` aligned with `ReportDataTable.Fields`. Each `ReportDataType` maps to one CLR type:

| `ReportDataType` | CLR type |
| --- | --- |
| `Text` | `string` |
| `Integer` | `long` |
| `Decimal` | `decimal` |
| `Boolean` | `bool` |
| `Date` | `DateTime`, date only, no time zone; never shifted |
| `DateTime` | `DateTime` in UTC; the engine converts it to the tenant time zone |

Use `ReportDataValues.Coerce(value, dataType)` to normalize raw values; it also unwraps `System.Text.Json` nodes and parses text with the invariant culture. A missing value is `null`.

### Conditions you may push down

`query.Conditions` carries the report's row filters as `ReportDataCondition`s (field, `ReportFilterOperator`, typed values, with `DateTime` values already in UTC). Relative date filters (in the last or next N days) arrive as a date range. Applying a condition is **optional** and must never drop a row the engine's own filter would keep, because the engine applies every filter again after reading:

- Ignoring every condition is always correct.
- `ReportDateRange.For(conditions, field)` returns inclusive bounds that never drop a matching row; apply them to an indexed date column.
- Text compares ignoring case in the engine, so do not push text equality to a case-sensitive collation.
- Conditions are offered only for data sets that no outer join can fill with empty values.

## Join keys

When a report joins a data set with an inner or left join, the engine can send the distinct keys of the rows before it, so the joined data set reads only the records that can match instead of its newest records up to the row limit. To take part:

1. Mark the field `IsKeyFilterable` (with `ReportRecordDataSet<T>`, list it in `KeyFilterableFields`). Only `Text` and `Integer` fields joined to a field of the same type are narrowed.
2. Read the keys with `ReportJoinKeys.For(query.Conditions, field)`: `null` when no join key condition applies to the field, an empty list when no record can match, or the values as text, with their case kept.
3. Apply them **exactly**: return every record whose value is one of the keys, and no other. A source that cannot must not set the flag.

The keys arrive as an `In` condition with `IsJoinKey` set, in batches of `JoinKeyBatchSize`, up to `MaxJoinKeys` keys; with more keys, the joined data set is read in full (see [Configuration](index.md#configuration)).

## Relationships

A `ReportFieldReference(source, dataSet, field)` on a field says that its values identify records of another data set, such as an invoice's customer:

- The builder joins on it automatically when both data sets are in a report, lists the referenced data sets under **Related** in **Add data set**, and suggests the join.
- Reference other modules' data sets by their stable names, such as `new ReportFieldReference("ContactCenter", "DialerProfiles", "ItemId")`, or the users data set with `ReportsConstants.UsersDataSource`, `ReportsConstants.UsersDataSet` and `ReportsConstants.UserIdField`. A content type is `new ReportFieldReference(ReportsConstants.ContentsDataSource, "{ContentType}", "ContentItemId")`.
- A reference to a source that is not enabled is simply unused.
- Declare references only when the source knows them without reading data, for example from definitions.

## Grouping in the data source

A report over one data set that groups by fields or date periods and counts, sums, averages, or takes the smallest or largest values can be answered by its source, so the engine reads a few groups instead of every row (see [Large data](large-data.md#grouping-in-the-data-source)).

For YesSql records, implement `IReportAggregateDataSet` on the data set and hand the query to `ReportIndexAggregator` (namespace `CrestApps.OrchardCore.Reports.Designer.DataSources`, in `CrestApps.OrchardCore.Reports.Core`), which answers it with one `GROUP BY` statement over the map index table:

```csharp
using CrestApps.OrchardCore.Reports.Designer.DataSources;

public sealed class InvoicesReportDataSet : ReportRecordDataSet<Invoice>, IReportAggregateDataSet
{
    // The report fields whose values the index copies unchanged, mapped to their index column.
    private IReadOnlyDictionary<string, string> _aggregateColumns;

    // ...constructor, fields, CanReadAsync and LoadAsync as above...

    public Task<ReportAggregateTable> AggregateAsync(ReportAggregateQuery query, CancellationToken cancellationToken)
    {
        _aggregateColumns ??= ReportIndexAggregator.MapByName<InvoiceIndex>(Fields.Select(field => field.Name));

        return ReportIndexAggregator.AggregateAsync<InvoiceIndex>(_session, null, query, _aggregateColumns, cancellationToken);
    }
}
```

`ReportRecordDataSource` passes the query to a readable data set that implements `IReportAggregateDataSet`, after checking access. `MapByName` maps each field to the index column of the same name; pass your own dictionary when the names differ, and map only fields whose index column holds exactly the value the field reads. Pass the index's collection as the second argument when the records are stored in one.

`ReportIndexAggregator` declines (returns `null`) whenever it cannot answer exactly the way the engine would: a field that is not a mapped column, a text comparison other than equality, equality on non-ASCII text, an empty-text test on a text column, or more groups than `MaxGroups`. Enum columns are compared and grouped by name, and date buckets are written as a `CASE` expression over UTC boundaries.

To group anything else, implement `IReportAggregateDataSource` on the source (or `IReportAggregateDataSet` on a record data set) yourself:

- `query.Groups` lists the fields to group by. A group with `Boundaries` is a date bucketed by those ascending UTC boundaries: its value is the zero-based bucket index as a `long`, and records outside every bucket or without a value fall in a `null` group.
- `query.Measures` lists what to compute per group: `Count` (of the field's values, or of all records when `Field` is `null`), `Sum`, `Min` and `Max`.
- Each result row holds the group values, then the measures, in that order; values use the CLR types above, with date-times in UTC.
- `query.Conditions` must be applied **exactly**: comparisons are exact, text ignores case, `NotEquals` and `NotIn` keep records whose value is empty. Answer only with records `query.Context.User` may read.
- Return `null` for anything you cannot answer exactly, or for more groups than `query.MaxGroups`. The engine then reads rows as usual.

## Security

- When a saved report runs, `context.User` is the **report owner's** principal, not the viewer's. Filter by that principal exactly as you would for the owner. While a report is designed, it is the designer.
- Never fall back to "everything" when the principal is `null` or cannot be authorized: return no data sets, a `null` schema, and no rows.
- Check access in `GetDataSetsAsync`, `GetSchemaAsync` and again in `QueryAsync` (`ReportRecordDataSource` does this for you).
- Never expose secrets, password hashes, tokens, message bodies, IP addresses or storage locations.

See [Permissions and security](permissions.md).

## Content fields and parts

To report on a custom content field type or content part, extend the built-in **Content items** source instead of writing a new one. These types are in the `CrestApps.OrchardCore.Reports` module (namespaces `CrestApps.OrchardCore.Reports.Contents`, `.Contents.Services` and `.Contents.Models`), so the module that registers them references it and requires `ReportsConstants.BuilderFeature` and `OrchardCore.Contents`.

A field type whose value is one JSON property of the field needs one line:

```csharp
services.AddContentReportFieldProvider("RatingField", ReportDataType.Integer, ContentReportValueMode.Single, "Value");
```

`ContentReportValueMode.Single` reads one value, `First` the first entry of an array, and `Join` the non-empty entries of an array joined with commas. The property names are tried in order.

A field type with several values implements `IContentReportFieldProvider`. The first field uses the base name `{PartName}.{FieldName}`; extra values add a suffix:

```csharp
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;

public sealed class AddressFieldReportProvider : IContentReportFieldProvider
{
    private readonly IStringLocalizer S;

    public AddressFieldReportProvider(IStringLocalizer<AddressFieldReportProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string FieldType => "AddressField";

    public IEnumerable<ContentReportField> GetFields(ContentReportFieldContext context)
    {
        return
        [
            context.CreateElementField(null, context.DisplayName, ReportDataType.Text, ContentReportValueMode.Single, "City"),
            context.CreateElementField("PostalCode", S["{0} (postal code)", context.DisplayName], ReportDataType.Text, ContentReportValueMode.Single, "PostalCode"),
            context.CreateElementField("Country", S["{0} (country)", context.DisplayName], ReportDataType.Text, ContentReportValueMode.Single, "CountryCode"),
        ];
    }
}
```

```csharp
[RequireFeatures(ReportsConstants.BuilderFeature, "OrchardCore.Contents")]
public sealed class ContentReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentReportFieldProvider, AddressFieldReportProvider>();
    }
}
```

- Providers are keyed by `FieldType`; the provider registered last wins, so a module can replace a built-in one. A field type no provider handles is read as text from its `Text` or `Value` property.
- Every property a part stores is already offered, discovered from the stored items and typed from their values (see [Every property of every part](data-sources.md#every-property-of-every-part)). Implement `IContentReportPropertySource` to type the properties of your own parts, including before any item stores them: return each property's path within the part (dots between nested names) and its `ReportDataType`, and nothing for parts you do not know. A typed property wins over one inferred from stored values. Register it with `services.AddScoped<IContentReportPropertySource, MyPartPropertySource>()`.
- For part data with friendlier names, computed values, or references, implement `IContentReportPartProvider`. Every provider is asked about every part and returns nothing for parts it does not know; the fields of all providers are combined. Name the fields `{PartName}.{Property}` with `ContentReportFieldContext.GetFieldName`.
- For a value that is computed, derive a field from `ContentReportField`. Override `GetValue` to read the value from a content item, and `PrepareAsync` for work done once per query, such as loading the display texts of referenced items. `PrepareAsync` runs only when a report uses the field. Values may be raw JSON nodes; the source converts them to the field's type.
- Create descriptors with `ContentReportFieldContext.CreateDescriptor`, and set `IsIdentifier` and `References` on fields that point to other content items or users.

## Checklist

- Names of the source, its data sets and its fields are stable, and field names are unique within a data set.
- Every data set and schema is hidden from a principal that may not read it, and a `null` principal sees nothing.
- Every value has the CLR type of its field, date-times are UTC, and missing values are `null`.
- `QueryAsync` returns at most `MaxRows` rows, newest first, and sets `Truncated` when there are more.
- Pushed-down conditions never drop a row the engine would keep; join keys and aggregates are exact or declined.
- Identifiers are marked, references declared where known, and the main date set.
- The source's feature and permissions are listed in the [Feature reference](../../feature-reference.md), and its configuration, if any, in [Configuration](../../configuration.md).
