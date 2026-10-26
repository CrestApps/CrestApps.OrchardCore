---
name: crestapps-report-data-source
description: >
  Skill for adding a new data source (connector) that the CrestApps.OrchardCore Report Builder can read, such as a SQL
  Server or PostgreSQL database, an Elasticsearch or Azure AI Search index, a REST API, or any module's own records.
  Data sources are Orchard Core data sources (IDataSource, OrchardCore.DataSources, from the Data Pipelines Core
  feature OrchardCore.DataPipelines.Core), so the same source serves reports and Orchard Core data pipelines. Covers
  RecordDataSource / RecordDataSet<T> (the easy path), IDataSource for dynamic sources, static Name / DisplayName /
  Description, the security boundary (only expose what the principal may read), value types and UTC dates, streaming
  page by page, optional condition push-down that must never drop rows, join keys, references, aggregate push-down
  with IAggregateRecordDataSet and ReportIndexAggregator, the CrestApps helpers (Identifier, MarkKeyFilterable,
  ReportIndexPages), registration with AddDataSource<T>(), tests, and docs. Also covers reading
  custom content parts and fields through Orchard Core's ContentDataSourceOptions. Use this skill whenever the
  request is to "add a report data source", "let the report builder read from <system>", "add a connector for
  reports", implement IDataSource / RecordDataSource for reports, or report on a new content field type.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "2.0"
---

# Adding a Report Builder Data Source

The **Report Builder** (`CrestApps.OrchardCore.Reports.Builder`, in the Reports module) lets people build reports
with drag and drop. It is built on Orchard Core's **Data Pipelines Core** feature (`OrchardCore.DataPipelines.Core`,
Orchard Core 4.0), which the builder depends on. Data Pipelines Core provides the data source contract, the
built-in **Content items**, **Users** and **Queries** sources, the formula language and its function catalog, and
the shared links. The builder's engine (`CrestApps.OrchardCore.Reports.Core`, `Designer/`) owns everything that is
the same for every source:

- planning and validating the query against the live schemas (`ReportQueryPlanner`);
- joins (inner/left/right/full), formulas, transforms, filters, grouping, aggregation, sorting, limits;
- tenant time zone conversion, exposed (viewer) filters and their option lists;
- visuals (tables, charts, metrics, pivots), export, sharing, share links and authorization.

A **data source** answers only three questions: which data sets exist for this principal, what fields each one has,
and what rows it holds. Everything else comes for free, and the source is also readable by Orchard Core data
pipelines.

Read these first; they are authoritative:

- Orchard Core: `src/OrchardCore/OrchardCore.DataSources.Abstractions/` (`IDataSource`, `RecordDataSource.cs` with
  `RecordDataSet<T>`, `IRecordDataSet`, `IAggregateRecordDataSet`, `DataConditions`, `DataField`, `DataValues`),
  `src/OrchardCore/OrchardCore.DataSources.Core/` (`AddDataSource<T>()`, `IDataSourceManager`), and the docs
  `src/docs/reference/modules/DataSources/README.md` ("Extending data sources").
- CrestApps helpers: `src/Abstractions/CrestApps.OrchardCore.Reports.Abstractions/DataSources/DataFieldExtensions.cs`
  and `src/Core/CrestApps.OrchardCore.Reports.Core/DataSources/` (`ReportIndexPages`, `ReportIndexAggregator`).
- Reference sources: `src/Modules/CrestApps.OrchardCore.ContactCenter/Reports/DataSources/`
  (`ContactCenterReportDataSource`, `ContactCenterReportDataSet<TRecord, TIndex>`, `DataSets/`) with
  `src/Modules/CrestApps.OrchardCore.ContactCenter/DataSourcesStartup.cs`, and
  `src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Reports/` with its `DataSourcesStartup.cs`.
- `src/Modules/CrestApps.OrchardCore.Reports/Designer/Services/ReportViewsDataSource.cs` — an `IDataSource` built
  on the engine.
- `src/Core/CrestApps.OrchardCore.Reports.Core/Designer/ReportQueryEngine.cs` — how sources are read.
- Docs: `src/CrestApps.Docs/docs/modules/report-builder/` (`custom-data-sources.md` for building a source).

## The contract

`IDataSource` (namespace `OrchardCore.DataSources`, package `OrchardCore.DataSources.Abstractions`):

| Member | Required behavior |
| --- | --- |
| `static string Name` | Stable technical name. Designs store it (`ReportDataSetReference.Source`). Never rename. |
| `static LocalizationSource DisplayName`, `Description` | `LocalizationSource.Create<MySource>("...")` (namespace `OrchardCore.Localization`). Shown in the builder's data source picker. |
| `GetDataSetsAsync(context, ct)` | Data sets **`context.User` may read**. `context.User` may be null: return nothing. |
| `GetSchemaAsync(dataSet, context, ct)` | The fields of a data set, or **null** when it does not exist or the principal may not read it. The engine reads a data set only after this returns a schema: **this is the security boundary**. It may throw `ReportQueryException` with a clear message (for example a broken upstream definition). |
| `ReadAsync(query, ct)` | `IAsyncEnumerable<DataBatch>`: rows of `query.DataSet` with the fields of `query.Fields` (every field when empty), at most `query.MaxRows` rows, about `query.BatchSize` per batch. Re-check access. Honor cancellation. |

The members are **static abstract**, so `IDataSource` cannot be a generic type argument: never resolve sources with
`GetServices<IDataSource>()`. Resolve them by name through `IDataSourceManager` (`GetDataSources()` lists their
descriptors without creating them, `GetDataSource(name)` resolves one).

Rules:

1. **Security.** When a saved report runs, `context.User` is the **report owner's** principal, not the viewer's
   (in a data pipeline, the user who published it). Filter by that principal exactly as you would for the owner.
   Never fall back to "everything" when the user is null.
2. **Stable names.** Data set and field names are stored in designs. Field names may contain dots
   (`Part.Field.Property`); they are referenced as `alias.FieldName`.
3. **Types.** Each `DataFieldType` maps to one CLR type: Text=`string`, Integer=`long`, Decimal=`decimal`,
   Boolean=`bool`, Date=`DateTime` (date only, no zone, never shifted), DateTime=`DateTime` **in UTC** (the engine
   converts to the tenant zone). Normalize with `DataValues.Coerce(value, fieldType)`. Rows are `object[]` aligned
   with the `DataBatch` fields. `RecordDataSet<T>` converts for you (enums become names, `DateTimeOffset`, local and
   unspecified date-times become UTC).
4. **Identifiers.** Set `DataField.IsIdentifier` on keys and foreign keys; the builder suggests them for joins and
   never sums them by default.
5. **Push-down is optional and must be a superset.** `query.Conditions` carry typed `DataCondition`s (DateTime
   already in UTC). Apply a condition only if your translation can never drop a row the engine's own filter would
   keep (the engine re-applies every filter; text compares case-insensitively, so do not push text equality to a
   case-sensitive collation). Ignoring all conditions is always correct. Use `DataConditions.DateRange(conditions,
   field)` for the inclusive date bounds.
6. **Stream, don't load.** Read the store page by page in a stable order (newest first when there is a main date);
   the base class stops the enumeration at the row limit. Never list every record and truncate in memory.
7. **Recursion.** If your source runs other report queries (like views), guard recursion through
   `context.Properties` (see `ReportViewRunner.EnterAsync`).

## The easy path: `RecordDataSource` and `RecordDataSet<T>`

```csharp
public sealed class InvoicesReportDataSet : RecordDataSet<Invoice>
{
    public InvoicesReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<InvoicesReportDataSet> S)
        : base(new DataSetDescriptor("Invoices", S["Invoices"], S["One row per invoice."]) { DefaultDateField = "IssuedUtc" })
    {
        var group = S["Invoice"].Value;

        AddField("InvoiceId", S["Invoice ID"], DataFieldType.Text, r => r.InvoiceId, group).Identifier();
        AddField("Amount", S["Amount"], DataFieldType.Decimal, r => r.Amount, group);
        AddField("IssuedUtc", S["Issued"], DataFieldType.DateTime, r => r.IssuedUtc, group);
        AddField("OwnerId", S["Owner"], DataFieldType.Text, r => r.OwnerId, group)
            .Identifier(new DataFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));

        Fields.MarkKeyFilterable(["InvoiceId"]);   // LoadAsync filters these exactly from DataConditions.Keys.
    }

    public override Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default) => ...;

    protected override async IAsyncEnumerable<Invoice> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // DataConditions.Keys(query.Conditions, "InvoiceId"): null = no key filter, empty = nothing can match.
        // DataConditions.DateRange(query.Conditions, "IssuedUtc"): inclusive bounds for the index column.
        await foreach (var invoice in ReportIndexPages.ReadAsync(_session, Query, collection: null, cancellationToken))
        {
            yield return invoice;
        }
    }
}

public sealed class BillingReportDataSource : RecordDataSource, IDataSource   // declare IDataSource yourself
{
    public BillingReportDataSource(InvoicesReportDataSet invoices) => DataSets = [invoices];

    public static string Name => "Billing";
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<BillingReportDataSource>("Billing");
    public static LocalizationSource Description { get; } = LocalizationSource.Create<BillingReportDataSource>("Invoices of the billing module.");

    protected override IEnumerable<IRecordDataSet> DataSets { get; }
}
```

- `AddField(name, displayName, DataFieldType, read, group)` returns the `DataField` (set `Description`, chain helpers).
- `CanReadAsync(context, ct)` is the permission check; `RecordDataSource` lists only readable data sets and re-checks
  access on every schema, read and aggregate call.
- `LoadAsync(query, ct)` returns `IAsyncEnumerable<TRecord>` streamed in a stable order, page by page.
- CrestApps helpers (`CrestApps.OrchardCore.Reports.DataSources`, Reports.Abstractions): `field.Identifier(params
  DataFieldReference[] references)`, `Fields.MarkKeyFilterable(names)`. `RecordDataSource` lists the data sets the fields reference on each data set
  (`DataSetDescriptor.References`), so the builder can show related data sets before reading their fields.
- YesSql helpers (`CrestApps.OrchardCore.Reports.Designer.DataSources`, Reports.Core):
  `ReportIndexPages.ReadAsync(session, () => query, collection, ct)` and `ReadPagesAsync(...)` read 500 records a
  page and detach each page from the session; the query factory is called once per page and needs a stable order
  (for example `.OrderByDescending(i => i.IssuedUtc).ThenByDescending(i => i.Id)`).
- A source whose data sets depend on features injects them through an interface each feature registers (see
  `IContactCenterReportDataSet`), so data sets of disabled features are never listed.
- For YesSql records read through a map index, derive from `IndexedReportDataSet<TRecord, TIndex>`
  (`CrestApps.OrchardCore.Reports.Designer.DataSources`, Reports.Core) instead of writing `LoadAsync`: pass the
  collection and the module's existing permissions to the constructor, add the fields, override `DateColumn` (a
  non-nullable index column holding exactly the field's value), `KeyColumns`, `DescribeColumns` and
  `GetAggregateColumns` (drop columns the index changes, such as upper-cased emails), and call `CompleteDescription()`.
  For the entries a record holds (order lines, refunds...), derive from
  `IndexedReportDataSet<TDocument, TIndex, ReportChildRow<TDocument, TEntry>>` and return
  `ReportChildRow<TDocument, TEntry>.For(document, document.Entries)` from `GetRows`. The Orders source
  (`src/Modules/CrestApps.OrchardCore.Orders/Reports/DataSources/`) is the reference.
- Store modules name their sources and data sets in `CommerceReportDataSets` (Reports.Abstractions) and reference each
  other through it (`Reference(source, dataSet)`, `ProductReference()`, `VariantReference()`); users and content items
  through `ReportDataReferences.User()` and `ReportDataReferences.ContentItem(type)`.
- Every index read by date needs a database index on the date column and `Id`, created with
  `IndexTableIndexCreation.CreateIfMissingAsync` from the migration's `CreateAsync` and a new `UpdateFromNAsync` step.
- Reference other modules' data sets by their stable names, such as
  `new DataFieldReference("ContactCenter", "DialerProfiles", "ItemId")`, the users data set
  (`ReportsConstants.UsersDataSource`, `UsersDataSet`, `UserIdField`), or a content type
  (`ReportsConstants.ContentsDataSource`, `"{ContentType}"`, `"ContentItemId"`); a reference to a source that is not
  enabled is simply unused.
- Never expose secrets, message bodies, IP addresses, or storage locations.

## Join keys

Mark a field `IsKeyFilterable` only if `LoadAsync` applies `DataConditions.Keys(query.Conditions, field)` **exactly**
(every record whose value is a key, no other). `null` means no key filter; an empty list means nothing can match
(`yield break`). The engine sends keys as an `In` condition with `IsJoinKey`, in batches of `JoinKeyBatchSize`.

## Aggregate push-down

The engine asks a source that implements Orchard Core's `IDataAggregateSource`; `RecordDataSource` implements it and
passes the query to a readable data set that implements `IAggregateRecordDataSet`
(`Task<IReadOnlyList<object[]>> AggregateAsync(DataAggregateQuery query, CancellationToken ct)`; return `null` to
decline). For YesSql map indexes:

```csharp
_aggregateColumns ??= ReportIndexAggregator.MapByName<InvoiceIndex>(Fields.Select(f => f.Name));
return ReportIndexAggregator.AggregateAsync<InvoiceIndex>(_session, collection, query, _aggregateColumns, cancellationToken);
```

Map only fields whose index column holds exactly the value the field reads. Decline (return `null`) when the data set
restricts records beyond its permission (the statement reads the whole index). A custom implementation must apply
`query.Conditions` exactly, answer only with records `query.Context.User` may read, return group values then
measures per row (date buckets as zero-based `long` indexes over `Boundaries`), and return `null` for anything it
cannot answer exactly or for more than `query.MaxGroups` groups.

## Dynamic sources: implement `IDataSource`

For an external system whose data sets or fields are discovered at run time, implement `IDataSource` directly:
`GetDataSetsAsync`, `GetSchemaAsync` (returning `DataSetSchema { DataSet, Fields }`), and `ReadAsync` yielding
`new DataBatch(fields, rows)` a page at a time, respecting `query.Fields`, `query.MaxRows` and `query.BatchSize`.
Infer types from sampled values with `DataValues.InferType` when the system has no schema.

## Layout and registration

- Register with `services.AddDataSource<TDataSource>()` (package `OrchardCore.DataSources.Core`; the extension is in
  `Microsoft.Extensions.DependencyInjection`) plus the data sets as scoped services, in a startup marked
  `[RequireFeatures("OrchardCore.DataPipelines.Core")]` (and the module's own `[Feature(...)]` when the data belongs
  to one feature). See `ContactCenter/DataSourcesStartup.cs` and `Omnichannel.Messaging/DataSourcesStartup.cs`.
  Do not require `ReportsConstants.BuilderFeature`: the builder depends on Data Pipelines Core, and the source should
  also serve data pipelines.
- **A module exposing its own stored records** keeps the source in that module, so the Reports module never depends
  on it.
- **A source for an external system** goes in its own module `src/Modules/CrestApps.OrchardCore.Reports.<Source>`
  with `Manifest.cs` (category `Reporting`, dependency `OrchardCore.DataPipelines.Core`) and `Startup.cs`. Add the
  project to `CrestApps.OrchardCore.slnx`, the targets project
  `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.csproj`, and the tests
  project references.
- Connection settings for external systems go in configuration (`CrestApps:Reports:<Source>`), documented in
  `docs/configuration.md` with the environment-variable form; never in a design.

## Custom content parts and fields

Do not write a source for content. The built-in **Content items** source is Orchard Core's: it reads the public
properties of part and field classes registered with `AddContentPart<T>()` / `AddContentField<T>()` (nested objects
up to 3 levels; secrets, `[JsonIgnore]`, lists of objects and dictionaries left out) and the fields added in the
admin. Names: a part value is `{Part}.{Property}`, a single-value field `{Part}.{Field}`, a multi-value field
`{Part}.{Field}.{Property}`; list values are comma-separated text. To read a part or field type differently, or one
without a class, configure `ContentDataSourceOptions` (namespace `OrchardCore.DataSources.Contents`, Orchard Core's
Data Pipelines module): `options.Fields["MyField"] = [new("Value", DataFieldType.Text)]`,
`options.Parts["MyPart"] = [new("Settings.Mode", DataFieldType.Text)]`. The content source declares only the
`Owner` → `Users.UserId` reference; content pickers and user pickers are joined explicitly.

## Tests (required)

Model on `tests/CrestApps.OrchardCore.Tests/Modules/ContactCenter/Reports/ContactCenterReportDataSourceTests.cs`,
`tests/CrestApps.OrchardCore.Tests/Modules/Omnichannel.Messaging/Reports/MessagingReportDataSourceTests.cs`, and
`tests/CrestApps.OrchardCore.Tests/Modules/Reports/Designer/` (`ReportDesignerTestHelpers.cs`:
`InMemoryReportDataSource`, `TestDataSourceManager`, `ReportDesignerTestServices`):

- unauthorized principal and null principal see nothing and get null schemas;
- every field's type and value, including missing values;
- only requested fields are returned; `MaxRows` and truncation; reads page past the first page;
- each pushed-down condition and join key filter never drops a row (compare with the engine's result without
  push-down); aggregates equal the row-by-row result or decline;
- an end-to-end run through `ReportQueryEngine` joining your source with another.

Use `TestContext.Current.CancellationToken` (xUnit1051) and build with
`-c Release -warnaserror -p:RunAnalyzers=true`.

## Docs (required)

Add the source to the built-in sources of `src/CrestApps.Docs/docs/modules/report-builder/data-sources.md` (or its own
technical page linked from there; `custom-data-sources.md` explains how to build one), its feature to
`docs/feature-reference.md`, configuration to `docs/configuration.md`, what people can report on with it to the User
Manual page `docs/user-manual/report-builder/data-sources.md`, and its name to "Words you will see" in
`docs/user-manual/report-builder/index.md`. Never mention tests, test counts or internal plans in the docs site, and
keep code blocks out of the User Manual.
