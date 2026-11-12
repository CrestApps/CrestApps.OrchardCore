---
sidebar_label: Accounts
sidebar_position: 15
title: Accounts
description: The Account content type, the one record for the businesses and households that contacts, orders, assets and work orders belong to, with the account picker, the account search, the account page and the extension points other modules use.
user_manual: user-manual/accounts
---

| | |
| --- | --- |
| **Features** | Accounts (`CrestApps.OrchardCore.Accounts`), Account Members (`CrestApps.OrchardCore.Accounts.Members`), Account Locations (`CrestApps.OrchardCore.Accounts.Locations`) |
| **Core** | `CrestApps.OrchardCore.Accounts.Core` |
| **Abstractions** | `CrestApps.OrchardCore.Accounts.Abstractions` |
| **Category** | Content Management |
| **Depends on** | `CrestApps.OrchardCore.Resources`, `OrchardCore.Contents`, `OrchardCore.ContentTypes`, `OrchardCore.Lists`, `OrchardCore.Title`; Account Members also needs `OrchardCore.Users`, and Account Locations [Addresses](addresses.md) |

An account is the business or household that other records belong to. It is one content type, `Account`, that every
module extends instead of keeping an organization record of its own:

- the [CRM](../omnichannel/crm.md) puts contacts and opportunities in an account;
- [Accounts B2B](accounts-b2b.md) lets an account buy as a business, with terms and price lists, its members as buyers
  and its locations as shipping addresses;
- Field Service links assets, work orders and service agreements to an account.

The module has three features. **Accounts** is the account type and everything below. **Account Members** and
**Account Locations** add the data more than one module needs: [the users who sign in for an
account](#account-members) and [the places it has](#account-locations). A module that needs them depends on the
feature; nothing about them is commerce-specific.

## The account type

The feature's migration defines `AccountPart` and creates the `Account` type when no type of that name exists. The
type has these parts:

| Part | Position | |
| --- | --- | --- |
| `TitlePart` | 1 | The account's name. |
| `AccountPart` | 2 | The marker that makes a type an account type. It holds no data. |
| `ListPart` | 3 | The records the account holds, such as contacts. Page size 20, with a header. |

Any type that carries `AccountPart` is an account type, so a site can add more of them, such as `Household`. Code
never tests for the type name; it uses `AccountRecordKinds.IsAccount(definition)` or
`IAccountContentTypeProvider.IsAccountTypeAsync(contentType)`.

On a site where the CRM created the `Account` type before accounts had a module of their own, the type and part already
exist under the same names. The migration then changes nothing, so the type's settings and every account stay as they
are.

## What belongs to an account

The Accounts module knows no child types of its own. A module whose records can sit in an account registers an
`IAccountChildTypeRule`:

```csharp
public sealed class MemberAccountChildTypeRule : IAccountChildTypeRule
{
    public bool CanBelongToAccount(ContentTypeDefinition definition)
        => AccountRecordKinds.HasPart(definition, "MemberPart");

    public bool MustNotBelongToAccount(ContentTypeDefinition definition)
        => false;
}

services.AddScoped<IAccountChildTypeRule, MemberAccountChildTypeRule>();
```

- A type is a child when at least one rule allows it, no rule excludes it, and it is not an account type itself.
- The CRM's rule allows contact and opportunity types and excludes lead types.

`AccountListSynchronizer` keeps the `ListPart` of every account type in line with the rules. It runs:

- when a part is attached to or detached from a type;
- when a type is imported;
- after a recipe's `ContentDefinition` or `ReplaceContentDefinition` step.

A type is added to the list once, the first time it is seen, and recorded in `AccountPartSettings.OfferedContentTypes`.
An administrator who takes it out of the list keeps it out. An excluded type is always taken out.

A record belongs to an account through Orchard Core's `ContainedPart`, the same way any list item does, so
`ContainedPartIndex.ListContentItemId` finds an account's records.

## The account picker

Every type that can belong to an account gets an **Account** field on its editor. It is a content display driver, not a
part, so no type needs setting up. The field:

- writes the record's `ContainedPart`, placing it in the account, moving it, or taking it out;
- leaves alone a record that is kept in a list that is not an account;
- shows the account as a badge on the record's row in the admin list.

The picker uses the shared item selector, backed by the account search.

## The account search

```http
GET /Admin/api/crestapps/accounts/search?query=acme
```

The search returns up to 50 accounts whose title contains the query, ordered by title, as `{ value, text }` items:
the content item id and the title. It needs `ListContent`, and searches only the account types the user may view
(`ViewContent` for the type), since an account type is securable. The picker places a record only in an account its
editor may view. The route is named `CrestApps.Accounts.Search`
(`AccountsConstants.RouteNames.Search`), so views link to it with `Url.RouteUrl` rather than a hard-coded path.

## The account page

An account is the umbrella over everything that belongs to it. Its page is Orchard Core's content display page
(**View** on its row, `DetailAdmin`), with its title, breadcrumb and toolbar (**Edit**, **New**, **List Activities**),
and below them the account's sections: a list down the left, and the chosen section on the right. On a phone the list
is a dropdown. Each feature adds its own section, or a card in one, with a content display driver placed through
`AccountPageTabs`:

| Tab | `AccountPageTabs` | Added by |
| --- | --- | --- |
| **Overview** | `Overview` (0) | Account Members (the members and their roles) and Account Locations (the addresses). |
| **Contacts** | `Contacts` (10) | The [CRM](../omnichannel/crm.md#the-account-page): the account's contacts, with their email and phone, and a **New** button per contact type. |
| **Opportunities** | `Opportunities` (20) | The CRM: open deals first, with their stage, amount and close date. |
| **Leads** | `Leads` (30) | The CRM: the leads converted into the account's contacts. A lead never sits in an account itself. |
| **Activities** | `Activities` (40) | The CRM: the scheduled and completed activities of the account's contacts, with **List Activities**. |
| **Orders** | `Orders` (50) | [Orders](carts-and-orders.md#the-account-page): the latest orders placed for the account (`Order.AccountId`). |
| **Business** | `Business` (60) | [Accounts B2B](accounts-b2b.md#the-account-page): its terms, what it owes and its buyers. |
| **Invoices** | `Invoices` (70) | Accounts B2B: the invoices for what it bought on its terms. |
| **Service** | `Service` (80) | Reserved for field service: work orders, assets and service agreements. |
| **Records** | `Records` (100) | Accounts: Orchard Core's list of every record the account holds, with the buttons that add one. |

A section shows only when the feature that adds it is on and the user may see what it shows; each one checks its own
permissions. `AccountPageTabs.Location(tab, position)` returns the placement, such as `Content:1#Orders;50`: an
ordinary Orchard Core tab placement, so every module's sections come out in the same order. A tab name the class
doesn't know goes after **Service**, under its own name.

The `ContentZone__AccountPage` alternate of the page's `Content` zone, added by `AccountPageShapeTableProvider`, draws
those tabs as the side list instead of Orchard Core's tab strip, so the page stays tidy however many modules add one. It
uses the shared page sections of the Resources module (the `PageSections` shape), which the staff profile uses too: each
section's title, icon and position are registered with `services.AddPageSection(AccountPageTabs.PageName, name, title,
icon, position)`. Accounts registers the sections it knows; a module that adds a new section registers it the same way.
The chosen section is kept in the address's hash (`#account-contacts`), so a link, or a record's editor returning to the
account, opens that section.

```csharp
internal sealed class AccountWorkOrdersDisplayDriver : ContentDisplayDriver
{
    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override IDisplayResult Display(ContentItem contentItem, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin)
        {
            return null;
        }

        return Initialize<AccountWorkOrdersViewModel>("AccountWorkOrders", model => model.AccountId = contentItem.ContentItemId)
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Service));
    }
}
```

The module's `placement.json` (`contentPart: AccountPart`) moves the `ListPart`'s list to the **Records** section,
puts its toolbar in the page's header, and hides the title card the page heading already shows.

The account's editor is laid out the same way: its title first, then a **Members**, a **Locations** and a **Business**
tab, placed with `AccountPageTabs.EditorLocation(tab)`.

### Records that refer to an account by id

Records kept outside the content store name the account by its content item id: `Order.AccountId`, and a
transaction's parent reference (`ParentReferenceType` `AccountsConstants.ReferenceTypes.Account`, `ParentReferenceId`
the account's id), which is indexed and read with `ITransactionStore.GetByParentReferenceAsync`.

## Account members

The **Account Members** feature lets users sign in for an account. Its migration defines `AccountMembersPart` and adds
it to the `Account` type at position 4; another account type can be given the part by hand.

| `AccountMember` | |
| --- | --- |
| `UserId` | The user. A user is a member of one account at most. |
| `UserName`, `Email` | As they were when the account was saved. |
| `Role` | `AccountMemberRole.Member`, or `Admin`: a member who also manages the account for the others. What an admin may do is up to each module; Accounts B2B lets admins see every order and approve them. |
| `ContactContentItemId` | The account's contact with the member's email, or none. |

- **Editor.** A **Members** card on the account's editor takes each member's user name or email and role. It needs
  `ManageAccountMembers` (**Manage the users who sign in for accounts**), given to the Administrator role: adding
  someone to an account lets them act for it, such as buying on its terms. It refuses a user who is a member of another
  account.
- **Index.** `AccountMemberIndex` (`AccountContentItemId`, `UserId`, `Published`, `Latest`) holds one row per member
  of each version.
- **Lookup.** `IAccountMemberService.FindAccountAsync(userId)` returns the published account a user signs in for;
  `FindOtherAccountIdAsync(userId, exceptAccountId)` tells an editor the user already belongs to another one.
- **Contacts.** When an account is saved, each member is linked to the account's contact with their email, through the
  registered `IAccountContactFinder`s. The CRM registers one that matches a contact's primary email among the contacts
  in the account. A member with no match, or more than one, has no contact.

A module keeps its own settings for a member by user id, in a part of its own. Accounts B2B keeps each buyer's order
limit that way:

```csharp
var account = await accountMemberService.FindAccountAsync(userId);
var member = account?.Get<AccountMembersPart>()?.FindMember(userId);
```

## Account locations

The **Account Locations** feature keeps the places an account has. Its migration defines `AccountLocationsPart` and
adds it to the `Account` type at position 5.

| `AccountLocation` | |
| --- | --- |
| `LocationId` | Stays the same when the location is renamed or moved. |
| `Name` | Such as *Head office*; the city when left blank. |
| `Address` | An `Address`, entered and checked with the shared [address editor](addresses.md), addressed to the account. |

`AccountLocationsPart.FindLocation(locationId)` finds one. Accounts B2B offers them as shipping addresses at checkout;
a field service module offers them as service addresses.

## Removing an account

When an account's last version is removed, its records are detached: their `ContainedPart` is removed after the
request, in batches of 100, so they keep working as records of their own.

Records that refer to an account by its id rather than by `ContainedPart`, such as orders (`Order.AccountId`) and work
orders, keep the id of the removed account. Nothing stops an account that still owes money on its B2B terms from being
removed, so check what it owes on its page first. A module that needs to act when an account goes adds a
`ContentPartHandler<AccountPart>` of its own.

## Import and export

With Content Transfer on, every type that can belong to an account gets an `Account` column (also read as
`Account Name` or `AccountName`):

- **Import.** The value names an account by its title. A name that matches no account, or more than one, leaves the
  record where it was.
- **Export.** The column carries the account's title.

## Admin menu

The feature adds a top-level **Accounts** item, Orchard Core's content list scoped to the account types. A module that
lists accounts in its own menu turns it off. `AccountsAdminMenuOptions` is in the abstractions package, so a module
needs no reference to the Accounts module to do it:

```csharp
services.Configure<AccountsAdminMenuOptions>(options => options.ShowAccountsMenu = false);
```

The CRM does this and lists accounts under **Interaction Center > Accounts**.

## Recipes and deployment

The **Accounts** deployment step (`AccountsDeploymentStep`, under **Content**) exports every account, with all the parts
on it, such as its members, locations and business buying, as an `Accounts` recipe step. With **Also export the records
the accounts hold** on (`IncludeRecords`, the default), it also exports the content items in the accounts' lists, such
as their contacts and opportunities.

```json
{
  "name": "Accounts",
  "Accounts": [
    {
      "ContentItemId": "4xf4a7cq978mj04mg6nfygm3dd",
      "ContentItemVersionId": "4v7cfa9x3jhpc8w7m6d0x4w1qe",
      "ContentType": "Account",
      "DisplayText": "Northwind Traders",
      "Latest": true,
      "Published": true,
      "TitlePart": { "Title": "Northwind Traders" },
      "AccountPart": {},
      "AccountLocationsPart": {
        "Locations": [
          {
            "LocationId": "hq",
            "Name": "Head office",
            "Address": { "Country": "US", "AddressLine1": "1 Main Street", "City": "Reno", "Region": "NV", "PostalCode": "89501" }
          }
        ]
      }
    }
  ],
  "Records": [
    {
      "ContentItemId": "4c2k1m9qz8a7b6c5d4e3f2g1h0",
      "ContentItemVersionId": "4c2k1m9qz8a7b6c5d4e3f2g1hv",
      "ContentType": "Contact",
      "DisplayText": "Ada Lovelace",
      "Latest": true,
      "Published": true,
      "ContainedPart": { "ListContentItemId": "4xf4a7cq978mj04mg6nfygm3dd", "ListContentType": "Account" }
    }
  ]
}
```

The `Accounts` step imports them as Orchard Core's `Content` step does: an item with an existing `ContentItemId` is
updated, so running the same recipe twice changes nothing. Accounts are imported before their records, so each record
lands in its account. An item in `Accounts` without `AccountPart`, or an item without a `ContentItemId` or
`ContentType`, is reported as a recipe error and skipped. With CrestApps Recipes on, `AccountsRecipeStepSchema` and
`AccountsDeploymentStepSchema` describe both steps, so recipes are checked and written with help.

Accounts are still ordinary content items, so Orchard Core's `Content` step and **All Content** deployment step move
them too. B2B price lists have their own `PriceLists` step (see [Accounts B2B](accounts-b2b.md#recipes-and-deployment)),
and orders and invoices move with the Orders and Transactions modules.

A recipe enables the features with the `feature` step:

```json
{
  "name": "feature",
  "enable": [
    "CrestApps.OrchardCore.Accounts",
    "CrestApps.OrchardCore.Accounts.Members",
    "CrestApps.OrchardCore.Accounts.Locations"
  ]
}
```

An account's members and locations are parts of the account, so they travel with it.

## Permissions

Accounts use Orchard Core's content permissions. The `Account` type is securable, so a role can be given rights to
accounts alone. Choosing an account's members also needs `ManageAccountMembers`.

## Account Team

The **Account Team** feature (`CrestApps.OrchardCore.Accounts.Team`, needs [Staff](./staff.md)) adds the `AccountTeamPart` to the Account type: an **owner** (the account manager or sales rep who answers for the account) and the rest of the **account team**, both picked from staff with the `StaffPicker`. The account page shows them in its Overview tab, with links to their staff pages. The part's values are staff member ids (`OwnerStaffId`, `TeamStaffIds`).
