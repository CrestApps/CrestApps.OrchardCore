---
sidebar_label: Tax returns
title: Tax registrations, tax returns and e-filing
description: "Record where your business is registered for sales and use tax and how often it files, see each period's return with its due date, prepare it from your books, mark it ready and filed, record its payments, amend it, and file it electronically."
technical_manual:
  - modules/taxation
---

Tax returns list every period your business files a sales and use tax return for, worked out from your tax registrations. The site prepares each return from the figures your books give (taxable sales, tax collected, purchases that owe use tax) and works out the amount due. You file the return with the agency and pay it; then you record what you did here.

| | |
| --- | --- |
| **Menu** | **Commerce** → **Taxation** → **Tax Registrations**; **Commerce** → **Taxation** → **Tax Returns** |
| **Permission** | Manage taxation (tax registrations); Manage tax returns (see and prepare returns); File tax returns (mark filed, amend, record payments, file electronically) |
| **Feature** | Sales Tax (tax registrations); Tax Returns (filing frequency and returns); Tax E-Filing (electronic filing) |

<AskYourAdmin />

## Tax registrations

A tax registration says where your business collects a tax and under which number.

1. Open **Commerce** → **Taxation** → **Tax Registrations** and choose **Add tax registration**.
2. Give it a **Name**, such as *Michigan sales, use and withholding tax*, and choose the **Jurisdiction**. Choose a **Tax type**, or **Every tax type** for a return that has both sales and use tax.
3. Enter the **Registration number** the agency gave you, and keep **Active** ticked.
4. Under **When it applies**, enter the days it takes effect and ends, if you know them, and a **Sales threshold** when the business must collect only above a level of sales.
5. With Tax Returns on, the **Returns** card sets how the business files:
   - **Filing frequency**: monthly, quarterly or annually. **Files no return here** leaves the registration out of the tax returns.
   - **Due day** and **Months after the period**: day 20 and 1 month means the 20th of the month after the period. A day past the end of a month is its last day. Tick **Move a due date on a weekend to the Monday after** when the agency does.
   - **Return form**: how the return's boxes are worked out. **Generic return (totals by kind)** adds up the figures by kind. A jurisdiction's own return, with its discount for filing on time, its penalty and interest, is a tax form you build under **Commerce** → **Taxation** → **Tax Forms**, prepared from **Sales tax returns** (see [Tax forms](tax-forms.md#sales-tax-returns)); a pack can add one too.
   - **Annual return form**, **Annual return due day** and **Annual return month**: a yearly return filed besides the monthly or quarterly ones, when the jurisdiction asks for one. Day 28 and month 2 means February 28 after the year.
6. Choose **Save**.

A registration with tax returns can't be deleted; clear **Active** instead.

## The periods and their returns

Open **Commerce** → **Taxation** → **Tax Returns**. Each row is a period of a registration, by due date, with its form and status: **Not prepared**, **Draft**, **Ready to file**, **Filed** or **Amended**. A row with a return shows its amount due and, once filed, what is left to pay. Choose another year in the list's toolbar to see the periods that end in it.

## Prepare a return

1. Choose **Prepare** on the period's row. The return opens with the figures of the period, the boxes of its form and the amount due.
2. Check the **Figures of the period** and the **Boxes of the return**. A note at the top says when a figure is missing.
3. When your books change, choose **Prepare again**. A draft past its due date is worked out as if filed today, so you see the penalty a late return owes.
4. When the return is right, choose **Mark ready to file**. Its figures are kept until you file it or choose **Reopen**.

You can delete a draft that has no payment.

## Mark a return as filed

After you file the return on the agency's site or on paper:

1. Open the return. Under **Mark as filed**, enter the day you filed it and the **Confirmation number** the agency gave. The day can't be before the period ends.
2. Choose **Mark as filed**. The return never changes again. Its boxes are worked out once more as filed that day, so a late return keeps its penalty and one on time keeps its discount.

## Record a payment

Recording a payment moves no money: it records what you paid.

1. Open the return. Under **Payments**, enter the **Amount**, the day you paid (**Paid on**), the agency's **Reference** and the **Method**, such as ACH debit or check. Some features add their own fields here, such as the account you paid from.
2. Choose **Record payment**. The return shows what was paid and what is left.

If another part of the site that acts on tax returns (such as your books) can't take the payment, you see **The change was not saved** and nothing is recorded. Ask your administrator to look at the site's log.

Choose **Remove** to take away a payment recorded by mistake.

## Amend a return

1. Open the filed return and choose **Amend**. An amended return is prepared again from the figures of the period. It counts what you paid with the first return, so it shows only what is left to pay or to get back.
2. Check it, file it with the agency, and mark it as filed. The first return then shows **Amended**.

## File electronically

With Tax E-Filing on, a return that is ready to file has an **Electronic filing** card. It offers to file only once the period is over.

1. Under **File as**, choose a file format and a channel. **Tax return (JSON) · Download and file by hand** builds a file of the return; a pack adds a state's own format and channel, and may add its own choices here, such as whether the agency takes the payment from your bank and on which day.
2. To try the channel without filing, tick **Test submission** (it shows only when a channel offered takes tests). The file goes to the agency's test system: its answer shows as **Accepted as a test: nothing was filed**, and the return stays ready to file for real.
3. Choose **File electronically**.
4. With the manual channel, choose **Download**, file the file on the agency's site, then record the agency's answer: choose whether the agency accepted or rejected the return, the day it was filed, the confirmation number and any message, and choose **Record the answer**.
5. When the agency accepts the return, the return is marked as filed with its confirmation number. When it rejects it, correct the return and file it again.

A channel that sends returns itself asks the agency for its answer every 15 minutes; choose **Check the answer** to ask now.
