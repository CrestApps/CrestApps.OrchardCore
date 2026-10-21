---
sidebar_label: Classifieds
title: Classifieds
description: "Let visitors post listings from your site, such as ads, directory entries or properties: approve them before they show, let them run for a term and be renewed, let people search them near where they are, and sell upgrades that feature them."
technical_manual:
  - modules/classifieds
---

Let visitors post their own listings, such as ads, directory entries or homes for sale. You approve them, they run for a set number of days, and owners renew them or pay to have them featured.

| | |
| --- | --- |
| **Menu** | Content > Posted listings |
| **Permission** | Moderate posted listings; Post listings for the people who post |
| **Feature** | Classifieds |

<AskYourAdmin />

To try it, run the **Classifieds sample** recipe under **Configuration > Recipes**. It adds a Classified Ad that signed-in visitors post, and a $5 upgrade that features an ad for 7 days.

## Let visitors post a kind of listing

1. Add the **Classified** part to a content type, such as Classified Ad.
2. In the part's settings, enter how many **days** a listing runs, and tick whether listings wait for your approval.

Signed-in visitors post from **My listings** in their account. They can say where it is, a place and its map coordinates, which **Use where I am** fills in, so people searching nearby find it.

## Approve listings

Under **Content > Posted listings**, filter by **Waiting for approval**. **Approve** shows a listing for its term. **Reject** takes down a listing, with a reason if you give one, and the owner is emailed. When an owner edits an approved listing of a kind that needs approval, it waits for you again.

## When a listing ends

On its end date, a listing is taken down and its owner is emailed. They bring it back with **Renew** under **My listings**, which starts a new term. Renewing one that's still up adds a term to its end.

## Sell upgrades

Make a product an upgrade with the **Classified upgrade** part:

- **Feature** puts the listing at the top of searches, with a Featured badge, for its days.
- **Bump** moves it to the top once, as if just posted.

Owners buy upgrades from **My listings**, through the cart and checkout. The upgrade applies when the order is paid.

## The search

Anyone finds listings at **/listings**, by kind, words and distance from where they are. The results show on a map.
