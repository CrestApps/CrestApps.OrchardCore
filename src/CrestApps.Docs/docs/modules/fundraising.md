---
sidebar_label: Fundraising
title: Fundraising
description: "Fundraising: campaigns donors give their own amount to, from suggested amounts or their own and in honor of someone, with progress toward a goal on the campaign's page and a tax receipt for each donation, emailed and kept in the donor's account."
user_manual: user-manual/commerce/fundraising
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Fundraising` (Fundraising) |
| **Category** | Commerce |
| **Depends on** | Commerce, Orders |
| **Seams it fills** | `ICartLineHandler` (Carts), `IOrderEventHandler` (Orders) |

The **Fundraising** feature raises money. The attachable **Fundraising campaign** part makes a product a campaign that donors give their own amount to. Admin screens are described in the [User Manual](../user-manual/commerce/fundraising.md). The **Fundraising sample** recipe adds a Fundraising Campaign type and a campaign raising $10,000.

## The campaign part

`FundraisingCampaignPart` holds the following, in the product's currency:

- `Goal`: zero for none.
- `EndsUtc`: `null` for no end.
- `SuggestedAmounts`: up to six.
- `MinimumAmount`.
- `TaxDeductible`.

On the product's page, the part adds two shapes to the `ProductAction` display:

- **`CampaignProgress`**: what's been raised toward the goal, by how many donors, and the days left.
- **`DonateForm`**: the suggested amounts in the shopper's currency, rounded to whole units, a field for their own amount, and one for who the gift honors. The fields belong to the Add to cart form, posting `details[donation.amount]` and `details[donation.tribute]`.

The part is in `CartOptions.ChooseOnProductPageParts`, so product cards send shoppers to the page to choose.

## The cart line

`DonationLineHandler` makes the donor's amount the line's price:

- **Prepare** reads the amount in the invariant culture (`DonationAmounts.Parse`), so "25,50" is never read as 2550. It refuses one under the campaign's least, and keeps the tribute, up to 120 characters.
- **Evaluate** sets the unit price to the amount, as chosen in the cart's currency, and limits the line to one. It sets `ExcludeFromDiscounts`, which the cart copies onto the order line, so no promotion, company price or role discount takes anything off a gift. It reports a problem once the campaign has ended.

## Donations

`Donation` (collection `Fundraising`, store `IDonationStore`) holds the campaign, the order and line, the donor, the amount in the currency paid, the amount in the campaign's currency (`CampaignAmount`, converted when the order is paid), the tribute, its receipt number, and what was refunded.

`DonationService` does the following:

- **`RecordAsync`** runs when an order is paid. It records a donation for each campaign line, numbered `{order number}-{line}`, and emails a receipt for each tax-deductible one when receipts are emailed. Recording an order twice records nothing more.
- **`RefundAsync`** takes a refund off the order's donations, in proportion to each.
- **`GetProgressAsync`** adds up what's been given and not refunded, in the campaign's currency, and counts the donors.

`FundraisingSettings` (**Settings > Commerce > Fundraising**, `ManageFundraising`) holds the organization named on receipts (the site's name when empty), its tax ID, the receipt statement, and whether receipts are emailed.

## Pages

The storefront account's **Donations** page (`account/donations`) lists the donor's donations. Each tax-deductible one links to its printable receipt (`account/donations/{id}/receipt`), which only its donor can open.
