---
sidebar_label: Fundraising
title: Fundraising
description: "Raise money with campaigns: donors choose a suggested amount or their own and can give in honor of someone, the campaign's page shows progress toward the goal, and each donor gets a tax receipt by email and in their account."
technical_manual:
  - modules/fundraising
---

Raise money for a cause. Donors give the amount they choose, the campaign's page shows how close you are to the goal, and each donor gets a receipt for their taxes.

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Fundraising |
| **Permission** | Manage fundraising |
| **Feature** | Fundraising |

<AskYourAdmin />

To try it, run the **Fundraising sample** recipe under **Configuration > Recipes**. It adds a campaign raising $10,000 for a community library.

## Set up your receipts

Under **Settings > Commerce > Fundraising**:

- **Organization**: who donations go to, as it reads on receipts. Leave it empty to use the site's name.
- **Tax ID**: your tax or charity registration number.
- **Receipt statement**: what the receipt says about the gift, such as that nothing was given in return.
- **Email donors their receipts**: untick it to have donors find receipts in their account only.

## Start a campaign

1. Add the **Fundraising campaign** part to a product's content type. The sample's **Fundraising Campaign** type has it.
2. On the campaign, enter its **Goal**, or leave it empty for none.
3. Enter the **Suggested amounts** offered as buttons, such as 25, 50, 100, and the **Least donation**.
4. Enter when it **Ends**, or leave it empty for no end.
5. Tick **Donations get a tax receipt** when gifts are tax-deductible.

Set the product's price display to hide its price, since donors choose the amount.

## How donors give

On the campaign's page, donors pick an amount or enter their own, and can name someone they're giving in honor or memory of. Amounts show in the donor's currency. They check out like any order. Discounts and promotion codes never take anything off a donation.

The page shows what's been raised toward the goal, how many have given, and the days left. Once a campaign ends, it stops taking donations.

## Receipts

Each donor is emailed a receipt with your organization, tax ID and statement. They also find their donations under **Donations** in their account, where each receipt opens to print.

When you refund a donation's order, the refund comes off the campaign's progress and shows on the donor's receipt.
