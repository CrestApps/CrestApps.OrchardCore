---
sidebar_label: Share and Run Reports
title: Share, Run and Export Reports
description: Put a report in the admin menu, share it with people, roles or a link, and run and export it.
technical_manual:
  - modules/report-builder/permissions
---

A report you build is yours until you share it. You can put it in the admin menu, share it with people and roles, or create a link that opens it for anyone who has it. The people you share with run the report, change the filters you let them change, and export it.

Watch the short video, then follow the steps below.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-share-and-run.jpg" aria-label="Video: sharing a report with people and roles, creating a share link, and running and exporting a report">
  <source src="/img/docs/report-builder-share-and-run.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-share-and-run.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > *a report* > Sharing |
| **Permission** | Build reports and manage own custom reports and views (to share your reports); Share custom reports publicly and through share links (to share with everyone or create links) |
| **Feature** | Report Builder |

<AskYourAdmin />

## Put a report in the menu

On the **Settings** tab:

| Setting | What it does |
| --- | --- |
| **Report title** | Shown at the top of the report and in the report list. |
| **Description** | Shown above the report. |
| **Category** | Groups the report in the admin menu and the report list. |
| **Show in the admin menu** | Adds the report under **Reports** in the group you enter as **Category** (or *Custom Reports*), for everyone who can open it. |
| **Let people the report is shared with export it** | Turn it off to let them view the report but not download it. |

Publish the report to apply the settings.

## Share a report

On the **Sharing** tab:

| Setting | What it does |
| --- | --- |
| **People** | Search by user name or email and pick the people who may run the report. |
| **Roles** | Everyone in a checked role may run the report. **Authenticated** means everyone who is signed in. **Anonymous** means everyone, including visitors who are not signed in, and needs the *Share custom reports publicly and through share links* permission. |

Publish the report to apply the sharing. The people you share with find the report under **Reports > Shared Reports** (or **Report Builder** if they build reports), and in the admin menu when it is pinned there.

:::warning[A shared report shows data with your access]
A shared report reads data with **your** access: people see what the report shows even when they could not open that data themselves. Share only what they should see. If your account is disabled or deleted, your reports stop running; someone who designs reports can clone them and share the copies again.
:::

People who cannot open the admin can open a shared report at its own page outside the admin. A report shared with the **Anonymous** role opens there for anyone.

## Create a share link

A share link opens one report for anyone who has the link, without an account. You need the *Share custom reports publicly and through share links* permission, and the report must be published.

1. On the **Sharing** tab, under **Share links**, enter a **Note** that says what the link is for.
2. Optionally set **Expires**, **Allow export**, and **Require sign-in** (the link then works only for people who are signed in).
3. Click **Create link**, then **Copy**. For security, the full link is shown only once.

To stop a link working, click **Revoke**. The list shows each link's note, the first characters of its address, when it expires, what it allows, and whether it is **Active**, **Expired** or **Revoked**.

Pages opened through a share link are hidden from search engines, and every opening and export through a link is recorded in the site's logs.

## Run and export a report

**Reports > Report Builder** (or **Shared Reports**) lists every report you can open. Click **Run**, or the report's title, to open it. A report pinned to the menu also opens from **Reports** > *its category*.

1. Change the filters shown above the report, if any, and click **Show**.
2. To download the report, click **Export CSV**. When the Reports (OpenXml) feature is on, an **Export** menu offers **Export CSV** and **Export Excel (.xlsx)**.

The export uses the filters on screen. People the report is shared with can export it when **Let people the report is shared with export it** is on; people who may change the report can always export it. A share link exports only when its **Allow export** is checked.

When you may change the report, **Edit design** opens it in the builder.

## Clone and delete

From the list:

- **Clone**, under **Actions**, makes your own copy of a report you can run, named after it with *(copy)*, and opens it in the builder. The copy is not shared with anybody and not in the admin menu. You need the *Build reports and manage own custom reports and views* permission.
- **Delete** removes the report, its versions and all its share links. You need to own the report and be allowed to build reports, or to have *Manage all custom reports and views*.

Next: [Troubleshooting](troubleshooting.md).
