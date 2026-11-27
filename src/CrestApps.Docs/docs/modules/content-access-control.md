---
sidebar_label: Content Access Control
sidebar_position: 4
title: Content Access Control Feature
description: Role-based content access restrictions for Orchard Core content items.
user_manual:
  - user-manual/administration/content-access-control
---

| | |
| --- | --- |
| **Feature Name** | Content Access Control |
| **Feature ID** | `CrestApps.OrchardCore.ContentAccessControl` |

Provides a way to control who can access content items.

<YouTube video="content-access-control" title="Video overview of restricting content by role" />

## Overview

This feature allows you to restrict access to content items based on user roles. It depends on
[Enhanced Roles](roles.md) (`CrestApps.OrchardCore.Roles`) and adds a **Restrict content?** setting
(`RolePickerPartContentAccessControlSettings.IsContentRestricted`) to the `RolePickerPart` settings on a content
type. When it is `true`, the roles picked on each item decide who may view that item. The item editor then labels
the picker *Which roles can see this content* (when the part on the type has no display name of its own), shows
*Any role* while none is picked, and hints *Leave empty so everyone can see it.* (when the part has no hint); every
other role picker keeps its name, *Role Picker*.

How administrators turn the restriction on for a content type and how editors restrict an item is described in the
User Manual: [Restrict Content by Role](../user-manual/administration/content-access-control.md).

## How authorization works

The feature registers `RoleBasedContentItemAuthorizationHandler`, an `IAuthorizationHandler` for
`PermissionRequirement`:

- It acts on every permission that reads or changes a `ContentItem` resource: view, view own, preview, preview own,
  list, clone, clone own, edit, edit own, publish, publish own, delete and delete own, in general or for a securable
  type (such as `ViewContent_Handbook`), matched by name. A preview, a clone or the editor shows everything the item
  holds, and a securable type is checked with its own permissions, so without this a restricted item would leak through
  them. An editor outside an item's roles can't open it for editing, publish it or delete it, whatever content
  permission they hold; add editors to one of its roles (or give them the administrator role) to let them change it.
- It collects the role names from every `RolePickerPart` on the content type (named or unnamed) whose settings have
  `IsContentRestricted` set to `true`.
- If the user is in any of those roles, a view permission (`ViewContent` or the type's `ViewContent_{Type}`)
  succeeds; listing, previewing, cloning, editing, publishing and deleting still need their own permission, which this
  handler never grants. If
  roles were collected and the user is in none of them, the handler fails every reading permission. If no roles were
  picked, the item is not restricted.
- An older version (an audit trail snapshot, say) and a draft are also checked against the roles of the item's
  current published and latest versions, so opening or restoring a version can't remove or widen the restriction.
  Viewing the live (published) version meets only its own roles: a role change saved in a draft takes effect on the
  live page when the draft is published.
- **Authenticated** stands for anyone signed in and **Anonymous** for a visitor, though Orchard Core never puts
  either role in a user's claims: an item restricted to Authenticated opens for every signed-in user, one restricted
  to Anonymous only for visitors, and one restricted to both for everyone. `ContentAccessRoles` (in
  `CrestApps.OrchardCore.Roles.Core`) works out a reader's roles this way (`GetReaderRoles`, `IsInAnyRole`), so code
  that filters restricted content matches the handler.
- It runs even when another handler already succeeded the requirement, as the permission handler does for everyone
  who has **View content**, anonymous visitors included: a restricted item is refused to whoever isn't in its roles.
  Users in the Administrator role see every item.

## Where restricted items are left out

Beyond the item's own page, a restricted item is kept out of the other places that show content:

| Surface | How | Needs |
| --- | --- | --- |
| Sitemaps | `RestrictedContentSitemapFilter` (`ISitemapContentItemExtendedMetadataProvider`) leaves every restricted item out. A sitemap is built once and cached for everyone, so the item is left out whoever asks. | Sitemaps (`OrchardCore.Sitemaps`) |
| RSS feeds | `RestrictedContentFeedItemBuilder` wraps the feed item builder and removes each item the reader may not view. The feed of a list the reader may not view (a members-only blog) is emptied, its title and description included. | Feeds (`OrchardCore.Feeds`) |
| Search results page | `RestrictedContentSearchService` wraps every provider's `ISearchService` (Lucene, Elasticsearch, Azure AI Search) and removes the items the searcher may not view. The page window the search page asks for is kept; when it holds restricted hits, the hits right after it refill it (at most five windows read and authorized per search, whichever page is asked for), so a page with restricted hits is still full and the next-page link still shows. The hits that refilled a page show again at the top of the next one. | Search (`OrchardCore.Search`) |
| Search index | `ContentAccessIndexHandler` adds each role of a restricted item to its document as the keyword field `Content.ContentAccessControl.RoleNames`, so a query, an AI data source or a custom search page can filter by the reader's roles. | Indexing (`OrchardCore.Indexing`) |
| GraphQL `contentItem` and content type list queries | Orchard Core checks `ViewContent` on every item they return, so this handler leaves restricted items out. | GraphQL |
| Preview and clone | The handler refuses `PreviewContent` and `CloneContent` on a restricted item. | |
| Edit, publish and delete | The handler refuses `EditContent`, `PublishContent` and `DeleteContent` (and their own and per-type forms) on a restricted item to whoever isn't in its roles. | |

### Known gaps

- **Search engines can't skip an item:** Orchard Core's indexing has no hook that leaves an item out of an index, so a
  restricted item is still indexed (with its roles). Anything that queries the index directly (the Lucene or
  Elasticsearch query APIs, Queries) must filter on `Content.ContentAccessControl.RoleNames` itself.
- **Rebuild the indexes after enabling the feature:** the role field is written when an item is next indexed, so
  documents indexed before carry no roles until the index is rebuilt (**Search > Indexes > Rebuild**).
- **List rendering:** list parts, taxonomy term pages and Liquid or Queries summaries are not filtered yet.
- **GraphQL paging and nested fields:** list queries are filtered after paging, so `first: 10` can return fewer
  items, and items reached through a content picker, a list or a taxonomy field are not checked by Orchard Core.
- **The editor's live preview** (`PreviewController.Draft`) builds an item from the posted editor and checks the
  permission without the item; it is reached only from an editor the user could already open.

## Filtering lists and searches in SQL

Authorizing each item is fine for one page, but a list or a search of a restricted type would have to load every
item to drop the ones its reader can't open. The feature keeps `ContentAccessRoleIndex` (in
`CrestApps.OrchardCore.Roles.Core.Indexes`) for that: a row for each role allowed to read each published item of a
restricted type (`ContentItemId`, `ContentType`, `RoleName`). An item with no role picked is open to everyone, so it
gets two rows, `Anonymous` and `Authenticated`. Drafts and items of types that are not restricted get no row. When
the feature is first enabled, a deferred task indexes the published items that were restricted before.

Keep a query of published items to what its reader may open with a subquery on the index, matched on the content
item id against the reader's roles:

```csharp
var roles = ContentAccessRoles.GetReaderRoles(User).ToArray();
var query = session.QueryIndex<MyArticleIndex>();

if (!ContentAccessRoles.SeesEverything(User))
{
    query = query.Where(index => index.ContentItemId.IsIn<ContentAccessRoleIndex>(
        row => row.ContentItemId,
        row => row.RoleName.IsIn(roles)));
}
```

Only use it for a content type that has a restricted `RolePickerPart`: items of other types have no row and would
never be found. The [Knowledge Base](knowledge-base.md) filters the help center, its search and its suggestions this
way. A module that depends on the index declares the `CrestApps.OrchardCore.ContentAccessControl` feature
(`ContentAccessControlConstants.Feature`) as a dependency.

## AI knowledge bases

An AI knowledge base built from a content index answers anyone allowed to query its profile, without this module's
checks. So the module registers `ContentAccessIndexedDocumentVisibility` as an `IIndexedDocumentVisibility`: the AI data
source sync leaves out every item restricted to roles that leave out `Anonymous`, and removes one restricted after it was
indexed. Items with no role picked, and items of types without a restricting role picker, stay in. See
[Restricted content stays out](../ai/data-sources/index.md#restricted-content-stays-out).

## Restrict a content type in code

You can attach the part and set **Restrict content?** using the content definitions user interface or a migration.

Here is an example of how to create or update a content type named `CustomContentType`, whose items are restricted to the roles picked on them. "Administrator" is left out of the picker because administrators see every item anyway. Keep "Authenticated" in it to let an editor open an item to everyone signed in.

```csharp
internal sealed class CustomContentTypeMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CustomContentTypeMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("CustomContentType", type => type
            .WithPart<RolePickerPart>(part => part
                .WithDisplayName("Limit access to selected roles")
                .WithSettings(new RolePickerPartContentAccessControlSettings
                {
                    // Set the `Restrict content?` setting to `true` to enable the access control.
                    IsContentRestricted = true,
                })
                .WithSettings(new RolePickerPartSettings
                {
                    AllowSelectMultiple = true,
                    Required = true,
                    Hint = "Select one or more roles",
                    ExcludedRoles = ["Administrator"],
                })
            )
        );

        return 1;
    }
}
```

Finally, register this migration:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<CustomContentTypeMigrations>();
    }
}
```
