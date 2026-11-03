---
sidebar_label: Accounts
sidebar_position: 15
title: Accounts
description: The Account content type, the one record for the businesses and households that contacts, orders, assets and work orders belong to, with the account picker, the account search, the account page and the extension points other modules use.
user_manual: user-manual/accounts
---

| | |
| --- | --- |
| **Feature** | Accounts (`CrestApps.OrchardCore.Accounts`) |
| **Core** | `CrestApps.OrchardCore.Accounts.Core` |
| **Abstractions** | `CrestApps.OrchardCore.Accounts.Abstractions` |
| **Category** | Content Management |
| **Depends on** | `CrestApps.OrchardCore.Resources`, `OrchardCore.Contents`, `OrchardCore.ContentTypes`, `OrchardCore.Lists`, `OrchardCore.Title` |

An account is the business or household that other records belong to. It is one content type, `Account`, that every
module extends instead of keeping an organization record of its own:

- the [CRM](../omnichannel/crm.md) puts contacts and opportunities in an account;
- [Accounts B2B](accounts-b2b.md) lets an account buy as a business, with buyers, terms and price lists;
- Field Service links assets, work orders and service agreements to an account.

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
the content item id and the title. It needs `ListContent`. The route is named `CrestApps.Accounts.Search`
(`AccountsConstants.RouteNames.Search`), so views link to it with `Url.RouteUrl` rather than a hard-coded path.

## The account page

An account's page is Orchard Core's content display page (**View** on its row), which renders the account with the
`DetailAdmin` display type. Modules add cards to it with a content display driver:

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
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:20");
    }
}
```

The list part's records come first. Cards from other modules go at `Content:20` and after.

## Removing an account

When an account's last version is removed, its records are detached: their `ContainedPart` is removed after the
request, in batches of 100, so they keep working as records of their own.

## Import and export

With Content Transfer on, every type that can belong to an account gets an `Account` column (also read as
`Account Name` or `AccountName`):

- **Import.** The value names an account by its title. A name that matches no account, or more than one, leaves the
  record where it was.
- **Export.** The column carries the account's title.

## Admin menu

The feature adds a top-level **Accounts** item, Orchard Core's content list scoped to the account types. A module that
lists accounts in its own menu turns it off:

```csharp
services.Configure<AccountsAdminMenuOptions>(options => options.ShowAccountsMenu = false);
```

The CRM does this and lists accounts under **Interaction Center > Accounts**.

## Recipes and deployment

Accounts are content items, so Orchard Core's `ContentDefinition` and `Content` steps import and export them, with
every part another module adds. A recipe enables the feature with the `feature` step:

```json
{
  "name": "feature",
  "enable": [ "CrestApps.OrchardCore.Accounts" ]
}
```

## Permissions

Accounts use Orchard Core's content permissions. The `Account` type is securable, so a role can be given rights to
accounts alone.
