---
sidebar_label: Report Designer
sidebar_position: 8
title: Report Designer
description: A drag-and-drop report designer with pluggable data sources, joins, formulas, filters, charts, pivots, reusable views, and secure sharing.
user_manual:
  - user-manual/report-designer
---

The **Report Designer** feature of the [Reports](reports.md) module lets people build their own reports in the browser. A designed report reads data sets from one or more **data sources**, joins them, adds calculated fields, filters, groups and aggregates the rows, and renders tables, charts, headline metrics and pivot tables through the same renderer and exporters as every other report. The **Content Reports** feature adds the tenant's content types as a data source, and any module can add more sources.

| | |
| --- | --- |
| **Feature Name** | Report Designer |
| **Feature ID** | `CrestApps.OrchardCore.Reports.Designer` |
| **Dependency** | `CrestApps.OrchardCore.Reports` |

| | |
| --- | --- |
| **Feature Name** | Content Reports |
| **Feature ID** | `CrestApps.OrchardCore.Reports.Contents` |
| **Dependencies** | `CrestApps.OrchardCore.Reports.Designer`, `OrchardCore.Contents` |

How people use the designer is described in the User Manual: [Report Designer](../user-manual/report-designer.md).

## Architecture

The designer is split over the three Reports layers, so the engine can be reused outside Orchard Core and data sources can be added from any module:

- **`CrestApps.OrchardCore.Reports.Abstractions`** holds the data source contract (`IReportDataSource`, `IReportDataSourceManager`, `ReportDataType`, `ReportFieldDescriptor`, `ReportDataSetDescriptor`, `ReportDataSetSchema`, `ReportDataSourceQuery`, `ReportDataCondition`, `ReportDataTable`, `ReportDataValues`) in the `CrestApps.OrchardCore.Reports.DataSources` namespace, and the designed query model (`ReportQueryDefinition`, `ReportDataSetReference`, `ReportJoinDefinition`, `ReportCalculatedField`, `ReportFilterDefinition`, `ReportColumnDefinition`, `ReportSortDefinition`, `ReportVisualDefinition`) in `CrestApps.OrchardCore.Reports.Designer`.
- **`CrestApps.OrchardCore.Reports.Core`** holds the engine: `ReportQueryPlanner` checks a query against the live schemas and compiles it, `ReportQueryEngine` runs it, the formula language lives in `Designer/Expressions`, and `ReportDesignDocumentBuilder` turns a result into a `ReportDocument`.
- **`CrestApps.OrchardCore.Reports`** holds the Orchard Core side: the stores of designed reports, views and share links (document catalogs), permissions and the authorization handler, the designer pages and JSON endpoints, the admin menu, and the built-in **Report views** data source.

A run goes through these steps:

1. **Plan.** Every data set's schema is read with the principal the run reads data for. A data set the principal may not read fails the plan. Joins, calculated fields, columns, filters, sorts and the limit are checked, and every problem is reported at once.
2. **Read.** Each data source returns only the fields the query uses, up to the row limit. Row filters on data sets that no outer join can fill with empty values are offered to the source as `ReportDataCondition`s, which it may apply to read less; the engine always applies every filter again.
3. **Join.** Data sets are hash-joined in order (inner, left, right or full). Keys of different types (text and number) are compared as text; empty keys never match.
4. **Calculate, filter, group.** Row-level calculated fields are evaluated, then fixed filters, then the exposed filters with the viewer's values. When any column is a measure, rows are grouped by the dimension columns.
5. **Shape.** Result filters, sorts, the row limit and the result-size limit are applied.
6. **Render.** Visuals regroup the rows behind the result by fewer dimensions (charts, metrics, pivot tables, totals), so non-additive measures such as averages and distinct counts stay correct.

All processing runs in memory over the rows the sources return, so every source gets joins, formulas and aggregation without implementing them.

### Time zones

A data source returns `DateTime` field values in UTC. The engine converts them to the tenant time zone before anything else, so transforms, formulas, filters and display all work in local time. `Date` fields carry no time zone and are never shifted. Filter values written without a time (`2026-01-31`) cover the whole day.

## Security model

- A designed report or view is authorized like a content item: the broad permission is checked with the item as the resource, and `ReportDesignAuthorizationHandler` grants it to the owner and to the people the report is shared with.

  | Permission | Key | Granted by the handler to |
  | --- | --- | --- |
  | Manage all designed reports and views | `ManageAllReportDesigns` | The owner, when they hold `ManageOwnReportDesigns`. |
  | Design reports and manage own designed reports and views | `ManageOwnReportDesigns` | (implied by `ManageAllReportDesigns`) |
  | View all designed reports | `ViewAllReportDesigns` | The owner; the shared users and roles; everyone for the `Anonymous` role; every signed-in person for the `Authenticated` role. Any designer, for a view. |
  | Share designed reports publicly and through share links | `ShareReportsPublicly` | Nobody: needed to share with the `Anonymous` role and to create share links. |

  Administrators get all four through the default stereotype.

- **A saved report reads data with its owner's current access**, whoever runs it. The owner vouches for what the report shows; viewers need no access to the underlying data. When the owner is deleted or disabled, or loses access to a data set, the report stops running. The designer preview reads with the designer's access.
- A data source must hide every data set the principal may not read. **Content Reports** lists a content type only when the principal holds `ViewContent` for it, including the type-specific permission of a securable type. Because Orchard Core grants `ViewContent` broadly by default, make content types that hold sensitive data **Securable** to control which designers can report on them.
- Exposed filter values from the query string override only filters marked as exposed. Fixed filters cannot be changed or removed by a viewer.
- Share link tokens hold 256 random bits. Only their SHA-256 hash is stored, the full link is shown once, and links can expire, be revoked, require sign-in, and allow or deny export. Shared pages send `noindex` and `no-referrer`. Every link opening, export, creation and revocation is logged.
- Designer payloads are limited to 1 MB, formulas are nested at most 64 levels, views at most 8 levels, and a view that reads itself is refused.

## Pages and URLs

| URL | What it is |
| --- | --- |
| `/Admin/reports/designs` | The designed reports the user can run (Report Designer, or Shared Reports for non-designers). |
| `/Admin/reports/designs/{id}` | Runs a designed report in the admin. |
| `/Admin/reports/designer/create`, `/Admin/reports/designer/edit/{id}` | The designer. |
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
        "Designer": {
          "Limits": {
            "MaxRowsPerDataSet": 50000,
            "MaxJoinedRows": 250000,
            "MaxResultRows": 10000,
            "MaxFilterOptions": 500
          }
        }
      }
    }
  }
}
```

As environment variables:

```text
OrchardCore__CrestApps__Reports__Designer__Limits__MaxRowsPerDataSet=50000
OrchardCore__CrestApps__Reports__Designer__Limits__MaxJoinedRows=250000
OrchardCore__CrestApps__Reports__Designer__Limits__MaxResultRows=10000
OrchardCore__CrestApps__Reports__Designer__Limits__MaxFilterOptions=500
```

| Setting | Default | What it limits |
| --- | --- | --- |
| `MaxRowsPerDataSet` | `50000` | Rows read from one data set. A report that hits it shows a warning. |
| `MaxJoinedRows` | `250000` | Rows the joins may produce. |
| `MaxResultRows` | `10000` | Result rows kept after sorting. |
| `MaxFilterOptions` | `500` | Values listed by a drop-down filter. |

## Adding a data source

Implement `IReportDataSource` and register it as a scoped service. The designer lists it as soon as the feature that registers it is enabled.

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
| `IsIdentifier` | Marks keys, which the designer suggests when joining. |
| `ReportDataSourceContext.Properties` | A bag shared by one run, for example to detect recursion. |

A repository development skill under `.agents/skills/crestapps-report-data-source` walks through every step.

## Content Reports

The **Content Reports** feature registers the **Content items** data source: one data set per content type the user may view, reading published items with YesSql.

| Field | Name |
| --- | --- |
| Metadata | `ContentItemId` (identifier), `ContentItemVersionId`, `DisplayText`, `ContentType`, `Owner` (identifier), `Author`, `CreatedUtc`, `ModifiedUtc`, `PublishedUtc`, `Published` |
| Content field | `{PartName}.{FieldName}`, with `.{Suffix}` for extra values |
| Part data | `TitlePart.Title`, `AutoroutePart.Path`, `ContainedPart.ListContentItemId`, and other common parts |

For example, a `Customer` type with an `Email` text field and an `Order` type with a `Customer` content picker give `Customer.Email` and `Order.Customer` (the first picked id, an identifier), `Order.Customer.ContentItemIds` and `Order.Customer.DisplayText`. Join the order to the customer on `Order.Customer` = `ContentItemId`.

Built-in providers cover the Orchard Core text, numeric, boolean, date, date-time, time, HTML, Markdown, multi-text, link, content picker, user picker, media, taxonomy, localization set and YouTube fields, and the CrestApps phone field. An unknown field type is read as text from its `Text` or `Value` property. Date range filters on the content item index columns are passed down to the query; text filters are not, because database collations may compare case differently from the engine.

### Extending Content Reports

- **A content field type:** implement `IContentReportFieldProvider` (keyed by `FieldType`; the provider registered last wins), or register a one-property provider:

  ```csharp
  services.AddContentReportFieldProvider("RatingField", ReportDataType.Integer, ContentReportValueMode.Single, "Value");
  ```

- **A content part:** implement `IContentReportPartProvider`. Every provider is asked about every part; their fields are combined.
- A field derives from `ContentReportField`. Override `PrepareAsync` for work done once per query (it runs only when a report uses the field), and `GetValue` to read the value.

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
