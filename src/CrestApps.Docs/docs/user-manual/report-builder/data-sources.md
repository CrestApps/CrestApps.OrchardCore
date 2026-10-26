---
sidebar_label: Data Sources
title: What You Can Report On
description: The data sources the Report Builder offers - content items, saved queries, views, users and the records of other features - and who may use each.
technical_manual:
  - modules/report-builder/data-sources
  - modules/report-builder/custom-data-sources
---

A **data source** is where a report's data comes from. Each one offers one or more **data sets**, which are like tables. The data sources you see depend on the features your site uses and on your permissions.

The data sources are shared with Orchard Core: **Content items**, **Users** and **Queries** come from Orchard Core itself, and the data sources of the CrestApps features are also offered to Orchard Core's data pipelines, so a data pipeline can read the same data as your reports.

Watch the short video, then read about each data source below.

<YouTube video="report-builder" title="Narrated tour of the data sources and data sets the Report Builder offers" start={504} end={562} />

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > *a report* > Add data set |
| **Permission** | Build reports and manage own custom reports and views, and the permission each data source asks for below |
| **Feature** | Report Builder, and the feature that stores each kind of data |

<AskYourAdmin />

## The data sources

| Data source | What it offers | Shown when | You need |
| --- | --- | --- | --- |
| **Content items** | One data set per content type of the site, such as *Customer* or *Order*, grouped by the kind of content type (its stereotype). | The **Contents** feature is on. | Permission to view that content type. |
| **Queries** | One data set per saved query, such as a SQL or search query, with the rows it returns. | The **Queries** feature is on. | Permission to run that query: **Execute Api - All queries**, or **Execute Api -** *the query's name*. |
| **Report views** | The [reusable views](views.md) you and your team saved. | Always. | **Build reports and manage own custom reports and views**. |
| **Users** | **Users** (one row per user account) and **User roles** (one row per user and role). | Always. | **View Users**. |
| **Omnichannel** | **Activities**, **Dispositions**, **Campaigns**, **Campaign groups** and **Activity batches**. | **Omnichannel Activities** is on. | **View Omnichannel reports**. |
| **Contact Center** | **Interactions**, **Interaction events**, **Call sessions**, **Call quality**, **Call recordings**, **Callback requests**, **Dialer profiles**, **Queues**, **Queue groups**, **Queue items**, **Agent profiles**, **Agent sessions** and **Shared voicemails**, each when the Contact Center feature that stores it is on. | A Contact Center feature is on. | **View Contact Center reports**. Call recordings also need **Listen to anyone's call recordings**; shared voicemails need **Access shared queue voicemail for entitled queues**, and show only the queues you may answer. |
| **AI chat** | **Chat sessions** and **Chat session metrics** (messages, handle time, tokens, ratings, resolution and conversion). | **AI Chat** is on; the metrics need **AI Chat Session Analytics**. | **View AI Chat Analytics**. |
| **Messaging** | **Conversations** and **Messages** of the messaging workspace. | **Omnichannel Messaging Workspace** is on. | **View all messaging conversations**. |
| **Orders** | **Orders**, **Order lines**, **Order refunds**, **Order disputes**, **Order discounts** and **Order taxes**; **Returns** and **Gift cards** when those features are on. | **Orders** is on. | **Manage orders**. Gift cards also need **Manage gift cards**. |

If a data source or data set is missing, your site may not use the feature that stores that data, or you may not have the permission it asks for. Ask your administrator.

## Content items

Each content type is a data set. It starts with the information every item has, in the **Content item** group: **Content item id**, **Version id**, **Content type**, **Display text**, **Owner**, **Author**, **Published**, **Latest**, **Created**, **Modified** and **Published on**.

Then come the values of its parts and fields, grouped by part:

- a field with one value is one column, such as an order's *Total*;
- a field with several values gives one column per value, such as the address and the text of a link;
- a field that holds several items, such as a content picker or a user picker, gives their IDs, separated by commas;
- the values of the parts, such as the title of a **Title** part, and the values of the parts that features add, such as the contact preferences of a contact.

Values that look secret, such as passwords and access tokens, are never offered.

Which items you see depends on your permissions for the content type. If you may edit it, you see the latest version of each item, drafts included; use the **Published** column to tell them apart. Otherwise you see the published items only. When a saved report runs, this follows the permissions of the report's owner.

To protect sensitive content from report builders, your administrator can make its content type **Securable** and allow only some roles to view it.

## Queries

Each saved query is a data set, grouped by the kind of query, such as SQL. A query does not list its columns, so its fields are found in the first rows it returns: a query that returns no rows yet offers no fields. A query that returns content items offers the information every item has, such as **Display text** and **Owner**, but not the values of their parts and fields; to report on those, use the **Content items** data source. The query runs without parameters, once per report run, so its parameters need default values.

## Users

**Users** lists every user account with its **User name**, **Email**, **Roles**, whether it is **Enabled**, and other account settings, such as whether two-factor authentication is on and the number of failed sign-ins. Passwords, security codes, tokens and other secrets are never shown. **User roles** has one row per user and role, with the **Role**, so you can count the users of each role.

The owner of a content item, and the records of other features that point to a user, such as an activity's assigned user, are joined to **Users** for you.

## Records of other features

The Omnichannel, Contact Center, AI chat and Messaging data sources offer the records those features keep. Related data sets are joined for you: an activity to its disposition and campaign, an interaction to its activity, an agent to their user account, and so on. Secrets, storage locations, IP addresses and message text are never shown.

These data sets read the newest records first and narrow the read to the period your report's date filter asks for, so keep a date filter on reports over a lot of data. See [Filters](filters.md#the-date-filter-a-new-report-starts-with).

## Reusable views

The **Report views** data source lists the views saved under **Reports > Report Views**. A view's columns are its fields. See [Reusable views](views.md).

## More data sources

Your administrator or developer can add data sources for other systems, such as another database or a business application, and other Orchard Core modules can add their own. They appear in **Add data set** like the ones above. How to build one is described in the Technical Manual: [Add your own data source](../../modules/report-builder/custom-data-sources.md).

Next: [Publish and versions](publish-and-versions.md).
