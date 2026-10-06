---
sidebar_label: Import and Export
title: Bulk Import and Export
description: Load many content items at once from a CSV or Excel file, and download content items to a file.
technical_manual:
  - modules/content-transfer
---

Bulk import creates or updates many content items from one spreadsheet, and bulk export downloads content items to a spreadsheet. Use them to load a purchased list of leads, move records from another system, fix many records at once in Excel, or hand a list to someone outside the site.

| | |
| --- | --- |
| **Menu** | Content > Import; Content > Export |
| **Permission** | List content transfer entries (to open the Import page); Import content items from file, or Import *type* content items from file for one type; Export content items from file; Delete content transfer entries |
| **Feature** | Content Transfer (CSV files); Content Transfer (OpenXml) adds Excel files |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of enabling Content Transfer, exporting a content type, and starting a bulk import">
  <source src="/img/docs/content-transfer.mp4" type="video/mp4" />
</video>

## File formats

| Format | When you can use it |
| --- | --- |
| **CSV** (`.csv`) | Always. |
| **Excel workbook** (`.xlsx`) | When the **Content Transfer (OpenXml)** feature is on. |

Older Excel files (`.xls`) are not supported. Save them as `.xlsx` or CSV first.

Every import file follows the same rules:

- The very first row holds the column names.
- The order of the columns does not matter, and columns the content type does not know are ignored.
- The import page lists every column, whether it is required, what it holds and, for some columns, the values it accepts. Download a template from that page to start with the right columns.

## Which content types appear

Every content type appears on the import and export screens unless an administrator turns it off. On the content type's settings, **Allow bulk import** and **Allow bulk export** are ticked by default. Untick one to keep a type off that screen.

## Import a file

1. Open **Content > Import**. The **Bulk Import** page lists earlier imports.
2. Click **Import** and pick the content type. When only one type can be imported, the button reads **Import** followed by the type's name.
3. Read the **File Requirements** card, and download a template if you need one.
4. Choose your file.
5. Tick **Publish imported content** to publish each item as it is imported. Leave it unticked to save every item as a draft that you publish later.
6. For contacts and leads, set the extra import options. See [Contacts](../contacts.md) and [Leads, Accounts and Opportunities](../leads-accounts-opportunities.md).
7. Click **Upload**. A progress bar shows the upload.

Large files are sent in pieces automatically, so you can upload files of hundreds of megabytes. If a file is too big for your site, the page tells you the largest size allowed. Your administrator can raise it.

### What happens to each row

The import runs in the background, so you can leave the page. For each row:

- When the row has the `ContentItemId` of an item that already exists, that item is updated. Otherwise a new item is created.
- The item is checked the same way as when someone saves it by hand. A row that fails is skipped and counted as an error; the other rows still import.
- The item is published or kept as a draft, depending on **Publish imported content**.

Rows a contact import leaves out on purpose, such as duplicate phone numbers or numbers on a [do-not-call list](do-not-call-lists.md), are counted as errors too, with the reason.

### Follow an import

Each import is a row on the **Bulk Import** page showing the file name, content type, who uploaded it, its status, how many records imported and how many had errors. While it runs, a progress bar shows *N of M records processed*.

| Status | Meaning |
| --- | --- |
| **Pending** | Waiting to start. |
| **Processing** | Running now. |
| **Paused** | Stopped by someone. It continues from where it stopped when resumed. |
| **Completed** | Every row was imported. |
| **Completed with errors** | Finished, but some rows were skipped. |
| **Failed** | Stopped because of a problem. You can resume it. |
| **Deleting** | Being removed in the background. |

The **Actions** menu on each row offers:

- **Download errors** downloads only the rows that were skipped, in the same file format as your upload, with an **Errors** column that gives the reason for each. Fix them and import that file again.
- **Pause import** stops an import that is running.
- **Resume import** continues a pending, paused, failed or stalled import from the last saved batch.
- **Delete** removes the entry. It also deletes the uploaded file, but never the content items that were imported.

Use the status and sort menus above the list to find an import, and tick several rows to **Remove** them at once.

## Export content

1. Open **Content > Export**. The **Bulk Export** page lists earlier exports.
2. Click **Export** to open the **Export contents** form.
3. Choose the **File type** and the **Content type**.
4. Under **Export scope**, choose **Export all published**, or **Filtered Export** to narrow it down with the filters below.
5. Fill in any extra options shown for that content type. For example, contacts can add each contact's last activity; see [Contacts](../contacts.md).
6. Click **Export Data**.

| Filter | What it does |
| --- | --- |
| **Created from** / **Created to** | Only items created in that date range. |
| **Modified from** / **Modified to** | Only items changed in that date range. |
| **Owners** | Only items owned by these people. Type user names separated by commas. |
| **Published only** | Only the published version of each item. |
| **Latest only** | Only the newest version, published or draft. |
| **All versions** | Every saved version of each item. It cannot be combined with the two above. |

A small export downloads right away. A large one (more than 500 items unless your administrator changed it) is prepared in the background. You see *The export has been queued for background processing. You can download it from Bulk Export when it is ready.* When it is done, its row on **Bulk Export** shows **Completed** and a **Download** button. If notifications are turned on for your site, you also get a notification.

Exported files use the same column names as import templates, so you can export, edit the file, and import it back to update the same items.
