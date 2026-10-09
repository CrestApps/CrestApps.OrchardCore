---
sidebar_label: Report Builder
sidebar_position: 36
title: Report Builder
description: Build your own reports with drag and drop, join data sets, add formulas, filters, charts and pivot tables, and share them.
technical_manual:
  - modules/report-builder
---

The **Report Builder** lets you build your own reports without writing code. You pick the data you need (for example your customers and their orders), drag fields onto the report, choose how numbers are added up, and add charts, headline numbers, and pivot tables. You can save a report, put it in the admin menu so you can run it again with one click, and share it with people, roles, or a link.

| | |
| --- | --- |
| **Menu** | Reports > Report Builder, and Reports > Report Views |
| **Permissions** | Build reports and manage own custom reports and views (to design); Share custom reports publicly and through share links (to share with everyone or create links); Manage all custom reports and views (to change other people's reports) |
| **Features** | Report Builder. Your content types (such as customers or orders) are offered as data when Contents is on, and your saved queries when Queries is on. |

<AskYourAdmin />

People who only run reports that were shared with them need no permission: they see **Reports > Shared Reports**, and any report pinned to the menu that they may open.

## Words you will see

| Word | What it means |
| --- | --- |
| **Data source** | Where data comes from. **Content items** offers each content type of the site; **Queries** offers the saved queries you may run (such as SQL or search queries); **Report views** offers the views you and your team saved. Other features can add more sources. |
| **Data set** | One table of data from a source, such as the *Customer* content type. |
| **Field** | One piece of information in a data set, such as *Email* or *Total*. |
| **Dimension** | A column the report groups by, such as *Region*. |
| **Measure** | A column that adds up values, such as the *Sum* of *Total*. When a report has a measure, it shows one row per group of dimensions. |
| **View** | A saved, reusable data set: it joins, filters, and calculates once, and other reports can use its columns as fields. |

## Design a report

1. Open **Reports > Report Builder** and click **New Report**.
2. Type a title at the top of the page.
3. Click **Add data set**. Pick a data source on the left (or **All**), then click **Add** on the data set's card. Its fields appear in the **Data** pane on the left, grouped by part.
4. Drag fields onto **Columns**. You can also click the column icon next to a field. Numbers are added as a **Sum** by default; text, dates and identifiers become dimensions.
5. Click a column to change it in **Properties** on the right (see [Column settings](#column-settings)).
6. The **Preview** under the shelves refreshes as you work. Click **Refresh** to run it again.
7. Click **Publish** (or press Ctrl+S). Unfinished designs can be published: the builder lists their problems, and the report shows them when it runs until they are fixed.

Drag a column along the **Columns** shelf to move it. Click the cross on a column to remove it.

The builder fills the window, and each pane scrolls on its own. Collapse the **Data** pane, the **Properties and visuals** pane, or the **Columns and filters** section to give the preview more room; the builder remembers your choice.

## Drafts, publishing and versions

The builder saves your changes as you work, from the first change to a new report. The words next to the title say **Saving…**, then **Draft saved**, and you can close or refresh the page at any time. Saved changes are a **draft**: people who run the report keep seeing the published version until you click **Publish**.

- A new report you have not published yet is listed on the **Report Builder** page under **Not published yet**. Click **Continue** to keep working on it, or **Delete** to throw it away. Only you, and the people who manage every report, can see it.

- A bar above the tabs shows that the report has **unpublished changes**, who made them and when. Click **Discard changes** to go back to the published version, or **Publish** to make them live.
- Each time you publish a change, the builder keeps a **version**. Click **Versions** to list them with who published each one and when. **Preview** shows a version; **Restore** copies it into the draft, so you can check it and publish it. Restoring never changes what people run until you publish.
- Old versions are removed after a while; your administrator decides how many are kept.

## Work on a report with others

Two people can open the same report, but only one change can win, so the builder protects your work:

- When someone else changed the report since you opened it, your next change is **not saved** and a yellow bar says who changed it. Click **Reload their changes** to see their version (your unsaved changes are lost), or **Keep mine** to save your version over theirs.
- When real-time updates are turned on for your site, the builder also shows the initials of the others who have the report open, warns that your changes can conflict, and tells you as soon as someone saves, publishes or discards changes, with a **Reload** button.

## Combine data sets

To report on data that lives in two places (for example customers and their orders), add both data sets, then join them on the columns they share.

1. Open the **Data model** tab. Each data set is a card listing its columns; key columns are marked with a key and listed first. Drag a card by its title to move it.
2. Drag a column from one card onto the matching column of another card, such as the order's *Customer* onto the customer's *Content item id*. A line now connects the two columns.
3. To match on more than one column, drag another pair. Every pair must be equal for two rows to match.
4. Click the line or its badge to open the join on the right, then pick which rows to keep:

   | Choice | Keeps |
   | --- | --- |
   | **Only rows that match on both sides** | Customers that have orders, with each of their orders. |
   | **All rows before, matching rows of this data set** | Every customer, with their orders when they have any. |
   | **All rows of this data set, matching rows before** | Every order, with its customer when one matches. |
   | **All rows of both sides** | Everything from both data sets. |

When you add a second data set, the builder suggests a pair of matching columns when it recognizes one, and opens the join so you can check it. Data sets your site already links are joined for you: an order's content picker to the customer it picks, a contact to the account whose list holds it, and an item's owner to **Users**. Once a report has a data set, **Add data set** opens on **Related**, which lists the data sets linked to the ones you already have. On the **Design** tab, the **Joins** row above **Columns** lists every join; a red join still needs matching columns. Click a join there to change it in **Properties**, where **Add matching columns** adds another pair.

## Column settings

| Setting | What it does |
| --- | --- |
| **Header** | The column title. Left empty, the field name is used. |
| **Aggregate** | How a group of values is combined: **Count**, **Count distinct**, **Sum**, **Average**, **Minimum**, **Maximum** or **Median**. Pick **None** to group the report by this column. |
| **Transform** | Changes each value first: **Upper case**, **Lower case**, **Trim spaces**, **Number of characters**, **Round to whole number**, or for dates **Year**, **Quarter**, **Month**, **Week**, **Day**, **Day of week**, **Month of year** and **Hour of day**. Use **Month** to see revenue per month. |
| **Format** | How values are shown, such as `N0` (whole numbers), `N2`, `C2` (currency), `P1` (percentage) or `MMM yyyy`. |
| **Hide from tables** | Keeps the column for grouping, charts and filters without showing it in tables. |

Use **Sort** under the shelves to order the rows, and **Top rows** to keep only the first rows (for example the top 10 customers by revenue).

## Filter the data

Drag a field onto **Filters**, or click the filter icon next to it, then set it in **Properties**:

| Setting | What it does |
| --- | --- |
| **Applies to** | **Rows, before grouping** filters the data itself. **Result, after grouping** filters the finished rows, for example customers whose total is above 1,000. |
| **Condition** | Such as **is**, **contains**, **is between**, **is one of**, **is empty**, or for dates **is in the last number of days**. |
| **Value** | What to compare with. A date without a time covers the whole day. |
| **Let viewers change this filter** | Shows the filter above the report so the people who run it can change it. The values you set become its defaults. |
| **Filter label** | The label viewers see. |
| **Control** | How viewers pick values: **Automatic**, **Text box**, **Drop-down list**, **List with several choices**, **Date range**, **Number range** or **Yes or no**. Lists show the values found in the data. |

Filters that viewers cannot change always apply, and viewers cannot see or remove them.

## Calculated fields

A calculated field works out a new value with a formula, like a spreadsheet.

1. Under **Calculated fields**, click **New**.
2. Enter a **Label** and a **Name** (letters, digits and underscores).
3. Write the **Formula**. Click a field or function on the right to insert it. Fields are written in square brackets, such as `[Order.Order.Total]`.
4. Click **Check**. The builder shows the result type, or what is wrong. Click **Apply**.

Examples:

| Formula | Result |
| --- | --- |
| `[Order.Order.Total] * 0.2` | 20% of each order total. |
| `IF([Order.Order.Total] >= 1000, 'Large', 'Small')` | A label for each order, which you can group by. |
| `[Customer.Customer.FirstName] & ' ' & [Customer.Customer.LastName]` | A full name. |
| `DATEDIFF('day', [Order.CreatedUtc], TODAY())` | The age of each order in days. |
| `SUM([Order.Order.Total]) / COUNTD([Customer.ContentItemId])` | Revenue per customer, worked out once per group. |

A formula that uses an aggregate function (**SUM**, **AVG**, **MIN**, **MAX**, **COUNT**, **COUNTD**, **MEDIAN**) is worked out once per group, so it can divide one total by another. Inside such a formula, every field must be inside an aggregate function.

The functions list in the formula dialog explains every function. They cover text (such as **UPPER**, **LEFT**, **REPLACE**, **CONTAINS**), numbers (**ROUND**, **ABS**, **POWER**), dates (**YEAR**, **DATEADD**, **DATEDIFF**, **DATETRUNC**), and logic (**IF**, **IFS**, **SWITCH**, **COALESCE**, **IN**). A formula never stops the report: dividing by zero or a missing value gives an empty result.

## Visuals

The **Visuals** card on the right lists what the report shows, in order. Without visuals, the report shows one table. Click a visual to change it.

| Visual | What it shows |
| --- | --- |
| **Table** | The result rows. Pick the **Columns shown** and whether to **Show totals**. |
| **Chart** | A **Bar**, **Horizontal bar**, **Line**, **Area**, **Pie** or **Doughnut** chart of the **Values** by **Categories**. **Split into series by** draws one series per value of another column, and **Stack series** stacks them. |
| **Metrics** | Headline numbers: the total of each value column over the whole report. |
| **Pivot table** | A cross-tab: **Rows** down the side, the values of **Columns across** along the top, and the **Value** in each cell, with optional totals. |

You can also drag a field from the **Data** pane straight onto a visual's **Categories**, **Values**, **Split into series by**, **Rows**, **Columns across** or **Value** box. The builder adds the column for you: a number dropped on values is summed, and other fields dropped on values are counted.

Set each visual's **Width** to place visuals side by side. Charts, metrics, pivot tables and totals add up the underlying rows again, so an average stays a true average.

## Reuse data with views

A view saves a prepared data set (joined, filtered and calculated) so other reports don't have to repeat that work.

1. Open **Reports > Report Views** and click **Add View**.
2. Design it like a report: add data sets, relationships, calculated fields, filters and columns.
3. Save it.

In any report, click **Add data set**, pick **Report views** as the data source, and pick your view. Its columns appear as fields. A view cannot use itself, directly or through other views, and a view that a report uses cannot be deleted.

## Put a report in the menu

On the **Settings** tab, check **Show in the admin menu**. The report appears under **Reports** in the group you enter as **Category** (or *Custom Reports*), for everyone who can open it. Use **Description** for the text shown above the report.

## Share a report

On the **Sharing** tab:

| Setting | What it does |
| --- | --- |
| **People** | Search by user name or email and pick the people who may run the report. |
| **Roles** | Everyone in a checked role may run the report. **Authenticated** means everyone who is signed in. **Anonymous** means everyone, including visitors who are not signed in, and needs the *Share custom reports publicly and through share links* permission. |
| **Let people the report is shared with export it** | On the **Settings** tab. Turn it off to let them view the report but not download it. |

A shared report reads data with **your** access: people see what the report shows even when they could not open that data themselves. Share only what they should see. If your account is disabled or deleted, your reports stop running; someone who designs reports can duplicate them and share the copies again.

People who cannot open the admin can open a shared report at its own page outside the admin.

## Create a share link

A share link opens one report for anyone who has the link, without an account. You need the *Share custom reports publicly and through share links* permission, and the report must be saved.

1. On the **Sharing** tab, under **Share links**, enter a **Note** that says what the link is for.
2. Optionally set **Expires**, **Allow export**, and **Require sign-in** (the link then works only for people who are signed in).
3. Click **Create link**, then **Copy**. For security, the full link is shown only once.

To stop a link working, click **Revoke**. The list shows each link's note, the first characters of its address, when it expires, and whether it is **Active**, **Expired** or **Revoked**.

## Run, export, copy and delete

**Reports > Report Builder** (or **Shared Reports**) lists every report you can open. From the list:

- **Run** opens the report. Change the filters and click **Show**. Click **Export** to download it as CSV, or as Excel when the Reports (OpenXml) feature is on.
- **Edit** opens the builder (when you may change the report).
- **Duplicate** makes your own copy. The copy is not shared with anybody.
- **Delete** removes the report and all its share links.

## Troubleshooting

| Problem | What to do |
| --- | --- |
| A data set says it is not available to you | You may not view that content type. Ask your administrator. |
| The report says it cannot run because its owner has no active account | Ask someone who designs reports to duplicate it, and share the copy again. |
| A warning says only the first rows were read | The data set is larger than the builder reads at once. Add filters that narrow the data, or ask your administrator to raise the limits. |
| A formula says it mixes aggregated values with row-level fields | Wrap every field in an aggregate function, or remove the aggregate function. |
| The **Anonymous** role cannot be checked | You need the *Share custom reports publicly and through share links* permission. |
