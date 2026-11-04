---
sidebar_label: Accounts
sidebar_position: 10
title: Accounts
description: Keep the businesses and households you work with as accounts, put their contacts in them, and see everything an account holds on one page.
technical_manual:
  - modules/accounts
---

An **account** is a business or household you work with. Its contacts belong to it, and so can its deals, orders,
equipment and work orders, depending on the features your site uses. Each business is kept once, as one account, and
every part of the site uses that same account.

## Finding your accounts

Open **Accounts** in the admin menu to list them. On a site with the CRM, the list is under
**Interaction Center > Accounts** instead.

Search the list by name, and open an account with **View** to see its page.

## Creating an account

1. Open the accounts list and select **New Account**.
2. Type the account's name.
3. Select **Publish**.

## Putting a contact in an account

Open the contact and pick the account in its **Account** field, then save. To move the contact to another account,
pick that one instead. To take it out of every account, clear the field.

You can also create the contact from the account's page. It is then already in the account.

## The account's page

The account's page brings together everything that belongs to the account. Its sections are listed down the left;
click one to see it on the right. On a phone, pick the section from the list at the top. Which sections you see
depends on the features your site uses and what you're allowed to see:

| Section | What it shows |
| --- | --- |
| **Overview** | The people who sign in for the account and its locations. |
| **Contacts** | Its contacts, with their email and phone. **New** adds a contact to the account. |
| **Opportunities** | Its deals, open ones first, with their stage, amount and close date. |
| **Leads** | The leads that became its contacts. |
| **Activities** | How many calls, messages and tasks with its contacts are scheduled and done. **List Activities** shows them all. |
| **Orders** | The orders placed for it. |
| **Business buying** | When it buys as a business: its payment terms, what it owes and its buyers. |
| **Invoices** | The invoices for what it bought on its terms, and what is still owed on each. |
| **All records** | Everything the account holds. |

**Edit Account**, **New** (to add a record to the account) and **List Activities** are at the top of every section.
The address in your browser names the section you're on, so you can bookmark it or send it to a colleague.

If **All records** says *Please specify at least one contained content type*, no type has been set up to sit in
accounts yet. Turning on a feature that uses accounts, such as the CRM, sets this up for you.

The account's editor has tabs too: the account's name first, then **Members**, **Locations** and **Business**.

## The people who sign in for an account

With **Account Members** on, an account's editor has a **Members** card. Enter the **User name or email** of each
person who signs in for the account (they need a user account first) and choose a **Role**: a **Member** acts for the
account in what they do themselves, and an **Admin** also manages it for the others, such as approving their orders. A
person is a member of one account only. When a member's email is the email of one of the account's contacts, the
member is linked to that contact. Save to get more empty rows; clear a row to remove a member.

You need **Manage the users who sign in for accounts** to see the card.

## An account's locations

With **Account Locations** on, an account's editor has a **Locations** card for the places the account has, such as
its head office or its warehouses. Give each a name and its address, in the same address form as the rest of the site:
choose the country first and the form asks for what that country's addresses need. Save to add another; clear a
location's fields to remove it.

## Moving accounts to another site

To copy accounts to another site, add the **Accounts** step to a deployment plan under **Configuration > Import/Export >
Deployment Plans**. Leave **Also export the records the accounts hold** ticked to bring their contacts and
opportunities too. Every account comes with its members, locations and business buying settings. Import the file on the
other site as you would any deployment package. Importing the same file again updates the accounts instead of copying
them.

## Deleting an account

Use **Actions > Delete** on the account's row and confirm. The contacts and other records it held are kept, now
outside any account.

## Importing contacts into accounts

When you import contacts from a spreadsheet, an **Account** column puts each contact in the account with that name.
A name that matches no account, or matches more than one, leaves the contact outside any account. When you export,
the column shows each contact's account.
