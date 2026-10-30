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

This video shows the Marketplace and Classifieds modules, with posting, approving, renewing and upgrading listings and the search:

<YouTube video="marketplace-classifieds" title="Narrated video overview of the Marketplace and Classifieds modules" />

To try it, run the **Classifieds sample** recipe under **Configuration > Recipes**. It adds a Classified Ad that signed-in visitors post, and a $5 upgrade that features an ad for 7 days.

## Let visitors post a kind of listing

<YouTube video="marketplace-classifieds" title="Video of letting visitors post a kind of listing" start={282} end={316} />

1. Add the **Classified** part to a content type, such as Classified Ad.
2. In the part's settings (**Design > Content Definition > Content Types**, then **Edit** on the part), enter **Days a listing runs**, and tick **A moderator approves each listing before it's shown**.

<YouTube video="marketplace-classifieds" title="Video of posting a listing" start={316} end={353} />

Signed-in visitors post from **My listings** in their account, with **Post a Classified Ad**. They can say where it is, a place and its map coordinates, which **Use where I am** fills in, so people searching nearby find it.

## Approve listings

<YouTube video="marketplace-classifieds" title="Video of approving and rejecting listings" start={353} end={389} />

Under **Content > Posted listings**, filter by **Waiting for approval**. **Approve** shows a listing for its term. **Reject** takes down a listing, with a reason if you give one, and the owner is emailed. When an owner edits an approved listing of a kind that needs approval, it waits for you again.

## When a listing ends

<YouTube video="marketplace-classifieds" title="Video of renewing and upgrading a listing" start={389} end={430} />

On its end date, a listing is taken down and its owner is emailed. They bring it back with **Renew** under **My listings**, which starts a new term. Renewing one that's still up adds a term to its end.

## Sell upgrades

Make a product an upgrade with the **Classified upgrade** part:

- **Feature** puts the listing at the top of searches, with a Featured badge, for its days.
- **Bump** moves it to the top once, as if just posted.

Owners buy upgrades from **My listings**, through the cart and checkout. The upgrade applies when the order is paid.

## The search

<YouTube video="marketplace-classifieds" title="Video of the listings search" start={430} end={450} />

Anyone finds listings at **/listings**, by kind, words and distance from where they are. Listings that have coordinates also show on a map.
