---
sidebar_label: Dashboards
title: Interactive Dashboards
description: Show data grouped several ways on one page, frame visuals in scrolling containers, and let people click a row or a chart to filter the rest.
technical_manual:
  - modules/report-builder/interactive-dashboards
---

A dashboard puts everything about a question on one page: a scorecard per queue, a scorecard per agent, the list of single calls, and a trend by day. Three things in the builder make that work:

- **Queries**: each table or chart can group the data its own way, so one page can show totals per agent next to a trend per day.
- **Containers**: framed blocks with a title band. A container with a height scrolls what is inside it, so long tables do not push the rest of the page down.
- **When clicked**: clicking an agent in one table filters the other visuals to that agent, and clicking a day on a chart filters the calls to that day.

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > *a report* > Design |
| **Permission** | Build reports and manage own custom reports and views |
| **Feature** | Report Builder |

<AskYourAdmin />

## Show data grouped several ways

A report starts with one query: the data sets, columns, filters and sorting you set on the **Design** and **Data model** tabs. Every visual of that query shows the same result, grouped by the same dimensions. To show the same data grouped another way, add a query.

1. In the bar above the tabs, next to **Query**, click **Add query**. The new query starts as a copy of the one you were editing, so it reads the same data sets and has the same filters.
2. Give it a name, such as *By day*, in the box next to the query list.
3. Change its columns: for a trend, keep the date with **Transform** set to **Day** and the measures you want; remove the columns you do not need.
4. Add a visual. A visual shows the query you are editing when you add it. To move a visual to another query, pick it in the visual's **Query** setting.

Click a visual in the **Visuals** list to change it: the builder switches to its query, so its settings list that query's columns. Pick another query in the **Query** list to edit its columns and filters.

The filters that people who run the report can change are shared between queries when the query was copied: one **Placed** filter above the report filters every query that has it. A filter you add to only one query filters only that query.

To remove a query, pick it and click **Remove query**. Its visuals are removed with it.

## Frame visuals in containers

1. In the **Containers** card, under **Visuals**, click **Add container**.
2. Give it a **Title**, shown in the band across its top, a **Width**, and a **Height**. Pick **Grow with its visuals** for a container that is as tall as what it holds.
3. Click a visual and pick the container in its **Container** setting. A container is placed where its first visual is, and holds its visuals in their order.

The widths of the visuals in a container divide the container's width: two visuals at **Half** in a container sit side by side. When the visuals are taller than the container, they scroll inside it, and the column headers of a table stay in view while you scroll.

Exports keep the containers' layout where the format can:

| Format | How containers are exported |
| --- | --- |
| Excel | Each container is a worksheet named after it, with its visuals one under the other, each under its title. A visual outside a container is a worksheet of its own, named after the visual. Chart values are numbers, so you can chart or add them up in Excel. |
| PDF | Each container's title is a band across the page, with its visuals under it. A chart is drawn as on the page. |
| CSV | A CSV file has no sheets, so it lists the data of every visual in order. A chart is exported as its data. |

## Filter by clicking

1. Click a table or a chart in the **Visuals** list.
2. Under **When clicked**, pick the column whose value a click picks in **Pick the value of**. A table offers the dimensions it shows; a chart offers its categories and its series.
3. Under **Filter these visuals**, check the visuals the click filters.
4. A visual of another query filters the same field when its query reads it. When it does not, the builder says so: pick the field to filter in the box under the visual.

People who run the report can then:

| They do this | What happens |
| --- | --- |
| Click a row of the table, or a bar, point or slice of the chart | The visuals it is linked to show only the rows with that value. The row or mark stays highlighted. |
| Hold **Ctrl** (or **Cmd** on a Mac) and click | Adds the value to the ones picked, or removes it. |
| Click the picked value again, or **Clear selection** | Shows every row again. |

The visual they click keeps all of its rows, so they can pick another value. Only the visuals it is linked to load again; the rest of the page stays as it is.

Changing a filter above the report keeps the values picked, and an export is filtered the same way as the page. In Excel and PDF, each visual filtered by a click says so above its data, such as **Filtered by Agents: Alice**. The address of the page keeps them too, so reloading it or sending it to someone shows the same view.

You can try clicks in the **Preview** while you design.

### Clicking a date

When the column a click picks is a date kept to its **Day**, **Week**, **Month**, **Year** or **Hour**, a click filters the other visuals to that period: clicking *Oct 15* on a trend by day shows the calls of October 15. Picking several periods with **Ctrl** shows everything from the first to the last.

A click can pick text, numbers, dates and yes or no values. It cannot pick a column that adds up values (a measure), or a date kept to its **Day of week**, **Month of year** or **Quarter**, since those values do not belong to one period.

## An example: a call quality dashboard

1. Build the main query on your call data: **Agent** as a dimension and the averages of the quality scores as measures. Add a **Table** of it, titled *Agent scorecard*.
2. Click **Add query**, name it *By queue*, and change its dimension to **Queue**. Add a **Table**, titled *Queue scorecard*.
3. Click **Add query**, name it *Calls*, and keep one row per call: the date, the transcript, the agent and the disposition, with no measures. Add a **Table**, titled *Call selection*.
4. Click **Add query**, name it *By day*, with the call date kept to its **Day** and the average score. Add a **Line** chart, titled *Score trend*.
5. Add containers: *Queue scorecard* and *Agent scorecard* at **Full** width and 320 px high, *Call selection* and *Score trend* at **Half** width and 400 px high. Place each table and the chart in its container.
6. On *Agent scorecard*, under **When clicked**, pick **Agent**, and check *Call selection* and *Score trend*.
7. On *Score trend*, pick the date under **When clicked**, and check *Call selection*.

Clicking an agent now shows that agent's calls and trend; clicking a day on the trend narrows the calls to that day.

Next: [Join data sets](joins.md).
