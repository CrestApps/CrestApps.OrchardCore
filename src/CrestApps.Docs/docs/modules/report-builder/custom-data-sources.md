---
sidebar_label: Add Your Own Data Source
title: Add Your Own Report Data Source
description: Extend the Report Builder with your own Orchard Core data sources - record-backed sources, sources for external systems, grouping in the source, join keys, relationships, and more content parts and fields.
user_manual:
  - user-manual/report-builder/data-sources
---

Any module can add data to the Report Builder. The builder reads Orchard Core data sources, from the **Data Pipelines Core** feature (`OrchardCore.DataPipelines.Core`) of Orchard Core 4.0, so a data source you add is offered to reports and to Orchard Core's data pipelines alike. A **data source** answers only three questions: which data sets exist for the principal, which typed fields each one has, and which rows it holds. The engine does everything else for every source: joins, formulas, transforms, filters, grouping and aggregation, sorting, limits, time zones, viewer filters and their option lists, visuals, export, sharing and authorization.

This page covers what matters to reports. The contract itself is described in Orchard Core's [Data Sources](https://docs.orchardcore.net/en/latest/reference/modules/DataSources/#extending-data-sources) documentation.

This video shows a data source added by a module and how its data sets appear in the builder.

<YouTube video="report-builder" title="Narrated video of a module's own data source and its data sets in the Report Builder" start={739} end={772} />

## Choose an approach

| You want to report on | Build | Reference |
| --- | --- | --- |
| Records your module stores, such as YesSql documents or catalog entries | A `RecordDataSource` with one `RecordDataSet<T>` per record type, or one `IndexedReportDataSet<TRecord, TIndex>` per record type read through a map index | `OrchardCore.DataSources.Abstractions`, and `CrestApps.OrchardCore.Reports.Abstractions` and `CrestApps.OrchardCore.Reports.Core` for the helpers |
| Such records, grouped in the database for large data | The same, with `IAggregateRecordDataSet` and `ReportIndexAggregator` | Also `CrestApps.OrchardCore.Reports.Core` |
| An external system whose data sets or fields are found at run time, such as a database, a search index or a REST API | `IDataSource` | `OrchardCore.DataSources.Abstractions` |
| A content part or content field type that the **Content items** source does not read the way you want | `ContentDataSourceOptions` | Orchard Core's Data Pipelines module |
| Data that people can prepare in the browser | Nothing: a [reusable view](../../user-manual/report-builder/views.md) | |

The data source contract (`IDataSource`, `RecordDataSource`, `RecordDataSet<T>`, `DataField`, `DataFieldType`, `DataCondition`, `DataConditions`) is in the `OrchardCore.DataSources` namespace. The CrestApps helpers are in `CrestApps.OrchardCore.Reports.DataSources` (`CrestApps.OrchardCore.Reports.Abstractions`) and `CrestApps.OrchardCore.Reports.Designer.DataSources` (`CrestApps.OrchardCore.Reports.Core`), and `ReportsConstants` (the builder's feature ID and the built-in source names) is in `CrestApps.OrchardCore.Reports`.

## Where the code goes

- **A module exposing its own records** (activities, interactions, chat sessions...) keeps the source in that module, in a startup marked with the module's feature and `[RequireFeatures("OrchardCore.DataPipelines.Core")]`. The Reports module never depends on it, and the source is registered whenever Data Pipelines Core is on with that feature: with the Report Builder, which depends on Data Pipelines Core, or with Orchard Core's Data Pipelines. The Omnichannel, Contact Center, AI chat, Messaging and store sources (Orders, Inventory, Customers and the others) are built this way.
- **A source for an external system** goes in its own module, whose feature depends on `OrchardCore.DataPipelines.Core`. Connection settings belong in configuration (for example `CrestApps:Reports:{Source}`), never in a design.

Register a source with `services.AddDataSource<TDataSource>()`, from the `OrchardCore.DataSources.Core` package. Data sources are scoped services resolved by name through `IDataSourceManager`, which lists them with their name, display name and description without creating them. Two data sources cannot share a name.

:::note[Data sources are not resolved as a list of services]
`IDataSource` declares `Name`, `DisplayName` and `Description` as static abstract members, so it cannot be used as a generic type argument, and data sources are not resolved with `GetServices<IDataSource>()`. Use `IDataSourceManager`: `GetDataSource(name)` resolves one from the services of the scope.
:::

## Record-backed data sources

For records kept in a store, derive from `RecordDataSet<TRecord>` and `RecordDataSource` instead of implementing `IDataSource` by hand. The base classes check access on every call, read only the fields and the number of rows the query asks for, split the rows into batches, and convert each value to its field's type (enums become their name, `DateTimeOffset`, local and unspecified date-times become UTC).

This example exposes the invoices of a billing module, stored in YesSql with an `InvoiceIndex` map index. It follows the Contact Center and Messaging data sets:

```csharp
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

public sealed class InvoicesReportDataSet : RecordDataSet<Invoice>
{
    public const string Name = "Invoices";

    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;

    public InvoicesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<InvoicesReportDataSet> S)
        : base(new DataSetDescriptor(Name, S["Invoices"], S["One row per invoice."]) { DefaultDateField = "IssuedUtc" })
    {
        _session = session;
        _authorizationService = authorizationService;

        var invoice = S["Invoice"].Value;
        var people = S["People"].Value;

        AddField("InvoiceId", S["Invoice ID"], DataFieldType.Text, record => record.InvoiceId, invoice).Identifier();
        AddField("Number", S["Number"], DataFieldType.Text, record => record.Number, invoice);
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status, invoice);
        AddField("Amount", S["Amount"], DataFieldType.Decimal, record => record.Amount, invoice);
        AddField("IssuedUtc", S["Issued"], DataFieldType.DateTime, record => record.IssuedUtc, invoice);
        AddField("PaidUtc", S["Paid"], DataFieldType.DateTime, record => record.PaidUtc, invoice);

        // The customer is a content item of the Customer type; the account manager is a user.
        AddField("CustomerId", S["Customer ID"], DataFieldType.Text, record => record.CustomerContentItemId, people)
            .Identifier(new DataFieldReference("Contents", "Customer", "ContentItemId"));
        AddField("AccountManagerId", S["Account manager (user ID)"], DataFieldType.Text, record => record.AccountManagerUserId, people)
            .Identifier(new DataFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));

        // The fields LoadAsync filters exactly when a join sends keys.
        Fields.MarkKeyFilterable(["InvoiceId", "CustomerId"]);
    }

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, BillingPermissions.ViewInvoiceReports);
    }

    protected override async IAsyncEnumerable<Invoice> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Join keys: read only the invoices a joined report can match. An empty list means none can match. Only the
        // conditions of a join are matched exactly; the engine compares a viewer's filter on the same field without
        // regard to case, so that one is left to the engine.
        var joinKeys = query.Conditions.Where(condition => condition.IsJoinKey).ToArray();
        var invoiceIds = DataConditions.Keys(joinKeys, "InvoiceId")?.Select(key => key?.ToString()).ToArray();
        var customerIds = DataConditions.Keys(joinKeys, "CustomerId")?.Select(key => key?.ToString()).ToArray();

        // A viewer's filter that limits the status to a list of values is applied by the database, without regard to
        // case like the engine's own filter.
        var columns = new ReportIndexConditions<InvoiceIndex>(query.Conditions)
            .Text("Status", nameof(InvoiceIndex.Status));

        if (invoiceIds is { Length: 0 } || customerIds is { Length: 0 } || columns.MatchesNothing)
        {
            yield break;
        }

        // The period the report's filters put on the main date, never narrower than the filters.
        var (from, to) = DataConditions.DateRange(query.Conditions, "IssuedUtc");

        // The conditions on the index rows, added to the query of every page.
        IQueryIndex<InvoiceIndex> Filter(IQueryIndex<InvoiceIndex> invoices)
        {
            invoices = columns.Apply(invoices);

            if (invoiceIds is not null)
            {
                invoices = invoices.Where(index => index.InvoiceId.IsIn(invoiceIds));
            }

            if (customerIds is not null)
            {
                invoices = invoices.Where(index => index.CustomerContentItemId.IsIn(customerIds));
            }

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

            return invoices;
        }

        // Newest first by the issue date, a page at a time; the base class stops reading at the row limit.
        await foreach (var invoice in ReportIndexPages.ReadAsync<Invoice, InvoiceIndex>(_session, Filter, index => index.IssuedUtc, cancellationToken: cancellationToken))
        {
            yield return invoice;
        }
    }
}
```

The data source lists its data sets. Its name, display name and description are static, and it declares `IDataSource` itself, since `RecordDataSource` is a base class:

```csharp
using OrchardCore.DataSources;
using OrchardCore.Localization;

public sealed class BillingReportDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    public BillingReportDataSource(InvoicesReportDataSet invoices)
    {
        _dataSets = [invoices];
    }

    public static string Name => "Billing";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<BillingReportDataSource>("Billing");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<BillingReportDataSource>("Invoices of the billing module.");

    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
```

And a startup registers both when Data Pipelines Core is enabled, which the Report Builder enables:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<InvoicesReportDataSet>()
            .AddDataSource<BillingReportDataSource>();
    }
}
```

`AddField(name, displayName, type, read, group)` returns the field's `DataField`, so you can set its `Description` (a hint shown in the builder) or chain the helpers below. A source with many data sets that depend on features, like the Contact Center, injects them as an interface each feature registers (`IContactCenterReportDataSet`), so a data set whose feature is off is never listed.

### The CrestApps helpers

| Helper | What it does |
| --- | --- |
| `field.Identifier(params DataFieldReference[] references)` | Marks a field as identifying a record (`IsIdentifier`) and adds the data sets its values point to (`References`). |
| `Fields.MarkKeyFilterable(names)` | Sets `IsKeyFilterable` on the named fields; see [Join keys](#join-keys). |
| `ReportIndexPages.ReadAsync<TRecord, TIndex>(session, filter, date, collection, cancellationToken)` | Reads the records of a map index 500 at a time, newest first by a date column of the index (then by index row), and detaches each page from the session once it was read, so a long read does not keep every record in memory. Each page is found in the index table alone, after the last row of the page before, and only then are its records loaded, so every page costs the same however far the read has gone. `filter` adds the conditions on the index rows; a nullable date column reads the records without a date last; a `null` date orders by index row only. |
| `ReportIndexPages.ReadPagesAsync(...)` | The same, one page at a time, such as to look up what the records of a page refer to in one call. |
| `ReportIndexConditions<TIndex>` | Applies the conditions that limit a field to a list of values, such as a viewer's status or channel filter, to the index rows: `.Text(field, column)` compares in upper case and leaves text that isn't ASCII to the engine, `.Enum(field, index => index.Column)` compares enumeration values by name, and `MatchesNothing` tells when no value can match. Pass `Apply` to the filter of `ReportIndexPages`. |
| `ReportIndexAggregator` | Answers an aggregate query with one `GROUP BY` over a map index; see [Grouping in the data source](#grouping-in-the-data-source). |
| `IndexedReportDataSet<TRecord, TIndex>` | A `RecordDataSet<TRecord>` that does all of the above for YesSql records read through a map index: it checks the permissions you pass (the principal needs every one), reads with `ReportIndexPages` newest first by `DateColumn`, pushes the date range, the join keys of `KeyColumns` and the columns `DescribeColumns` declares down to the index, and groups in the database through `ReportIndexAggregator` (`GetAggregateColumns` leaves out a column the index changes, such as an email kept in upper case). Pass the records' collection to the constructor, add the fields, and call `CompleteDescription()`. See [A data set over an index](#a-data-set-over-an-index). |
| `IndexedReportDataSet<TDocument, TIndex, TRow>` and `ReportChildRow<TParent, TChild>` | The same for a data set whose rows are the entries a record holds, such as the lines of an order: `GetRows` turns each record read into rows, usually with `ReportChildRow<TParent, TChild>.For(record, record.Lines)`, so a row reads both the entry and its record. Such a data set never groups in the database, since the index holds one row per record. |
| `ReportDataReferences` | `User()` and `ContentItem(contentType)`: the references to Orchard Core's users and content items data sets. |
| `CommerceReportDataSets` | The stable names of the store's data sources and data sets, and `Reference(source, dataSet)`, `ProductReference()` and `VariantReference()`, so a module refers to another module's data set without depending on it. |

### A data set over an index

Most modules store records in YesSql with a map index, and `IndexedReportDataSet<TRecord, TIndex>` (`CrestApps.OrchardCore.Reports.Core`) does the reading for them. The invoices data set above becomes fields and a few overrides; this is how the Orders, Inventory and other store data sources are built:

```csharp
public sealed class InvoicesReportDataSet : IndexedReportDataSet<Invoice, InvoiceIndex>, IBillingReportDataSet
{
    public InvoicesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<InvoicesReportDataSet> S)
        : base(
            new DataSetDescriptor("Invoices", S["Invoices"], S["One row per invoice."]),
            session,
            authorizationService,
            BillingConstants.CollectionName,
            BillingPermissions.ViewInvoiceReports)
    {
        AddField("InvoiceId", S["Invoice ID"], DataFieldType.Text, record => record.InvoiceId).Identifier();
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Status);
        AddField("Currency", S["Currency"], DataFieldType.Text, record => record.Currency);
        AddField("Amount", S["Amount"], DataFieldType.Decimal, record => record.Amount).Description = S["In the invoice's currency."];
        AddField("IssuedUtc", S["Issued"], DataFieldType.DateTime, record => record.IssuedUtc);
        AddField("CustomerId", S["Customer ID"], DataFieldType.Text, record => record.CustomerId)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers));

        CompleteDescription();
    }

    // The main date: the index column must hold exactly the field's value.
    protected override (string Field, Expression<Func<InvoiceIndex, DateTime>> Column)? DateColumn
        => ("IssuedUtc", index => index.IssuedUtc);

    // The fields a join narrows the read on, each an index column that holds exactly its value.
    protected override IReadOnlyDictionary<string, Expression<Func<InvoiceIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<InvoiceIndex, string>>>(StringComparer.Ordinal)
        {
            ["InvoiceId"] = index => index.InvoiceId,
            ["CustomerId"] = index => index.CustomerId,
        };

    // The fields a viewer's filter limits to a list of values, applied to the index rows.
    protected override void DescribeColumns(ReportIndexConditions<InvoiceIndex> columns)
    {
        columns.Text("Status", nameof(InvoiceIndex.Status));
    }
}
```

Give the money fields a `Currency` field beside them, and say in their description which currency they are in. A module with several features registers its data sets as a marker interface, such as `IOrderReportDataSet`, in the startup of the feature that stores each record, so the data source lists only the data sets of the features that are on.

## Sources for external systems

Implement `IDataSource` when the data sets or fields are not known in advance, or the rows do not come from records you load. This example reads tickets from a help desk API:

```csharp
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;

public sealed class HelpDeskDataSource : IDataSource
{
    private const string TicketsDataSet = "Tickets";

    private readonly IHelpDeskClient _client;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStringLocalizer S;

    public HelpDeskDataSource(
        IHelpDeskClient client,
        IAuthorizationService authorizationService,
        IStringLocalizer<HelpDeskDataSource> stringLocalizer)
    {
        _client = client;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public static string Name => "HelpDesk";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<HelpDeskDataSource>("Help desk");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<HelpDeskDataSource>("Tickets of the help desk.");

    public async Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        // List only what context.User may read.
        return await CanReadAsync(context) ? [Describe()] : [];
    }

    public async Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (dataSet != TicketsDataSet || !await CanReadAsync(context))
        {
            return null; // The engine refuses the data set.
        }

        return new DataSetSchema
        {
            DataSet = Describe(),
            Fields = [.. GetFields()],
        };
    }

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Check again: reading is the last line of defense.
        if (query.DataSet != TicketsDataSet || !await CanReadAsync(query.Context))
        {
            yield break;
        }

        // Return the fields the report uses (every field when none is named); skip the work of the others.
        var fields = GetFields()
            .Where(field => query.Fields is null || query.Fields.Count == 0 || query.Fields.Contains(field.Name))
            .ToArray();

        // Optional push-down: ask the API for the period the filters allow, newest first, a page at a time.
        var (from, to) = DataConditions.DateRange(query.Conditions, "CreatedUtc");
        var remaining = query.MaxRows > 0 ? query.MaxRows : int.MaxValue;
        var skip = 0;

        while (remaining > 0)
        {
            var take = Math.Min(Math.Max(1, query.BatchSize), remaining);
            var tickets = await _client.GetTicketsAsync(from, to, skip, take, cancellationToken);

            if (tickets.Count == 0)
            {
                yield break;
            }

            var rows = tickets
                .Select(ticket => fields.Select(field => DataValues.Coerce(Read(ticket, field.Name), field.Type)).ToArray())
                .ToList();

            yield return new DataBatch(fields, rows);

            skip += tickets.Count;
            remaining -= tickets.Count;
        }
    }

    private DataSetDescriptor Describe()
    {
        return new DataSetDescriptor(TicketsDataSet, S["Tickets"], S["One row per help desk ticket."])
        {
            DefaultDateField = "CreatedUtc",
        };
    }

    private DataField[] GetFields()
    {
        return
        [
            new("TicketId", S["Ticket ID"], DataFieldType.Text) { IsIdentifier = true },
            new("Subject", S["Subject"], DataFieldType.Text),
            new("Priority", S["Priority"], DataFieldType.Text),
            new("RequesterEmail", S["Requester email"], DataFieldType.Text),
            new("CreatedUtc", S["Created"], DataFieldType.DateTime),
            new("ResolvedUtc", S["Resolved"], DataFieldType.DateTime),
            new("ReopenCount", S["Times reopened"], DataFieldType.Integer),
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

    private async Task<bool> CanReadAsync(DataSourceContext context)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, HelpDeskPermissions.ViewHelpDeskReports);
    }
}
```

```csharp
services.AddDataSource<HelpDeskDataSource>();
```

A source whose data sets come from somewhere else, such as the tables of a database, lists them in `GetDataSetsAsync` and describes each in `GetSchemaAsync`. A source that has to read data to learn its fields, like Orchard Core's **Queries** source, can sample rows and infer each field's type with `DataValues.InferType`. A source that cannot describe a data set, for example because an upstream definition is broken, may throw `ReportQueryException` (`CrestApps.OrchardCore.Reports.Designer`, in `CrestApps.OrchardCore.Reports.Core`) with a message the builder shows; a source meant for data pipelines too should throw an exception whose message is safe to show.

## The contract

| Member | Rule |
| --- | --- |
| `static Name` | Stable technical name. Designs store it (`ReportDataSetReference.Source`), so never rename it. |
| `static DisplayName`, `static Description` | `LocalizationSource`s, such as `LocalizationSource.Create<MySource>("...")`, shown in the builder's data source picker. |
| `GetDataSetsAsync(context, cancellationToken)` | The data sets `context.User` may read. When `context.User` is `null`, return nothing. |
| `GetSchemaAsync(dataSet, context, cancellationToken)` | The fields of a data set, or `null` when it does not exist or the principal may not read it. The engine reads a data set only after this returns a schema: this is the security boundary. |
| `ReadAsync(query, cancellationToken)` | Batches of rows of `query.DataSet` with the fields of `query.Fields` (every field when it is empty), at most `query.MaxRows` rows in all, about `query.BatchSize` rows per batch. Check access again, and honor cancellation. The engine asks for one row more than its limit to tell when a data set has more. |
| `RecordDataSet<T>.LoadAsync(query, cancellationToken)` | Streams the records in a stable order, a page at a time; the base class stops the enumeration once it has enough rows, so never load every record at once. |
| Data set and field names | Stable: designs store them. Field names may contain dots (`Part.Field.Property`) and are referenced as `alias.FieldName`. |
| `DataSetDescriptor.DefaultDateField` | The data set's main date. A new report starts filtered on its last 30 days, and the source can read only that period. |
| `DataSetDescriptor.Group`, `DataField.Group` | The group a data set or field is listed under in the builder. |
| `DataField.IsIdentifier` | Marks keys and foreign keys, which the builder suggests when joining and never sums by default. |
| `DataField.IsKeyFilterable` | The source filters join keys on this field exactly; see [Join keys](#join-keys). |
| `References` | On a field, the data sets (and their key field) its values point to; on a data set, every data set its fields point to. See [Relationships](#relationships). |
| `DataSourceContext.Properties` | A bag shared by one run, for example to detect recursion. |

### Values

Each row is an `object[]` aligned with the fields of its `DataBatch`. Each `DataFieldType` maps to one CLR type:

| `DataFieldType` | CLR type |
| --- | --- |
| `Text` | `string` |
| `Integer` | `long` |
| `Decimal` | `decimal` |
| `Boolean` | `bool` |
| `Date` | `DateTime`, date only, no time zone; never shifted |
| `DateTime` | `DateTime` in UTC; the engine converts it to the tenant time zone |

Use `DataValues.Coerce(value, fieldType)` to normalize raw values. A missing value is `null`. `RecordDataSet<T>` converts the values its fields read for you.

### Conditions you may push down

`query.Conditions` carries the report's row filters as `DataCondition`s (field, `DataFilterOperator`, typed values, with `DateTime` values already in UTC). Relative date filters (in the last or next N days) arrive as a date range. Applying a condition is **optional** and must never drop a row the engine's own filter would keep, because the engine applies every filter again after reading:

- Ignoring every condition is always correct.
- `DataConditions.DateRange(conditions, field)` returns the narrowest inclusive bounds the conditions allow; a missing bound is `null`. Apply them to an indexed date column.
- Text compares ignoring case in the engine, so do not push text equality to a case-sensitive collation; `ReportIndexConditions` compares it in upper case.
- Push a condition only to a column that holds the same value as the field. A column the index normalizes, such as a canonical provider name, can drop rows the field's value matches.
- Conditions are offered only for data sets that no outer join can fill with empty values.

### Index the date you read by

`ReportIndexPages` seeks to each page through the date column and the index row identifier, so create a database index on both as a step of the migration that creates the index table, which runs after the table exists on a new site and an upgraded one alike, with `IndexTableIndexCreation.CreateIfMissingAsync` (namespace `CrestApps.OrchardCore.YesSql.Core.Migrations`) so the step can run again safely:

```csharp
public async Task<int> UpdateFrom3Async()
{
    await IndexTableIndexCreation.CreateIfMissingAsync<InvoiceIndex>(SchemaBuilder, "IDX_InvoiceIndex_Report", null, "IssuedUtc", "Id");

    return 4;
}
```

Without it the database sorts the whole index table for every page.

## Join keys

When a report joins a data set with an inner or left join, the engine can send the distinct keys of the rows before it, so the joined data set reads only the records that can match instead of its newest records up to the row limit. To take part:

1. Mark the field `IsKeyFilterable` (with the helper, `Fields.MarkKeyFilterable(names)`). Only `Text` and `Integer` fields joined to a field of the same type are narrowed.
2. Read the keys from the conditions of the join only, those with `IsJoinKey` set: `DataConditions.Keys(query.Conditions.Where(condition => condition.IsJoinKey), field)` gives `null` when no condition limits the field to a list of values, an empty list when no record can match, or the values. `DataConditions.Keys` also returns the values of a viewer's `Equals` or `In` filter on the field. The engine compares those without regard to case, so an exact match in the database could drop a row the engine keeps: leave them to the engine, or apply them with `ReportIndexConditions.Text`, which compares in upper case. `IndexedReportDataSet` does this for you.
3. Apply them **exactly**: return every record whose value is one of the keys, and no other. A source that cannot must not set the flag.

The keys arrive as an `In` condition with `IsJoinKey` set, in batches of `JoinKeyBatchSize`, up to `MaxJoinKeys` keys; with more keys, the joined data set is read in full (see [Configuration](index.md#configuration)).

## Relationships

A `DataFieldReference(source, dataSet, field)` on a field says that its values identify records of another data set, such as an invoice's customer:

- The builder joins on it automatically when both data sets are in a report, lists the referenced data sets under **Related** in **Add data set**, and suggests the join. A `RecordDataSource` lists the data sets its fields reference on the data set itself (`DataSetDescriptor.References`), so **Related** can list them before the fields are read; a data source that implements `IDataSource` directly sets these references on the descriptors it returns.
- Reference other modules' data sets by their stable names, such as `new DataFieldReference("ContactCenter", "DialerProfiles", "ItemId")`, or the users data set with `ReportsConstants.UsersDataSource`, `ReportsConstants.UsersDataSet` and `ReportsConstants.UserIdField`. A content type is `new DataFieldReference(ReportsConstants.ContentsDataSource, "{ContentType}", "ContentItemId")`.
- A reference to a source that is not enabled is simply unused.
- Declare references only when the source knows them without reading data, for example from definitions.

## Grouping in the data source

A report over one data set that groups by fields or date periods and counts, sums, averages, or takes the smallest or largest values can be answered by its source, so the engine reads a few groups instead of every row (see [Large data](large-data.md#grouping-in-the-data-source)). The engine asks a source that implements Orchard Core's `IDataAggregateSource`; `RecordDataSource` implements it and passes the query to the data set, once it checked access, when the data set implements `IAggregateRecordDataSet`.

For YesSql records, implement `IAggregateRecordDataSet` on the data set and hand the query to `ReportIndexAggregator`, which answers it with one `GROUP BY` statement over the map index table:

```csharp
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using OrchardCore.DataSources;

public sealed class InvoicesReportDataSet : RecordDataSet<Invoice>, IAggregateRecordDataSet
{
    // The report fields whose values the index copies unchanged, mapped to their index column.
    private IReadOnlyDictionary<string, string> _aggregateColumns;

    // ...constructor, fields, CanReadAsync and LoadAsync as above...

    public Task<IReadOnlyList<object[]>> AggregateAsync(DataAggregateQuery query, CancellationToken cancellationToken = default)
    {
        _aggregateColumns ??= ReportIndexAggregator.MapByName<InvoiceIndex>(Fields.Select(field => field.Name));

        return ReportIndexAggregator.AggregateAsync<InvoiceIndex>(_session, null, query, _aggregateColumns, cancellationToken);
    }
}
```

`MapByName` maps each field to the index column of the same name; pass your own dictionary when the names differ, and map only fields whose index column holds exactly the value the field reads. Pass the index's collection as the second argument when the records are stored in one. A data set that limits which records a principal reads beyond its permission should return `null` instead, since the statement reads the whole index.

`ReportIndexAggregator` declines (returns `null`) whenever it cannot answer exactly the way the engine would: a field that is not a mapped column, a text comparison other than equality, equality on non-ASCII text, an empty-text test on a text column, the smallest or largest value of a text column, a group by a text column on MySQL, or more groups than `MaxGroups`. The engine groups text without regard to case, so `Acme Corp` and `ACME CORP` are one group shown with the first spelling it reads, and it groups the rows a source returns again, so a database may keep values that differ only in case apart but must not merge values that differ otherwise. SQLite, SQL Server and PostgreSQL group text in the database; MySQL's default collation also ignores accents, so a group by text is read row by row there. SQL Server's default collation also treats values that differ only in trailing spaces as equal. No database orders text the way the engine does, so the smallest or largest text value is always read row by row. Enum columns are compared and grouped by name, and date buckets are written as a `CASE` expression over UTC boundaries.

To group anything else, implement `AggregateAsync(DataAggregateQuery, CancellationToken)` yourself:

- `query.Groups` lists the fields to group by. A group with `Boundaries` is a date bucketed by those ascending UTC boundaries: its value is the zero-based bucket index as a `long`, and records outside every bucket or without a value fall in a `null` group.
- `query.Measures` lists what to compute per group: `Count` (of the field's values, or of all records when `Field` is `null`), `Sum`, `Min` and `Max`.
- Each result row holds the group values, then the measures, in that order; values use the CLR types above, with date-times in UTC.
- `query.Conditions` must be applied **exactly**: comparisons are exact, text ignores case, `NotEquals` and `NotIn` keep records whose value is empty. Answer only with records `query.Context.User` may read.
- Return `null` for anything you cannot answer exactly, or for more groups than `query.MaxGroups`. The engine then reads rows as usual.

## Security

- When a saved report runs, `context.User` is the **report owner's** principal, not the viewer's. Filter by that principal exactly as you would for the owner. While a report is designed, it is the designer. In a data pipeline, it is the user who published the pipeline.
- Never fall back to "everything" when the principal is `null` or cannot be authorized: return no data sets, a `null` schema, and no rows.
- Check access in `GetDataSetsAsync`, `GetSchemaAsync` and again in `ReadAsync` (`RecordDataSource` does this for you through `CanReadAsync`).
- Never expose secrets, password hashes, tokens, message bodies, IP addresses or storage locations.

See [Permissions and security](permissions.md).

## Content parts and fields

The built-in **Content items** source is Orchard Core's. It reads the parts and content fields registered in code from the public properties of their classes, and the fields added in the admin from the part definitions, so a part your module registers with `AddContentPart<TPart>()` is offered without any work. To read a part or a field type differently, such as with another type, as a list, or when it has no class, describe its values in `ContentDataSourceOptions` (namespace `OrchardCore.DataSources.Contents`, from Orchard Core's Data Pipelines module):

```csharp
using OrchardCore.DataSources;
using OrchardCore.DataSources.Contents;

services.Configure<ContentDataSourceOptions>(options =>
{
    // A field type with several values becomes one {PartName}.{FieldName}.{Property} field per value.
    options.Fields["AddressField"] =
    [
        new("City", DataFieldType.Text),
        new("PostalCode", DataFieldType.Text),
        new("CountryCode", DataFieldType.Text),
    ];

    // A part value becomes the field {PartName}.{Property}; a path reads a nested property.
    options.Parts["RatingPart"] =
    [
        new("Score", DataFieldType.Decimal),
        new("Votes.Count", DataFieldType.Integer) { DisplayName = "Votes", IsReadOnly = true },
    ];
});
```

A described field type is read only as described, so the names of its fields do not change. See [Reading more parts and fields](https://docs.orchardcore.net/en/latest/reference/modules/DataSources/#reading-more-parts-and-fields) in the Orchard Core documentation.

## Checklist

- Names of the source, its data sets and its fields are stable, and field names are unique within a data set.
- Every data set and schema is hidden from a principal that may not read it, and a `null` principal sees nothing.
- Every value has the CLR type of its field, date-times are UTC, and missing values are `null`.
- Records are streamed in a stable order, a page at a time, newest first when the data set has a main date.
- Pushed-down conditions never drop a row the engine would keep; join keys and aggregates are exact or declined.
- Identifiers are marked, references declared where known, and the main date set.
- The source is registered with `AddDataSource<T>()` in a startup that requires `OrchardCore.DataPipelines.Core`.
- The source's feature and permissions are listed in the [Feature reference](../../feature-reference.md), and its configuration, if any, in [Configuration](../../configuration.md).
