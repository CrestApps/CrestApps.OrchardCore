---
sidebar_label: Columns and Formulas
title: Columns and Formulas
description: Choose how each column adds up, groups and shows its values, and work out new values with spreadsheet-like formulas.
technical_manual:
  - modules/report-builder/formulas
---

Each column of a report has settings: how its values are combined (added up, counted, averaged), how they are changed first (such as grouping dates by month), and how they are shown (such as currency). When the data does not hold the value you need, a **calculated field** works it out with a formula, like a spreadsheet.

Watch the short video, then follow the steps below.

<YouTube video="report-builder" title="Narrated video of changing how columns add up and are formatted, and adding a calculated field" start={207} end={266} />

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > *a report* > Design |
| **Permission** | Build reports and manage own custom reports and views |
| **Feature** | Report Builder |

<AskYourAdmin />

## Column settings

Click a column on the **Columns** shelf to change it in **Properties** on the right.

| Setting | What it does |
| --- | --- |
| **Header** | The column title. Left empty, the field name is used, with its aggregate and any calculation it is shown as. |
| **Aggregate** | How a group of values is combined: **Count**, **Count distinct**, **Sum**, **Average**, **Minimum**, **Maximum** or **Median**. Pick **None** to group the report by this column. |
| **Transform** | Changes each value first, such as **Month** to see revenue per month. See [Transforms](#transforms). |
| **Format** | How values are shown, such as **N0** (whole numbers), **C2** (currency) or **MMM yyyy** (month and year). Click the question mark next to the box for a list of common formats. See [Formats](#formats). |
| **Show as** | Shows a measure as a quick table calculation, such as a percent of total or a running total, instead of its plain values. See [Show values as a calculation](#show-values-as-a-calculation). |
| **Shelf** | **Columns** or **Rows**. See [Lay out rows and columns](build-a-report.md#lay-out-rows-and-columns). |
| **Hide from tables** | Keeps the column for grouping, charts and filters without showing it in tables. |
| **Colors** | The colors of its header and cells, a color scale, and color rules. See [Colors](#colors). |

The column also shows the technical name of its field, which is how you write the field in a formula.

Most of these settings are also in the column's menu: click the arrow on the column, or right-click it.

### Aggregates

The choices depend on the type of the field:

| Field type | Aggregates you can pick |
| --- | --- |
| Numbers | **Count**, **Count distinct**, **Sum**, **Average**, **Minimum**, **Maximum**, **Median** |
| Dates and times | **Count**, **Count distinct**, **Minimum** (the earliest), **Maximum** (the latest), **Median** |
| Text | **Count**, **Count distinct**, **Minimum** and **Maximum** (the first and last in alphabetical order) |
| Yes or no | **Count**, **Count distinct** |

**Count** counts the rows that have a value; **Count distinct** counts the different values, for example how many different customers ordered. When a field is dropped on **Columns**, numbers are summed and every other field becomes a dimension. Numbers that identify a record, such as an ID, are not summed.

### Transforms

| Field type | Transform | Result |
| --- | --- | --- |
| Text | **Upper case**, **Lower case**, **Trim spaces** | The text changed that way, so *north* and *North* group together. |
| Text | **Number of characters** | The length of the text. |
| Numbers | **Round to whole number** | The number rounded, so 4.6 becomes 5. |
| Dates | **Year** | The year, such as 2026. |
| Dates | **Quarter** | The year and quarter, such as *2026 Q4*. |
| Dates | **Month** | The month, shown as month and year, such as *October 2026*. |
| Dates | **Week** | The Monday the week starts on. |
| Dates | **Day** | The date without its time. |
| Dates | **Day of week** | The name of the day, such as *Friday*, for comparing weekdays. |
| Dates | **Month of year** | The name of the month, such as *October*, for comparing the same month across years. |
| Date and time | **Hour of day** | The hour, from 0 to 23, for finding the busiest hours. |

Dates and times are shown, grouped and filtered in the site's time zone.

### Formats

Leave **Format** empty and values are shown in a way that suits them: whole numbers with thousands separators, other numbers with up to two decimals, dates as short dates, and yes or no values as **Yes** and **No**.

To pick a format, click the question mark next to the **Format** box. A short list of common formats opens, for numbers, for dates and times, or both, depending on the column. Click one to fill the **Format** box. You can also type a format yourself.

| Format | Shows 1234.567 as | Use it for |
| --- | --- | --- |
| **N0** | 1,235 | Whole numbers |
| **N2** | 1,234.57 | Two decimals |
| **C2** | $1,234.57 | Currency, in the site's currency |
| **#,##0** | 1,235 | Whole numbers, written as a pattern |
| **0.0** | 1234.6 | One decimal without thousands separators |

| Format | Shows 0.123 as | Use it for |
| --- | --- | --- |
| **P1** | 12.3% | A fraction as a percentage |
| **P0** | 12% | A percentage without decimals |

| Format | Shows October 9, 2026 at 2:30 PM as | Use it for |
| --- | --- | --- |
| **d** | 10/9/2026 | Short date |
| **yyyy-MM-dd** | 2026-10-09 | Year, month and day |
| **MMM yyyy** | Oct 2026 | Month and year |
| **dddd** | Friday | Day of the week |
| **g** | 10/9/2026 2:30 PM | Date and time |
| **HH:mm** | 14:30 | Time of day, on a 24-hour clock |

The examples are written in US English. The report uses the language settings of the site, so separators, currency signs and month names follow them.

## Show values as a calculation

A measure can show each value compared with the others the table or chart shows, without writing a formula. Click the arrow on the measure and pick **Show as**, or set **Show as** under **Properties**.

| Show as | Each value shows | Example with 300, 150 and 20 |
| --- | --- | --- |
| **Value** | The value itself. This is the default. | 300, 150, 20 |
| **Percent of total** | Its share of the sum of the values it is computed along. | 63.8%, 31.9%, 4.3% |
| **Running total** | The sum of the values up to it. | 300, 450, 470 |
| **Difference from previous** | How much it changed from the value before it. The first value has none. | (empty), -150, -130 |
| **Percent difference from previous** | How much it changed from the value before it, as a percentage of that value. | (empty), -50.0%, -86.7% |
| **Rank** | Its rank, 1 for the largest. Equal values share a rank. | 1, 2, 3 |

The calculation runs over the values in the order they are shown, so sort the report the way you want a running total or a difference to run, such as by month. With subtotals on, it follows the rows as they are grouped.

A column with no **Header** of its own is named after its calculation, so people reading the report know what they see: *% of total Number of rows*, *Running total of Sum of Total*, *Change in Sum of Total*, *% change in Sum of Total* or *Rank of Number of rows*. Type a **Header** to name it yourself; its pill then shows a small badge for the calculation: **%**, **Σ**, **Δ**, **%Δ** or **#**.

Totals rows, subtotal rows and the row totals of a cross-tab keep showing the plain totals, so the bottom of a **Percent of total** column still reads the total revenue rather than 100%.

### Compute along

In a cross-tab, pick which values each calculation runs over with **Compute along**, in the column's menu or under **Properties**:

| Compute along | In a cross-tab | In a chart split into series |
| --- | --- | --- |
| **Down the rows** | Down each column, such as each region's share of a month's revenue. | Along the categories of each series. |
| **Across the columns** | Across each row, such as each month's share of a region's revenue. | Across the series at each category, such as each customer's share of a region's revenue. |
| **Over the whole table** | Over every cell, row by row, such as each cell's share of all the revenue. | Over every point. |

A table without columns across the top, and a chart that isn't split into series, always compute down their rows or along their categories.

### How calculated values are shown

Percentages are shown with one decimal, such as 63.8%. To show them another way, set the column's **Format** to a percent format such as **P0** or **P2**. Ranks are shown as whole numbers. Running totals and differences use the column's own format. A rise is marked with a plus sign, such as +130 or +50.0%, and a fall with a minus sign.

A chart of a percentage reads its axis in percent. Color rules and color scales compare the values shown: a percentage is a fraction, so a rule for *less than 20%* is written *less than 0.2*.

Metrics show their plain totals, whatever the column is shown as.

## Colors

Colors help people spot problem areas at a glance, such as agents with a low score in red. Click a column and set its **Colors** under **Properties**. Click a color box to pick one of the common colors, a **Custom color**, or **None**.

| Setting | What it does |
| --- | --- |
| **Header text**, **Header fill** | The colors of the column header. |
| **Cell text**, **Cell fill** | The colors of every cell of the column. |
| **Color scale** | Shades each number from a low color at the smallest value to a high color at the largest, like a heat map. Pick **Red, yellow, green**, **Green, yellow, red**, **White to blue**, **White to red**, or **Custom** to choose the low, middle and high colors. Choose whether it fills the cells or colors their text. Numbers only. |
| **Color rules** | Color the cells whose value meets a condition, such as *less than 20*, *between 20 and 50*, *equals Sale* or *is empty*. Each rule fills the cell or colors the text. The first rule a value meets wins, so put the most important rule first with the arrow. |

To color numbers in three bands, click **Add red, orange, green**. It adds three rules: below a value in red, between two values in orange, and from the second value in green. Type the two values, such as 20 and 50, in the rules.

Rules win over the color scale, and the scale over the cell colors. A filled cell gets white or dark text, whichever reads better on its color. Totals and subtotals keep their own style. In a cross-tab, the colors of a measure color its cells.

The colors also appear when the report is exported to Excel or PDF, and in reports emailed by workflows. CSV files hold no colors.

## Calculated fields

A calculated field works out a new value with a formula, like a spreadsheet.

1. Under **Calculated fields** in the **Data** pane, click **New**.
2. Enter a **Label** (such as *Profit margin*) and a **Name** (a letter first, then letters, digits and underscores, such as *ProfitMargin*). Formulas use the name, so it cannot be changed later.
3. Write the **Formula**. Click a field or function in the lists on the right to insert it.
4. Click **Check**. The builder shows the result type, or what is wrong. Click **Apply**.

You don't have to type common formulas:

- In the formula window, click **ƒ** next to a field to insert a formula that suits its type around it: **Sum**, **Average**, **Minimum**, **Maximum**, **Median**, **Count** and **Count (distinct)**, and functions such as **Round to 2 decimals**, **Upper case**, **Year**, **Start of the month** or **Days until today**. The choices depend on the type of the field, as they do for a column's aggregates. When the label is empty, it is filled for you, such as *Sum of Total*.
- In the **Data** pane, right-click a field (or click its **⋮** button) and pick **New calculated field**, then a formula. The formula window opens with the formula and a label written. Pick **Custom formula…** to start from the field alone.
- Type in **Search functions** to find a function in the list, which is grouped by category.

You can always edit the formula, combine formulas, or write your own.

The new field appears under **Calculated fields**. Drag it onto **Columns**, **Filters** or a visual like any other field. A calculated field that gives a number is added with the **Sum** aggregate: when each row should show its own value, such as the wait of each call in minutes, set its **Aggregate** to **None** under **Properties**. Click the pencil next to it to change its formula, or **Delete** in the formula window to remove it.

### How to write a formula

- Write fields in square brackets: the data set's name, a dot, and the field's technical name, such as `[Order.CreatedUtc]`. A content field includes its part, such as `[Order.Order.Total]`. Clicking a field in the list writes it for you. Point at a data set's name in the **Data** pane to see the name formulas use for it.
- Write another calculated field by its name, such as `[ProfitMargin]`.
- Put text in single or double quotes, such as `'Large'`. Write numbers with a dot for decimals, such as `0.2`.
- Use `+`, `-`, `*` and `/` for arithmetic, and parentheses to group. Use `&` to join text.
- Compare with `=`, `<>` (not equal), `<`, `<=`, `>` and `>=`, and combine conditions with `AND`, `OR` and `NOT`.
- Use `TRUE`, `FALSE` and `NULL` (no value). Function names can be written in upper or lower case.

### Common formulas

The examples use an *Order* content type with a *Total* field, and a *Customer* content type with *FirstName*, *LastName* and *Email* fields. Use your own fields.

| What you want | Formula |
| --- | --- |
| 20% of each order total | `[Order.Order.Total] * 0.2` |
| A label you can group by | `IF([Order.Order.Total] >= 1000, 'Large', 'Small')` |
| More than two labels | `IFS([Order.Order.Total] >= 1000, 'Large', [Order.Order.Total] >= 100, 'Medium', 'Small')` |
| A full name | `[Customer.Customer.FirstName] & ' ' & [Customer.Customer.LastName]` |
| A name with capital first letters | `PROPER([Customer.Customer.LastName])` |
| The domain of an email address | `SPLIT([Customer.Customer.Email], '@', 2)` |
| Whether an email is at a company | `CONTAINS([Customer.Customer.Email], '@contoso.com')` |
| A value, or another one when it is missing | `COALESCE([Customer.Customer.Email], 'No email')` |
| The age of each order in days | `DATEDIFF('day', [Order.CreatedUtc], TODAY())` |
| A due date 30 days after the order | `DATEADD('day', 30, [Order.CreatedUtc])` |
| The month an order was placed | `DATETRUNC('month', [Order.CreatedUtc])` |
| An amount rounded to two decimals | `ROUND([Order.Order.Total] * 1.08, 2)` |
| Revenue per customer, once per group | `SUM([Order.Order.Total]) / COUNTD([Customer.ContentItemId])` |
| The share of large orders in each group | `SUM(IF([Order.Order.Total] >= 1000, 1, 0)) / COUNT()` |

### Formulas that work once per group

A formula that uses an aggregate function (**SUM**, **AVG**, **MIN**, **MAX**, **COUNT**, **COUNTD**, **MEDIAN**) is worked out once per group, so it can divide one total by another. Inside such a formula, every field must be inside an aggregate function. **COUNT()** with nothing between the parentheses counts the rows of the group.

### Functions

The formula language is Orchard Core's, so formulas work the same way in reports and in Orchard Core's data pipelines. The functions list in the formula window explains every function. They cover:

- **Text**, such as **UPPER**, **LOWER**, **PROPER**, **LEFT**, **RIGHT**, **MID**, **LEN**, **TRIM**, **REPLACE**, **CONTAINS**, **STARTSWITH**, **SPLIT** and **TEXT**.
- **Numbers**, such as **ROUND**, **FLOOR**, **CEILING**, **ABS**, **POWER**, **SQRT** and **MOD**.
- **Dates**, such as **TODAY**, **NOW**, **YEAR**, **MONTH**, **WEEKDAY**, **DATEADD**, **DATEDIFF** and **DATETRUNC**.
- **Logic**, such as **IF**, **IFS**, **SWITCH**, **COALESCE**, **IFNULL**, **ISBLANK** and **IN**.
- **Aggregates**: **SUM**, **AVG**, **COUNT**, **COUNTD**, **MIN**, **MAX** and **MEDIAN**.

Other features of the site can add functions of their own. They appear in the functions list with the others, and work in reports like the built-in ones.

A formula never stops the report: dividing by zero or a missing value gives an empty result. The full list, with every function's arguments, is in the Technical Manual's [formula reference](../../modules/report-builder/formulas.md).

Next: [Filters](filters.md).
