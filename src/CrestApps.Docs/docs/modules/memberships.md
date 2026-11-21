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

<YouTube video="memberships" title="Narrated video overview of memberships" />

## Memberships

This video shows the Membership part and the Memberships list:

<YouTube video="subscriptions" title="Video overview of subscriptions and memberships" />

`Membership` (collection `Memberships`, store `IMembershipStore`) holds the following:

- The member (`UserId`, `UserName`), the product and its title.
- `RoleNames`, and `AddedRoleNames`: the roles a membership made before role grants were recorded put the member in. New memberships leave `AddedRoleNames` empty, because the role grants record it.
- The orders that bought or extended it.
- `StartsUtc`, and `EndsUtc` (`null` for one that doesn't end).
- `MembershipStatus`: Active, Expired or Revoked.

`MembershipService` does the work:

- **`GrantAsync`** runs when a paid order has membership lines. It makes a membership, or extends the member's active one for that product, by `MembershipPeriods.Extend`: the part's days for each unit, from the current end when it hasn't passed, else from now. A guest's order makes none.
- **`EndAsync`** revokes the membership's role grants. The member leaves a role only when the membership put them in it and nothing else, such as another membership or a subscription, still grants it. Their security stamp is then updated, so a sign-in that still carries the role is checked again.

Roles go through `IRoleGrantService` (`CrestApps.OrchardCore.Users.Abstractions`, registered with `services.AddRoleGrants()` from `CrestApps.OrchardCore.Users.Core`), which records each grant on the user by source (`Membership`) and source ID (the membership). `MembershipRoleGrantSource` reports the grants of memberships made before grants were recorded, the first time the service touches a member, so their roles are kept and removed as before.
- **`ExpireAsync`** ends memberships past their end date. `MembershipsBackgroundTask` calls it hourly.

`MembershipOrderEventHandler` derives from `OrderLineGrantHandlerBase<MembershipPart>` (Orders.Core), which reads each line's roles and length from the product version the order bought, and does the following on each order event:

- **Paid:** grants the memberships. When the buyer is the one signed in on the request, it refreshes their sign-in, so the roles apply at once.
- **Canceled:** ends the memberships the order bought.
- **Refunded:** ends them only when the order is refunded in full.

`MembershipCartLineHandler` refuses a membership line to a guest, since it's for an account.

## Which roles a membership may grant

A purchase never makes anyone an administrator. `RoleGrantRules` (`CrestApps.OrchardCore.Users.Abstractions`) names the roles no grant gives (Anonymous, Authenticated and Administrator), and `IRoleGrantService` refuses them for every source, memberships and subscriptions alike. `MembershipService.GrantLineAsync`, where every way of granting a membership ends, also drops any role outside **Settings > Commerce > Memberships > Roles a membership may grant** (`MembershipSettings.GrantableRoles`) when that list names any, so a product saved by a recipe or an API write can't grant more than the editor allows.

The Membership part's roles are changed only by someone with both **Manage memberships** and Orchard Core's **Assign roles to users**; anyone else who edits the product keeps the roles it has. The settings page asks for the same two permissions.

## Gating content

Content Access Control restricts a content type's items to the roles chosen on their **RolePickerPart**, when the part's settings restrict content. `RoleBasedContentItemAuthorizationHandler` refuses **View content** on such an item to whoever isn't in one of its roles, even when the permission was granted to everyone, anonymous visitors included. Administrators see every item.

## Recipes and deployment

The `Memberships` recipe step (`MembershipsStep`) creates or updates memberships. The member is named by `UserName`, which must exist on the site; the member's id there is stored with the membership. A membership is matched by its `ItemId`, then by its member, its product (`ContentItemId`) and `StartsUtc`, so running a recipe twice changes nothing. An active membership grants its `RoleNames` through the role grants, as a bought one does. The **Memberships** deployment step (`MembershipsDeploymentStep`, *Commerce*) writes every membership; the member's user id and the roles a membership put its member in (`AddedRoleNames`) belong to the site, so they are left out.

```json
{
  "name": "Memberships",
  "Memberships": [
    { "ItemId": "4ms00000000000000000000001", "UserName": "ana", "ContentItemId": "4gold0000000000000000000001", "Title": "Gold", "RoleNames": [ "Member" ], "StartsUtc": "2026-01-01T00:00:00Z", "EndsUtc": "2027-01-01T00:00:00Z", "Status": "Active" }
  ]
}
```

## Pages

**Commerce > Memberships** (`ManageMemberships`) lists memberships with their member, roles and end date, filtered by status. **End now** ends one without a refund. The storefront account's **Memberships** page (`account/memberships`, the `AccountMemberships` shape) lists the member's own.
