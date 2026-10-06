---
sidebar_label: Do Not Call Lists
title: Do Not Call Lists and Registries
description: Upload your own do-not-call lists, connect the national registries, and make every contact import check them.
technical_manual:
  - modules/dnc-registry
---

A **do-not-call list** holds phone numbers your company must not call. You can upload your own lists, for example your internal suppression list or a monthly registry download, and connect the national registries of the United States and Canada. Contact imports then leave those numbers out, and the dialer skips them before it calls.

| | |
| --- | --- |
| **Menu** | Interaction Center > Local DNC Registry; Settings > Content Import; Settings > DNC Registries |
| **Permission** | Manage DNC registry settings |
| **Feature** | DNC Registry, plus one or more of: Local Do Not Call Registry, USA FTC Do Not Call Registry, Canada LNNTE-DNCL Registry |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of enabling the Local Do Not Call Registry and uploading a CSV list">
  <source src="/img/docs/dnc-registry.mp4" type="video/mp4" />
</video>

## Where the lists are checked

- **Contact imports.** When you [import contacts or leads](import-and-export.md), you can check the file against the lists you choose. Your administrator can make some lists mandatory for every import (see [Check every import](#check-every-import)).
- **The dialer.** When a [dialer profile](../dialer-profiles.md) has **Respect do-not-call and communication preferences** turned on, every connected list and registry is checked before each call. A number on a list is not called.
- **Calls dialed by hand.** Depending on how your site is set up, calls agents dial themselves can be checked too.

A registry that cannot answer, for example because it is unreachable, never counts as "not listed". An import skips that row and says why, and the dialer tries the record again in a later cycle.

## Upload a local list

The **Local Do Not Call Registry** keeps lists you upload yourself, organized by country.

1. Open **Interaction Center > Local DNC Registry** and click **Upload new list**.
2. Enter a **List name**, for example *June 2026 DNC Update*.
3. Pick the **Country** the numbers belong to. The menu shows each country's calling code.
4. Choose the **CSV file** and click **Upload**.

The page returns right away and the list imports in the background. Its row shows *N of M records processed* while it runs.

### Prepare the file

- A CSV file with one phone number on each row, in a single column. If your list is a spreadsheet with several columns or tabs, save just the phone number column as a CSV file first.
- A header row such as *Phone Number* is optional. It is skipped and not counted as an error.
- Numbers that do not start with `+` are read as numbers of the country you picked.
- Every number is converted to its full international form before it is stored. Numbers that cannot be converted are rejected.
- Blank rows, rows with more than one value, and repeated numbers are skipped and counted as rejected records.

### Follow and manage lists

Each list shows its file name, country, status, how many records were imported, how many were rejected, and when it was last processed.

| Status | Meaning |
| --- | --- |
| **Pending** | Waiting to start. |
| **Processing** | Importing now. |
| **Paused** | Stopped by someone. **Process now** continues it. |
| **Completed** | Every row was imported. |
| **Completed with errors** | Finished, but some rows were rejected. |
| **Failed** | Stopped because of a problem. It is retried automatically a few times. |
| **Deleting** | Being removed in the background. |

The **Actions** menu on each list offers:

- **Download errors** downloads the rejected rows, with the reason for each, as a CSV file.
- **Process now** starts or continues a list that is pending, paused, failed or stalled, without waiting for the background task.
- **Pause import** stops a list that is importing.
- **Delete** removes the list and all its phone numbers in the background.

:::note[Only completed lists are checked]
A list is used for checking only once its status is **Completed** or **Completed with errors**. A list that is still importing, paused or failed is not used yet.
:::

**Replace a list each month:** upload the new file first, wait until it is completed, then delete the old list. That way your numbers are never unprotected in between.

A background task checks every 10 minutes for lists that are waiting or stuck, for example after the site restarted, and continues them where they stopped.

## Check every import

Use this when company policy says every contact import must be checked, so nobody can forget.

<video controls preload="metadata" width="100%" aria-label="Screencast of uploading a local DNC list and enforcing it globally">
  <source src="/img/docs/omni-dnc-local.mp4" type="video/mp4" />
</video>

1. Open **Settings > Content Import**.
2. Tick **Enforce do-not-call registry checks globally**. Every contact import must then check phone numbers, and the person importing cannot turn it off.
3. Under **Enforced registries**, tick the lists and registries that are always checked. People can add more when they import, but cannot remove these.
4. Click **Save**.

On the import screen, an enforced list shows an **Enforced** badge, and the do-not-call option shows *This setting is globally enforced by the site administrator.*

## What people see when they import contacts

On the import screen for a contact or lead type, the **Do-not-call registry** section offers:

| Field | What it does |
| --- | --- |
| **Ignore numbers on national do-not-call registries** | Checks the file's phone numbers against the selected lists. |
| **When a number is on a registry** | **Skip the row** leaves it out of the import and lists it in the error file. **Import it marked Do not call** keeps the record with Do not call set, so the number is recognised if it is bought or imported again, and it is never called. |
| **Select registries to check against** | The lists and registries to use for this import. |

See [Contacts](../contacts.md) for the full contact import.

## Connect a national registry

Each national registry needs an account with the registry's operator. Get the account details first, then:

1. Open **Settings > DNC Registries** and pick the registry.
2. Fill in the fields below and click **Save**.

### USA FTC Registry

| Field | What it does |
| --- | --- |
| **Organization ID** | The organization ID registered with the FTC for API access. |
| **API key** | The API key the FTC gave you. |
| **Base URL** | The address of the FTC registry service. Keep the suggested address unless the FTC tells you otherwise. |

To get access, follow the FTC's guidance at [telemarketing.donotcall.gov](https://telemarketing.donotcall.gov/).

### Canada LNNTE-DNCL Registry

| Field | What it does |
| --- | --- |
| **Account number** | Your organization's account number registered with the CRTC for API access. |
| **API key** | The API key the CRTC gave you. |
| **Base URL** | The address of the LNNTE-DNCL service. Keep the suggested address unless the CRTC tells you otherwise. |

To get access, follow the CRTC's onboarding guidance at [www.lnnte-dncl.gc.ca](https://www.lnnte-dncl.gc.ca/en/Organization/DNCL_API).

:::tip[API keys stay hidden]
After you save an API key, the box stays empty and a green note says a key is stored. Leave the box empty to keep the key, or type a new one to replace it.
:::

A registry with no account details does not take part in checking. Fill in only the registries whose answers you rely on.
