---
name: crestapps-report-data-source
description: >
  Skill for adding a new data source (connector) to the CrestApps.OrchardCore Report Builder, such as a SQL Server
  or PostgreSQL database, an Elasticsearch or Azure AI Search index, a REST API, users, or any module's own records,
  modeled on the Content items, Queries and Report Views data sources. Covers the IReportDataSource
  contract (data sets, typed field schemas, rows), the security boundary (only expose what the principal may read),
  value types and UTC dates, optional filter push-down that must never drop rows, row limits and truncation, stable
  names, the feature/module layout, DI registration, tests, and docs. Also covers extending the content items source with
  IContentReportFieldProvider / IContentReportPartProvider for custom content fields and parts. Use this skill
  whenever the request is to "add a report data source", "let the report builder read from <system>", "add a
  connector for reports", implement IReportDataSource, or report on a new content field type.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Adding a Report Builder Data Source

The **Report Builder** (`CrestApps.OrchardCore.Reports.Builder`, in the Reports module) lets people build reports
with drag and drop. The engine (`CrestApps.OrchardCore.Reports.Core`, `Designer/`) owns everything that is the same
for every source:

- planning and validating the query against the live schemas (`ReportQueryPlanner`);
- joins (inner/left/right/full), the formula language, transforms, filters, grouping, aggregation, sorting, limits;
- tenant time zone conversion, exposed (viewer) filters and their option lists;
- visuals (tables, charts, metrics, pivots), export, sharing, share links and authorization.

A **data source** answers only three questions: which data sets exist for this principal, what fields each one has,
and what rows it holds. Everything else comes for free.

Read these first; they are authoritative:

- `src/Abstractions/CrestApps.OrchardCore.Reports.Abstractions/DataSources/*.cs` — the contract.
- `src/Modules/CrestApps.OrchardCore.Reports/Contents/Services/ContentsReportDataSource.cs` — the reference source.
- `src/Modules/CrestApps.OrchardCore.Reports/Queries/QueriesReportDataSource.cs` — a source whose schema is inferred from results.
- `src/Modules/CrestApps.OrchardCore.Reports/Designer/Services/ReportViewsDataSource.cs` — a source built on the engine.
- `src/Core/CrestApps.OrchardCore.Reports.Core/Designer/ReportQueryEngine.cs` — how sources are called.
- Docs: `src/CrestApps.Docs/docs/modules/report-builder.md`.

## The contract (all members required)

| Member | Required behavior |
| --- | --- |
| `string Name` | Stable technical name. Designs store it (`ReportDataSetReference.Source`). Never rename. |
| `LocalizedString DisplayName`, `Description` | Shown in the builder's data source picker. |
| `GetDataSetsAsync(context, ct)` | Data sets **`context.User` may read**. `context.User` may be null: return nothing. |
| `GetSchemaAsync(dataSet, context, ct)` | The fields of a data set, or **null** when it does not exist or the principal may not read it. The engine reads a data set only after this returns a schema: **this is the security boundary**. It may throw `ReportQueryException` with a clear message (for example a broken upstream definition). |
| `QueryAsync(query, ct)` | Rows of `query.DataSet` with at least `query.Fields` (return only those when computing others costs anything), at most `query.MaxRows` rows; set `Truncated` when more exist (read `MaxRows + 1`). Re-check access defensively. Honor cancellation. |

Rules:

1. **Security.** When a saved report runs, `context.User` is the **report owner's** principal, not the viewer's. Filter
   by that principal exactly as you would for the owner. Never fall back to "everything" when the user is null.
2. **Stable names.** Data set and field names are stored in designs. Field names may contain dots
   (`Part.Field.Suffix`); they are referenced as `alias.FieldName`.
3. **Types.** Each `ReportDataType` maps to one CLR type: Text=`string`, Integer=`long`, Decimal=`decimal`,
   Boolean=`bool`, Date=`DateTime` (date only, no zone, never shifted), DateTime=`DateTime` **in UTC** (the engine
   converts to the tenant zone). Normalize with `ReportDataValues.Coerce(value, dataType)` (it also unwraps
   `System.Text.Json` nodes). Rows are `object[]` aligned with `ReportDataTable.Fields`.
4. **Identifiers.** Set `ReportFieldDescriptor.IsIdentifier` on keys and foreign keys; the builder suggests them for
   joins (it scores identifier names that mention the other data set).
5. **Push-down is optional and must be a superset.** `query.Conditions` carry typed values (DateTime already in UTC
   for DateTime fields). Apply a condition only if your translation can never drop a row the engine's own filter
   would keep (the engine re-applies every filter; text compares case-insensitively, so do not push text equality to
   a case-sensitive collation). Ignoring all conditions is always correct.
6. **Limits.** Respect `MaxRows`. Do not page through everything and truncate in memory.
7. **Recursion.** If your source runs other report queries (like views), guard recursion through
   `context.Properties` (see `ReportViewsDataSource.EnterAsync`).

## Layout and registration

- **A source that wraps an Orchard Core feature** (like Contents and Queries) needs no feature of its own: add a startup
  class in the Reports module (or your module) marked `[Feature(ReportsConstants.BuilderFeature)]` and
  `[RequireFeatures("OrchardCore.X")]`, registering `services.AddScoped<IReportDataSource, MyReportDataSource>();`. See
  `Contents/ContentsReportsStartup.cs` and `Queries/QueriesReportsStartup.cs`.
- **A source for an external system** (a database, a search server, an API) goes in its own module
  `src/Modules/CrestApps.OrchardCore.Reports.<Source>` with `Manifest.cs` (category `Reporting`, dependency
  `ReportsConstants.BuilderFeature`) and `Startup.cs`. Add the project to `CrestApps.OrchardCore.slnx`, the targets
  project `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.csproj`, and the
  tests project references.
- Connection settings for external systems go in configuration (`CrestApps:Reports:<Source>`), documented in
  `docs/configuration.md` with the environment-variable form; never in a design.
- **A module exposing its own stored records** (activities, interactions, chat sessions...) keeps the source in that
  module, in a startup marked with the module's feature and `[RequireFeatures(ReportsConstants.BuilderFeature)]`, so the
  Reports module never depends on it. See `Omnichannel.Managements/Reports/DataSources/`.

## Record-backed sources: `ReportRecordDataSource` and `ReportRecordDataSet<T>`

For records kept in a store, derive from the base classes in `Reports.Abstractions/DataSources` instead of
implementing `IReportDataSource` by hand:

- `ReportRecordDataSet<TRecord>`: declare fields in the constructor with
  `AddField(name, displayName, dataType, record => value, group, isIdentifier, references...)`; implement
  `CanReadAsync(context)` (the permission check) and `LoadAsync(query, take, ct)` (at most `take` records, newest
  first). Values are converted to the field type, enums become their name, and unspecified date-times are treated as UTC.
  A field's references are added to the data set's references automatically.
- `ReportRecordDataSource`: name, display name, description, and `DataSets`. It lists only readable data sets and
  re-checks access on every schema and query call.
- Push dates down with `ReportDateRange.For(query.Conditions, "CreatedUtc")`, which returns inclusive bounds that never
  drop a matching row, and apply them to the index column (`records = records.Where(index => index.CreatedUtc >= value)`).
- Reference other modules' data sets by their stable names, such as
  `new ReportFieldReference("ContactCenter", "DialerProfiles", "ItemId")` or the users data set
  (`ReportsConstants.UsersDataSource`, `UsersDataSet`, `UserIdField`); a reference to a source that is not enabled is
  simply unused.
- Never expose secrets, message bodies, IP addresses, or storage locations.

## Extending the content items source instead

For a custom content field or part, do not write a new source:

- `IContentReportFieldProvider` (keyed by `FieldType`, last registration wins) or
  `services.AddContentReportFieldProvider("MyField", ReportDataType.Text, ContentReportValueMode.Single, "Text")`.
- `IContentReportPartProvider` for part data (all providers are combined).
- Derive fields from `ContentReportField`; use `PrepareAsync` for per-query batch work (it runs only when the report
  uses the field) and `GetValue` to read a value. Name fields `{PartName}.{FieldName}[.{Suffix}]`.

## Tests (required)

Model on `tests/CrestApps.OrchardCore.Tests/Modules/Reports/Contents/` and
`tests/CrestApps.OrchardCore.Tests/Modules/Reports/Designer/` (`InMemoryReportDataSource`, `ReportDesignerTestServices`):

- unauthorized principal and null principal see nothing and get null schemas;
- every field's type and value, including missing values;
- only requested fields are computed; `MaxRows` and `Truncated`;
- each pushed-down condition never drops a row (compare with the engine's result without push-down);
- an end-to-end run through `ReportQueryEngine` joining your source with another.

Use `TestContext.Current.CancellationToken` (xUnit1051) and build with
`-c Release -warnaserror -p:RunAnalyzers=true`.

## Docs (required)

Add the source to `src/CrestApps.Docs/docs/modules/report-builder.md` (or its own technical page linked from
there), its feature to `docs/feature-reference.md`, configuration to `docs/configuration.md`, and the data source name
to the User Manual page `docs/user-manual/report-builder.md` under "Words you will see".
