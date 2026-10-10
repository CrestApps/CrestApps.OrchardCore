---
sidebar_label: Join Data Sets
title: Join Data Sets
description: Report on data that lives in two places, such as customers and their orders, by joining data sets on the columns they share.
technical_manual:
  - modules/report-builder/data-sources
  - modules/report-builder/large-data
---

To report on data that lives in two places (for example customers and their orders), add both data sets, then join them on the columns they share. A join tells the builder which rows belong together, such as each order and the customer who placed it.

Watch the short video, then follow the steps below.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-joins.jpg" aria-label="Video: joining two data sets on the Data model tab and choosing which rows to keep">
  <source src="/img/docs/report-builder-joins.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-joins.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > *a report* > Data model |
| **Permission** | Build reports and manage own custom reports and views |
| **Feature** | Report Builder |

<AskYourAdmin />

## Joins the builder makes for you

Data sets your site already links are joined for you when you add them:

- an item's content picker to the content item it picks, such as an order's *Customer* to the customer;
- a contact to the account whose list holds it;
- an item's owner, and a user picker, to **Users**;
- records of other features to the records they point to, such as an activity to its disposition or campaign, or an activity's assigned user to **Users**.

Once a report has a data set, **Add data set** opens on **Related**, which lists the data sets linked to the ones you already have.

When you add a second data set that is not linked this way, the builder suggests a pair of matching columns when it recognizes one, and opens the join so you can check it.

## Join on the Data model tab

1. Open the **Data model** tab. Each data set is a card listing its columns; key columns are marked with a key and listed first. Drag a card by its title to move it.
2. Drag a column from one card onto the matching column of another card, such as the order's *Customer* onto the customer's *Content item ID*. A line now connects the two columns.
3. To match on more than one column, drag another pair. Every pair must be equal for two rows to match.
4. Click the line or its badge to open the join on the right, then pick which rows to keep.

## Choose which rows to keep

| Choice | Keeps |
| --- | --- |
| **Only rows that match on both sides** | Customers that have orders, with each of their orders. |
| **All rows before, matching rows of this data set** | Every customer, with their orders when they have any. |
| **All rows of this data set, matching rows before** | Every order, with its customer when one matches. |
| **All rows of both sides** | Everything from both data sets. |

"Before" means the data sets added earlier: data sets are joined in the order you added them. Rows with an empty value in a matching column never match.

## Change a join on the Design tab

On the **Design** tab, the **Joins** row above **Columns** lists every join; a red join still needs matching columns. Click a join there to change it in **Properties**:

- **Join type** is the choice of rows to keep.
- **Matching columns** lists each pair: a **Column of the data sets before** and a **Column of the joined data set**.
- **Add matching columns** adds another pair.

You can also click the link icon on a data set's card in the **Data** pane to edit its join.

## Tips

- One customer with three orders gives three rows. Use **Count distinct** on the customer, rather than **Count**, to count customers in such a report.
- A join on an ID field is the most reliable. Joining on names can match the wrong rows when two records share a name.
- To compare the same data in two roles, such as who created an activity and who it is assigned to, add **Users** twice and join each copy to a different field.

Next: [Kinds of reports](kinds-of-reports.md).
