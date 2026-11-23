---
sidebar_label: Payment Terms
title: Payment Terms
description: How to keep the store's payment terms, such as Net 30 or Due on receipt, with an optional discount for paying early.
technical_manual:
  - modules/transactions
---

A **payment term** says when a bill is due after the day it is issued, such as *Net 30* (30 days later) or *Due on receipt*, and can offer a discount for paying early, such as 2% when paid within 10 days. You keep the list of terms once, and the features that set due dates pick from it.

| | |
| --- | --- |
| **Menu** | Commerce > Payment Terms |
| **Permission** | Manage payment terms |
| **Feature** | Payment Terms |

<AskYourAdmin />

## The list of terms

Open **Commerce > Payment Terms**. Each row shows the name, when the bill is due, the early-payment discount if there is one, and **Turned off** for a term that can no longer be chosen. Type part of a name in **Search** and press Enter to find a term. The list follows the layout chosen for admin lists, as a list or as a grid.

## Add a term

1. Click **Add payment term**.
2. In **The term**, enter the **Name** printed on documents, such as *Net 30*. **Notes** are for staff only.
3. In **When it is due**, pick one of these in **Due**:
   - **Due on receipt**: due on the day the document is issued.
   - **A number of days after the issue date**: enter the **Days**, such as 30.
   - **End of the month, plus a number of days**: due on the last day of the month the document is issued in, plus the **Days** (0 for the last day itself).
   - **A day of the next month**: enter the **Day of the next month**, from 1 to 31. When the month has fewer days, the bill is due on its last day.
4. To offer a discount for paying early, enter the **Discount (%)** and for how many days it is **Offered for**, counted from the issue date with the last day included. Leave the discount empty for none.
5. Click **Save**.

Dates are counted in your site's time zone, so a term never moves a bill to a different day.

## Change or stop using a term

Click the term's name, or **Edit**, change it and click **Save**. Documents already issued keep the due date they were given.

To stop using a term without deleting it, clear **Can be chosen for new documents**. To delete it, click **Delete** and confirm. Documents already issued keep the term's name and due date.

## Move terms to another site

Payment terms can be exported in a deployment plan (step **Payment terms**) and imported with a recipe.

## Related pages

- [Transactions](transactions.md)
- [Invoices and Receipts](invoices-and-receipts.md)
