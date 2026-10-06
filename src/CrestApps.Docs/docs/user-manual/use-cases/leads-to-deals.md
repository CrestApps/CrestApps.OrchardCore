---
sidebar_label: Leads to Deals
title: Turn Leads into Deals
description: Keep prospects apart from your customers as leads, call and text them, convert the ones that qualify into contacts, and track the deals as opportunities under accounts.
technical_manual:
  - omnichannel/crm
---

## Who it's for and what you get

For **managers** and **sales teams** who work lists of prospects: purchased lists, trade-show scans or web forms.

When it is done:

- prospects live as **leads**, apart from your contacts, so your contact list holds only real customers;
- agents and the dialer call and text leads exactly like contacts, and the outcome moves each lead through your **lead statuses**;
- a lead that qualifies is **converted**: it becomes a contact (or merges into one who has its number), joins an **account**, and can open an **opportunity** for the deal;
- reports show your lead funnel, which sources and lists convert best, and your pipeline.

## Before you start

| You need | Who sets it up |
| --- | --- |
| The feature **Omnichannel CRM** (it turns on Omnichannel Management), **Content Transfer** to import lists, and **Reports** | Your administrator, in **Tools > Features**, or with the **Omnichannel CRM starter** recipe |
| Content permissions for the lead, contact, account and opportunity types | Your administrator |
| **Convert leads** for the people who convert (the Agent role has it); **Manage lead statuses** and **Manage opportunity stages** for the manager | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |
| To call or text leads: the features for an [outbound calling campaign](outbound-calling-campaign.md) or for [texting](text-your-contacts.md) | Your administrator |

<AskYourAdmin />

## Steps

1. **Turn it on.** The quickest way is the **Omnichannel CRM starter** recipe under **Tools > Recipes**. It adds a **Lead** type that converts into **Contact**, a **Sales Opportunity** type, and a starting set of lead sources. See [Leads, accounts and opportunities](../leads-accounts-opportunities.md).
2. **Review your lists.** Under **Interaction Center > Management**, check the **Lead Statuses** (open, closed and converted), the **Lead Sources** (where leads come from) and the **Opportunity Stages** (with their probability, and which ones are won or lost). See [Leads, accounts and opportunities](../leads-accounts-opportunities.md).
3. **Import your leads.** Import the list under **Content > Import** with the lead type. Give the rows a list name, a lead source, a status and an owner, and skip numbers that already belong to a contact or an open lead. See [Leads, accounts and opportunities](../leads-accounts-opportunities.md) and [Import and export](../administration/import-and-export.md).
4. **Work the leads.** Open **Interaction Center > Leads** to see your open leads. Search them by status, source, list, rating or owner. See [Leads, accounts and opportunities](../leads-accounts-opportunities.md).
5. **Call and text them.** Create an activity load with the lead type as the **Record type**, and use the **Lead filters** to pick, for example, one list's hot leads. Use a dialer load for calls, a manual load for agents' lists, or an automatic load for AI texts or calls. See [Load activities](../load-inventory.md), [Run an outbound calling campaign](outbound-calling-campaign.md) and [Automated AI SMS and voice](../automated-ai.md).
6. **Move leads along.** On the subject flow, set **Set lead status** on each action, so a *No answer* moves the lead to *Working - Contacted* on its own. See [Subject flows](../subject-flows.md).
7. **Convert the ones that qualify.** Open the lead and click **Convert Lead**: pick or create the contact, the account, and tick **Create an opportunity** to record the deal. You can also convert from a disposition with the **Convert Lead** flow action, or let the AI convert qualified leads on an automatic load. See [Leads, accounts and opportunities](../leads-accounts-opportunities.md).
8. **Work the deals.** Track opportunities under **Interaction Center > Opportunities**, moving each one through its stages, and see each company's contacts and deals under **Interaction Center > Accounts**. See [Leads, accounts and opportunities](../leads-accounts-opportunities.md).
9. **Measure.** Use **Lead funnel**, **Lead conversion by source and list** and **Opportunity pipeline** under **Reports**. See [Reports](../reports.md).

## Check that it works

1. Import a small test list of two or three leads, with your own number on one of them.
2. Open **Interaction Center > Leads** and find them by their list name.
3. Convert one lead with **Create an opportunity** ticked. The lead leaves the Leads list, shows a *Converted* banner that links to the new contact, and the opportunity appears under **Interaction Center > Opportunities**.
4. Run the **Lead funnel** report for today: it counts the converted lead.

## Tips

- A converted lead is read-only and never loaded again. Its history and open work move to the contact.
- You cannot convert a lead while a call or message with it is in progress.
- When a number belongs to both a contact and a lead, inbound calls and texts match the contact.
- Keep **Skip leads that are already contacts** ticked on loads, so customers are not called again as strangers.
- To act when a lead converts, for example to send a welcome email, use a workflow that starts on **Lead Converted**. See [Leads, accounts and opportunities](../leads-accounts-opportunities.md).
