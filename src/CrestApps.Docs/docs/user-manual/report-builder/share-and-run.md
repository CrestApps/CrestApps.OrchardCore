---
sidebar_label: Share and Run Reports
title: Share, Run and Export Reports
description: Put a report in the admin menu, share it with people and roles, send people a link to it, and run and export it.
technical_manual:
  - modules/report-builder/permissions
---

A report you build is yours until you share it. You can put it in the admin menu, share it with people and roles, or send people a link that opens it for them. The people you share with run the report, change the filters you let them change, and export it.

Watch the short video, then follow the steps below.

<YouTube video="report-builder" title="Narrated video of sharing a report with people and roles, creating a share link, and running and exporting a report" start={561} end={595} />

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > *a report* > Sharing |
| **Permission** | Build reports and manage own custom reports and views (to share your reports); Share custom reports publicly and through share links (to share with everyone or create links) |
| **Feature** | Report Builder. Emailing share links needs Email. |

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

People who cannot open the admin can open a shared report at its own page outside the admin. A report shared with the **Anonymous** role opens there for anyone, including visitors who are not signed in: this is how you show a report to everyone.

## Create a share link

A share link sends one report to people who have an account on the site. Only the people you send it to can open it, and only once they sign in. A link always expires, and you can limit how many times it is used. You need the *Share custom reports publicly and through share links* permission and the right to change the report, and the report must be published.

A link is for named people only. To show a report to everyone, share it with the **Anonymous** role instead (see above).

1. On the **Sharing** tab, under **Share links**, fill in the settings below.
2. Click **Create link**.
3. If the link was emailed, the page says who received it. Otherwise, click **Copy** and send the link to its recipients yourself. For security, the full link is shown only on this page, and not again once you leave it.

| Setting | What it does |
| --- | --- |
| **Recipients** | Search by user name or email and pick the people the link is for. They must be users of the site. |
| **Note** | Optional. What the link is for. It is sent with the email and shown in the list of links. |
| **Expires after (days)** | How many days the link works, at least 1. It starts at 7. |
| **Most uses** | How many times the link can be used, or `0` for no limit. Opening the report, changing its filters and exporting it are each a use. |
| **Allow export** | Lets the recipients download the report. |
| **Email the link to the recipients** | Sends the link and your note to each recipient by email. It is on at first, and needs the Email feature. Turn it off to send the link yourself. |

The list under the settings shows every link of the report:

| Column | What it shows |
| --- | --- |
| **Shared with** | The recipients, and the note. |
| **Expires** | When the link stops working. |
| **Uses** | How many times the link was used, such as *3 of 10* when it has a limit. |
| **Allows** | **Export** when the recipients may download the report. |
| **Status** | **Active**, **Expired**, **Used up** (used as many times as allowed) or **Revoked**. |

To stop a link working before it expires, click **Revoke**.

When a recipient opens the link, the site asks them to sign in if they are not signed in yet. Someone else who signs in with the link is refused, and a link that expired, was used up or was revoked no longer opens. Pages opened through a share link are hidden from search engines, and every opening and export through a link is recorded in the site's logs.

The links of every feature of the site, such as report links and the files of data pipelines, are listed together under **Tools > Shared Links** for people with the *Manage shared links* permission. They can revoke and delete links there too. A link is deleted a day after it stops working, and a report's links are deleted with the report.

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
