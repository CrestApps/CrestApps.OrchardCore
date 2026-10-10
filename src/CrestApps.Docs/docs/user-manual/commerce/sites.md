---
sidebar_label: Selling Sites
title: Selling Sites
description: Sell whole websites as a subscription, let customers watch their new site being created on My Sites, and fix the ones that could not be created from Site provisioning.
technical_manual:
  - modules/subscriptions
  - modules/checkout
---

With **Selling Sites**, a subscription plan sells a whole website. The buyer names their new site and its administrator during checkout, pays, and gets a running site of their own a minute or two later. The site keeps running for as long as their subscription is current.

Creating a site happens after the purchase, in the background, so a slow or failed setup never loses a sale: the customer always has a record of what they bought, and a site that could not be created is retried and, if needed, handed to an administrator.

| | |
| --- | --- |
| **Menu** | Subscriptions > Site provisioning (administrators), Subscriptions > My Sites (customers) |
| **Permission** | Manage subscriptions (Site provisioning), Manage own subscriptions (My Sites) |
| **Feature** | Subscriptions - Sites, available on the main (default) site only |

<AskYourAdmin />

## Set up a plan that sells a site

A site plan is an ordinary [subscription plan](subscriptions.md) with the **Tenant Onboarding** part added.

1. Open **Content > Content Definition > Content Types** and edit your plan type, or create one as described in [Subscriptions](subscriptions.md#create-a-plan-type).
2. Click **Add Parts**, tick **Tenant Onboarding**, and save.
3. Open a plan of that type. In **Tenant Onboarding**, pick the **Recipe** the new site is set up from, for example a blog or an agency site. It is required.
4. Set the price and billing terms as for any plan, then click **Publish**.

When a customer signs up for that plan, the checkout adds a **Your new site** step:

| Field | What it does |
| --- | --- |
| **Site name** | Letters, digits, and underscores, starting with a letter. It identifies the site and cannot be changed later. It must not already be taken. |
| **Site title** | What visitors see. The customer can change it at any time. |
| **URL prefix** | The part of the address after your domain, for example *contoso* for a site at your-domain/contoso. It must not belong to another site. |
| **Custom domains** | Optional domain names the site answers on, separated by commas, such as *contoso.example.com*. They must not belong to another site. |
| **Site template** | Shown only when more than one template is available. It starts on the plan's recipe. |
| **User name**, **Email**, **Password**, **Confirm password** | Under **Site administrator**: the account the customer signs in to their new site with. When the customer comes back to this step, **Password** can be left blank to keep the one already entered. |

Everything the customer can fix is checked on this step, before they pay: a taken name, a domain or prefix that belongs to another site, or passwords that do not match. See [Checkout](checkout.md) for the rest of the purchase.

### Choose the database new sites use

New sites are created on the database chosen on the **Tenant Onboarding** tab of **Settings > Subscriptions** (permission **Manage subscriptions settings**). The buyer never chooses it.

| Field | What it does |
| --- | --- |
| **Database for new sites** | Every site sold is created on this database. **Sqlite (a file per site)**, the default, needs no database server. |
| **Connection string** | Required for every provider except Sqlite. Each site gets its own table prefix, derived from its name. |
| **Schema** | Optional. Leave blank to use the provider's default. |

:::note[Saving the Tenant Onboarding tab]
The tab also has domain options. It saves only when **Use custom domains** is ticked or **Local domains** is set to something other than **None**.
:::

## My Sites (for customers)

**My Sites** opens the **My sites** page, where a customer sees every site they bought and opens the ones that are ready.

:::note[Customers need admin access]
**My Sites** is in the admin area, so a customer also needs the **Access admin panel** permission to open it, in a role they belong to. Without it they see *You do not have access to this resource*.
:::

| Badge | What it means |
| --- | --- |
| **Being created** | The site is paid for and is being set up. |
| **Retrying** | The last attempt failed and another one is scheduled. Nothing for the customer to do. |
| **Ready** | The site exists. Click **Open** to go to it and sign in with the administrator account entered at checkout. |
| **Needs help** | The site could not be set up. The page says *We could not finish setting this site up. Please contact support.* |

Each row also shows the **Site name** and when it was **Bought**. While a site is still being created, the page notes that a new site usually takes a minute or two to appear; reload the page to see its progress.

## Site provisioning (for administrators)

**Site provisioning** lists every site that has been bought, with the job that creates it. Use it to spot sites that could not be created and to try them again.

| Status | What it means |
| --- | --- |
| **Pending** | Paid for and waiting to be created. The site checks for waiting jobs every minute. |
| **Running** | Being created right now. |
| **Succeeded** | The site exists and the customer can sign in. |
| **Failed** | The last attempt failed and another is scheduled, with a longer wait after each failure. |
| **Abandoned** | Creating the site failed 5 times, so it is no longer retried. The customer has paid and has no site, so a person needs to act. |

Each row shows the site name, its status, the **Owner**, when it was **Bought**, the number of **Attempts**, and the last error in red when there is one.

- Use the status filter in the list header to show one status, for example **Abandoned**, or **Any status** for all.
- Use the pager at the bottom, and **Items per page** to show more or fewer rows.

### Retry a site

Use **Retry now** when a job is **Failed** and you have fixed the cause, so you do not have to wait for the next scheduled attempt.

1. Fix the cause shown in the error, for example the database for new sites.
2. Click **Retry now** on the job. It is offered on every job that has not succeeded.
3. In the confirmation, click **Retry now** to try creating the site immediately, or **Cancel**.

The page then says *The site was created.* or *The site was not created. The job is queued for another attempt.* A retry starts a fresh count of attempts.

:::warning[Abandoned jobs need a person]
For safety, the administrator password the customer entered is discarded once a job is **Abandoned**. Retrying an abandoned job therefore cannot finish it: the page says *This job no longer holds an administrator password, so the site cannot be created automatically.* Create the site by hand, give the customer its administrator account, and refund or adjust the purchase if needed.
:::

## When the subscription ends

A sold site runs for as long as its subscription is current, including the period the customer already paid for after cancelling and the grace period after a failed payment. When the subscription is no longer current, the site is **disabled**: it stops serving visitors, but nothing is deleted. If the subscription becomes current again, for example when the customer pays a late renewal, the site is enabled again exactly as it was.

## Tips and troubleshooting

- **Site provisioning or My Sites is missing.** **Subscriptions - Sites** can only be enabled on the main site. Ask your administrator to enable it there.
- **A job is Abandoned.** Read the last error and fix the cause so the next sale does not fail the same way. Then create this customer's site by hand from the site management screens, or refund the purchase from **Commerce > Payments** (see [Payments and refunds](payments-and-refunds.md)).
- **The customer says their site disappeared.** Check their subscription under **Subscriptions > Agreements**. A site whose subscription expired or was cancelled is disabled, not deleted.
- **The Recipe list on the plan is empty.** Only setup recipes are offered. Ask your administrator which site templates are installed.
