---
sidebar_label: Formula Reference
title: Report Builder Formula Reference
description: The Report Builder formula language - syntax, operators and precedence, types, missing values, aggregate formulas, and every function.
user_manual:
  - user-manual/report-builder/columns-and-formulas
---

Calculated fields (`ReportCalculatedField`) are written in a small, spreadsheet-like formula language. It is parsed by `ExpressionParser`, type-checked and compiled by `ExpressionCompiler`, and its functions are declared in `ExpressionFunctions` (all in `CrestApps.OrchardCore.Reports.Core`, `Designer/Expressions`). The builder lists the functions from the same catalog (`/Admin/reports/builder/api/functions`), so this page and the formula window always agree. How people write formulas in the builder is described in the User Manual: [Columns and formulas](../../user-manual/report-builder/columns-and-formulas.md#calculated-fields).

## Syntax

| Element | How it is written |
| --- | --- |
| A data set field | `[alias.Field]`, such as `[Order.Order.Total]` or `[Order.CreatedUtc]`. The alias is the data set's alias in the report, made from its display name without spaces or punctuation (`Activities`, `Order`, or `Order2` for a second copy). A `]` inside the brackets is doubled (`]]`). |
| A calculated field | `[Name]`, such as `[ProfitMargin]`. Names start with a letter, followed by letters, digits and underscores. |
| The row count | `[$count]`, shown in the builder as **Number of rows**. It is already aggregated. |
| Text | In single or double quotes: `'Large'`, `"Large"`. A quote inside is doubled: `'O''Brien'`. |
| Numbers | With a dot as the decimal separator: `0.2`, `1000`. Whole numbers are integers; numbers with a dot are decimals. |
| Keywords | `TRUE`, `FALSE`, `NULL`, `AND`, `OR`, `NOT`. Keywords and function names are case-insensitive. |
| Function calls | `NAME(argument, ...)`, such as `ROUND([Order.Order.Total], 2)`. |
| Grouping | Parentheses. Formulas may be nested at most 64 levels deep. |

## Operators

From the lowest precedence to the highest:

| Precedence | Operators | Meaning |
| --- | --- | --- |
| 1 | `OR`, `\|\|` | Either condition is true. |
| 2 | `AND`, `&&` | Both conditions are true. |
| 3 | `NOT`, `!` | The condition is not true. |
| 4 | `=`, `==`, `!=`, `<>`, `<`, `<=`, `>`, `>=` | Comparisons. The result is a boolean. |
| 5 | `&` | Joins two values as text. |
| 6 | `+`, `-` | Adds and subtracts. |
| 7 | `*`, `/`, `%` | Multiplies, divides, and takes the remainder. |
| 8 | unary `-`, `+` | Negates a number. |

Operators of the same precedence apply from left to right.

### Types of operators

- `+` joins text when either side is text. Adding a number to a date or date-time adds that many days; subtracting a number from a date subtracts days; subtracting two dates gives the number of days between them, as a decimal.
- `-`, `*`, `/` and `%` need numbers (apart from the date cases above); `/` always gives a decimal. Integer arithmetic that overflows continues in decimals.
- `&` always gives text and treats a missing value as empty text, so `[First] & ' ' & [Last]` works when one part is missing.
- Comparisons compare numbers as numbers, dates as dates (text on the other side is read as a date), and text ignoring case.
- A value counts as true when it is `TRUE`, a non-zero number, or a non-empty text other than `false` or `0`.

### Missing values

- A missing operand gives a missing result for `+`, `-`, `*`, `/` and `%`. Dividing by zero, or a remainder by zero, gives a missing result.
- `=` treats a missing value and empty text as equal to each other; `<`, `<=`, `>` and `>=` with a missing side are false.
- A function given a value it cannot convert, such as text for `ROUND`, gives a missing result. A formula never stops a report while it runs.

## Result types

The formula window shows the type a formula returns, which decides the aggregates, transforms, filter conditions and formats offered for it:

| Type | Comes from |
| --- | --- |
| Text | Text fields, text literals, `&`, `+` with text, the text functions. |
| Integer | Whole-number fields and literals, `LEN`, `FIND`, `YEAR` and the other date parts, `COUNT`, `COUNTD`, `ROUND` without decimals, `FLOOR`, `CEILING`, `INTEGER`, `SUM` of integers. |
| Decimal | Decimal fields and literals, `/`, `AVG`, `MEDIAN`, `SUM` of decimals, `ROUND` with decimals, `POWER`, `SQRT`, `NUMBER`, subtracting two dates. |
| Boolean | Comparisons, `AND`, `OR`, `NOT`, `CONTAINS`, `STARTSWITH`, `ENDSWITH`, `ISNULL`, `ISBLANK`, `IN`. |
| Date | `TODAY`, `DATE`, date fields. |
| Date-time | `NOW`, `DATETIME`, date-time fields. |

`IF`, `IFS`, `SWITCH`, `COALESCE`, `IFNULL`, `LEAST`, `GREATEST` and `MOD` return the common type of their results. `DATEADD` and `DATETRUNC` return the type of their date argument.

## Row-level and aggregate formulas

- A **row-level** formula is evaluated once per joined row, before filters and grouping. It can be a dimension, a measure (with an aggregate on its column), or a filter.
- A formula that calls an aggregate function (`SUM`, `AVG`, `AVERAGE`, `MEDIAN`, `MIN`, `MAX`, `COUNT`, `COUNTD`) is **aggregated**: it is evaluated once per group, after grouping, so it can divide one total by another, such as `SUM([Order.Order.Total]) / COUNTD([Customer.ContentItemId])`. Charts, metrics, pivot tables and totals evaluate it again over their own groups, so a ratio stays a true ratio in a total.
- In an aggregated formula, every field must be inside an aggregate function: the formula may not mix aggregated values with row-level fields. The argument of an aggregate function is a row-level expression, such as `SUM(IF([Order.Order.Status] = 'Paid', [Order.Order.Total], 0))`.
- Aggregate functions cannot be nested, and an aggregated field (including `[$count]` and another aggregated calculated field) cannot be aggregated again.
- Calculated fields can use other calculated fields by name.

## Functions

### Aggregate

| Function | What it does |
| --- | --- |
| `SUM(number)` | Adds the values of the group. |
| `AVG(number)` | Averages the values of the group. |
| `AVERAGE(number)` | Same as AVG. |
| `MEDIAN(number)` | Returns the middle value of the group. |
| `MIN(value)` | Returns the smallest value of the group. |
| `MAX(value)` | Returns the largest value of the group. |
| `COUNT([value])` | Counts the rows of the group, or the rows where the value is present. |
| `COUNTD(value)` | Counts the distinct values of the group. |

### Logical

| Function | What it does |
| --- | --- |
| `IF(condition, then, [else])` | Returns one value when the condition is true and another when it is not. |
| `IIF(condition, then, else)` | Same as IF. |
| `IFS(condition1, value1, condition2, value2, ..., [else])` | Returns the value of the first condition that is true. |
| `SWITCH(value, match1, result1, match2, result2, ..., [else])` | Returns the result paired with the first match of the value. |
| `COALESCE(value1, value2, ...)` | Returns the first value that is not missing. |
| `IFNULL(value, fallback)` | Returns the fallback when the value is missing. |
| `ISNULL(value)` | Returns true when the value is missing. |
| `ISBLANK(value)` | Returns true when the value is missing or empty text. |
| `IN(value, option1, option2, ...)` | Returns true when the value equals any option. |
| `NOT(condition)` | Returns true when the condition is not true. |

### Text

| Function | What it does |
| --- | --- |
| `UPPER(text)` | Converts text to upper case. |
| `LOWER(text)` | Converts text to lower case. |
| `TRIM(text)` | Removes leading and trailing spaces. |
| `PROPER(text)` | Capitalizes the first letter of each word. |
| `LEN(text)` | Returns the number of characters in the text. |
| `LEFT(text, count)` | Returns the first characters of the text. |
| `RIGHT(text, count)` | Returns the last characters of the text. |
| `MID(text, start, [count])` | Returns characters from the middle of the text. The first character is at position 1. |
| `SUBSTRING(text, start, [count])` | Same as MID. |
| `REPLACE(text, find, replacement)` | Replaces every occurrence of a text, ignoring case. |
| `CONCAT(value1, value2, ...)` | Joins values into one text. Missing values are skipped. |
| `FIND(text, search)` | Returns the position of the search text (starting at 1), or 0 when it is not found. |
| `CONTAINS(text, search)` | Returns true when the text contains the search text, ignoring case. |
| `STARTSWITH(text, search)` | Returns true when the text starts with the search text, ignoring case. |
| `ENDSWITH(text, search)` | Returns true when the text ends with the search text, ignoring case. |
| `SPLIT(text, separator, index)` | Splits the text and returns the part at the index (starting at 1). |
| `TEXT(value, [format])` | Converts a value to text, optionally with a .NET format such as 'N2' or 'yyyy-MM'. The invariant culture is used, so the text is the same for every viewer. |

### Number

| Function | What it does |
| --- | --- |
| `ABS(number)` | Returns the absolute value of a number. |
| `ROUND(number, [decimals])` | Rounds a number to a number of decimals (0 by default). |
| `FLOOR(number)` | Rounds a number down to a whole number. |
| `CEILING(number)` | Rounds a number up to a whole number. |
| `POWER(number, exponent)` | Raises a number to a power. |
| `SQRT(number)` | Returns the square root of a number. |
| `MOD(number, divisor)` | Returns the remainder of a division. |
| `SIGN(number)` | Returns -1, 0, or 1 for a negative, zero, or positive number. |
| `NUMBER(value)` | Converts a value to a decimal number. |
| `INTEGER(value)` | Converts a value to a whole number, dropping any fraction. |
| `LEAST(value1, value2, ...)` | Returns the smallest of the values. |
| `GREATEST(value1, value2, ...)` | Returns the largest of the values. |

### Date

| Function | What it does |
| --- | --- |
| `NOW()` | Returns the current date and time, in the tenant time zone. |
| `TODAY()` | Returns the current date, in the tenant time zone. |
| `YEAR(date)` | Returns the year of a date. |
| `QUARTER(date)` | Returns the quarter of a date (1 to 4). |
| `MONTH(date)` | Returns the month of a date (1 to 12). |
| `DAY(date)` | Returns the day of the month of a date. |
| `WEEK(date)` | Returns the ISO week number of a date. |
| `WEEKDAY(date)` | Returns the day of the week of a date, from 1 (Monday) to 7 (Sunday). |
| `HOUR(date)` | Returns the hour of a date-time (0 to 23). |
| `MINUTE(date)` | Returns the minute of a date-time (0 to 59). |
| `DATE(year, month, day)` or `DATE(value)` | Builds a date from its parts, or converts a value to a date. |
| `DATETIME(value)` | Converts a value to a date-time. |
| `DATEADD('part', number, date)` | Adds a number of years, quarters, months, weeks, days, hours, minutes, or seconds to a date. |
| `DATEDIFF('part', start, end)` | Counts the years, quarters, months, weeks, days, hours, minutes, or seconds from start to end. |
| `DATETRUNC('part', date)` | Truncates a date to the start of its year, quarter, month, week, day, hour, or minute. |

The `part` of `DATEADD`, `DATEDIFF` and `DATETRUNC` is case-insensitive and accepts these names:

| Part | Also written |
| --- | --- |
| `year` | `years`, `yyyy`, `yy` |
| `quarter` | `quarters`, `q`, `qq` |
| `month` | `months`, `mm`, `m` |
| `week` | `weeks`, `wk`, `ww` |
| `day` | `days`, `dd`, `d` |
| `hour` | `hours`, `hh` |
| `minute` | `minutes`, `mi`, `n` |
| `second` | `seconds`, `ss`, `s` (not for `DATETRUNC`) |

`DATEDIFF` counts calendar boundaries for years, quarters and months (`DATEDIFF('month', '2026-01-31', '2026-02-01')` is 1), whole weeks and days between the dates, and whole hours, minutes and seconds between the times. Weeks start on Monday. Date-time fields are in the tenant time zone before any formula runs.

## Examples

| Formula | Result |
| --- | --- |
| `[Order.Order.Total] * 0.2` | 20% of each order total. |
| `IF([Order.Order.Total] >= 1000, 'Large', 'Small')` | A label for each order, which can be grouped by. |
| `SWITCH([Activities.Status], 'Completed', 'Done', 'Canceled', 'Done', 'Open')` | Maps several statuses to fewer labels. |
| `[Customer.Customer.FirstName] & ' ' & [Customer.Customer.LastName]` | A full name. |
| `DATEDIFF('day', [Order.CreatedUtc], TODAY())` | The age of each order in days. |
| `DATETRUNC('month', [Order.CreatedUtc])` | The first day of each order's month. |
| `[Order.PublishedUtc] - [Order.CreatedUtc]` | Days from creating an item to publishing it, with a fraction. |
| `SUM([Order.Order.Total]) / COUNTD([Customer.ContentItemId])` | Revenue per customer, worked out once per group. |
| `SUM(IF([Activities.AiEscalated], 1, 0)) / COUNT()` | The share of activities escalated by AI in each group. |
| `COALESCE([Customer.Customer.Phone], [Customer.Customer.Email], 'No contact')` | The first contact detail present. |
