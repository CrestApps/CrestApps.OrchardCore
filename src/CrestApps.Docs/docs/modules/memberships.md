---
sidebar_label: Memberships
title: Memberships
description: "Memberships: a product whose purchase puts the buyer in roles for its length, extended by buying again, ended by a full refund, a cancellation, its end date or the store, so content gated by those roles opens and closes with it."
user_manual: user-manual/commerce/memberships
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Memberships` (Memberships) |
| **Category** | Commerce |
| **Depends on** | Commerce, Orders, Roles |
| **Seams it fills** | `IOrderEventHandler` (Orders), `ICartLineHandler` (Carts) |

The **Memberships** feature sells access. The attachable **Membership** part (`MembershipPart`: `RoleNames`, `DurationDays`) makes a product a membership. Buying it puts the buyer in its roles, so whatever those roles gate opens to them, such as content restricted with Content Access Control. Admin screens are described in the [User Manual](../user-manual/commerce/memberships.md). The **Memberships sample** recipe adds a Member role, a members' article and an annual membership.

## Memberships

This video shows the Membership part and the Memberships list:

<YouTube video="subscriptions" title="Video overview of subscriptions and memberships" />

`Membership` (collection `Memberships`, store `IMembershipStore`) holds the following:

- The member (`UserId`, `UserName`), the product and its title.
- `RoleNames`, and `AddedRoleNames`: the roles it put the member in that they weren't in already.
- The orders that bought or extended it.
- `StartsUtc`, and `EndsUtc` (`null` for one that doesn't end).
- `MembershipStatus`: Active, Expired or Revoked.

`MembershipService` does the work:

- **`GrantAsync`** runs when a paid order has membership lines. It makes a membership, or extends the member's active one for that product, by `MembershipPeriods.Extend`: the part's days for each unit, from the current end when it hasn't passed, else from now. A guest's order makes none.
- **`EndAsync`** takes the member out of the roles the membership added that no other active membership of theirs gives. It then updates their security stamp, so a sign-in that still carries the roles is checked again.
- **`ExpireAsync`** ends memberships past their end date. `MembershipsBackgroundTask` calls it hourly.

`MembershipOrderEventHandler` derives from `OrderGrantHandlerBase` (Orders.Core) and does the following on each order event:

- **Paid:** grants the memberships. When the buyer is the one signed in on the request, it refreshes their sign-in, so the roles apply at once.
- **Canceled:** ends the memberships the order bought.
- **Refunded:** ends them only when the order is refunded in full.

`MembershipCartLineHandler` refuses a membership line to a guest, since it's for an account.

## Gating content

Content Access Control restricts a content type's items to the roles chosen on their **RolePickerPart**, when the part's settings restrict content. `RoleBasedContentItemAuthorizationHandler` refuses **View content** on such an item to whoever isn't in one of its roles, even when the permission was granted to everyone, anonymous visitors included. Administrators see every item.

## Pages

**Commerce > Memberships** (`ManageMemberships`) lists memberships with their member, roles and end date, filtered by status. **End now** ends one without a refund. The storefront account's **Memberships** page (`account/memberships`, the `AccountMemberships` shape) lists the member's own.
