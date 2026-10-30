---
sidebar_label: Memberships
title: Memberships
description: "Sell memberships that open content to members: a product that puts its buyer in a role for a year, a month or for good, renewed by buying again, and ended by a refund, a cancellation or its end date."
technical_manual:
  - modules/memberships
---

Sell a membership: buying it makes the buyer a member, and what's for members opens to them, such as articles, guides or downloads only members see.

| | |
| --- | --- |
| **Menu** | Commerce > Memberships |
| **Permission** | Manage memberships |
| **Feature** | Memberships, with Content Access Control to keep content for members |

<AskYourAdmin />

This video shows how to sell a membership, keep content for members, what a member sees, and how memberships end:

<YouTube video="memberships" title="Narrated video overview of memberships" />

To try it, run the **Memberships sample** recipe under **Configuration > Recipes**. It adds a Member role, a members' article, and an Annual Membership at $49.

## Sell a membership

<YouTube video="memberships" title="Video of selling a membership" start={73} end={107} />

1. Under **Access Control > Roles**, add a role for members, such as **Member**.
2. Add the **Membership** part to a product's content type. On the product, under **Membership**, tick the roles members are in.
3. Enter how many **Days a membership lasts**, such as 365, or 0 for one that doesn't end.

<YouTube video="memberships" title="Video of buying a membership and what a member sees" start={138} end={177} />

Buying it makes the buyer a member at once. Buying it again while still a member adds the days to when it ends. Buyers sign in to buy a membership, since it's for their account. They see theirs under **Memberships** in their account.

## Keep content for members

<YouTube video="memberships" title="Video of keeping content for members with the Role Picker" start={107} end={138} />

With **Content Access Control** on, add the **Role Picker** part to a content type, and in its settings tick that it restricts who sees the content. On each item, choose the roles that can see it, such as Member. Anyone else who opens it is asked to sign in, or told they don't have access. Administrators see everything.

## When a membership ends

<YouTube video="memberships" title="Video of managing and ending memberships" start={177} end={214} />

A membership ends:

- On its end date.
- When its order is canceled.
- When its order is refunded in full.
- When you choose **End now** under **Commerce > Memberships**, which refunds nothing and ends it at once, as **Ended early**. Under **Commerce > Memberships** you can search by member and filter by **Active**, **Expired** or **Ended early**.

A member's session keeps the roles it had when they signed in, so a member who has just bought (or lost) a membership may need to sign in again before the roles apply.

When it ends, the member is taken out of the roles it gave them, unless another membership still gives them those roles. Anyone who was in a role before buying stays in it.
