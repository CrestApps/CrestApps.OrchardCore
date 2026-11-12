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
type. When it is `true`, the roles picked on each item decide who may view that item.

How administrators turn the restriction on for a content type and how editors restrict an item is described in the
User Manual: [Restrict Content by Role](../user-manual/administration/content-access-control.md).

## How authorization works

The feature registers `RoleBasedContentItemAuthorizationHandler`, an `IAuthorizationHandler` for
`PermissionRequirement`:

- It acts on the `ViewContent`, `PreviewContent` and `CloneContent` permissions (and their *own* variants) checked
  against a `ContentItem` resource: a preview or a clone shows everything the item holds. Edit, publish and delete
  checks are not affected.
- It collects the role names from every `RolePickerPart` on the content type (named or unnamed) whose settings have
  `IsContentRestricted` set to `true`.
- If the user is in any of those roles, the requirement succeeds. If roles were collected and the user is in none
  of them, the handler fails the requirement. If no roles were picked, the item is not restricted.
- It runs even when another handler already succeeded the requirement, as the permission handler does for everyone
  who has **View content**, anonymous visitors included: a restricted item is refused to whoever isn't in its roles.
  Users in the Administrator role see every item.

## Where restricted items are left out

Beyond the item's own page, a restricted item is kept out of the other places that show content:

| Surface | How | Needs |
| --- | --- | --- |
| Sitemaps | `RestrictedContentSitemapFilter` (`ISitemapContentItemExtendedMetadataProvider`) leaves every restricted item out. A sitemap is built once and cached for everyone, so the item is left out whoever asks. | Sitemaps (`OrchardCore.Sitemaps`) |
| RSS feeds | `RestrictedContentFeedItemBuilder` wraps the feed item builder and removes each item the reader may not view. | Feeds (`OrchardCore.Feeds`) |
| Search results page | `RestrictedContentSearchService` wraps every provider's `ISearchService` (Lucene, Elasticsearch, Azure AI Search) and removes the items the searcher may not view; the total count is lowered by the items removed from the page. | Search (`OrchardCore.Search`) |
| Search index | `ContentAccessIndexHandler` adds each role of a restricted item to its document as the keyword field `Content.ContentAccessControl.RoleNames`, so a query, an AI data source or a custom search page can filter by the reader's roles. | Indexing (`OrchardCore.Indexing`) |
| GraphQL `contentItem` and content type list queries | Orchard Core checks `ViewContent` on every item they return, so this handler leaves restricted items out. | GraphQL |
| Preview and clone | The handler refuses `PreviewContent` and `CloneContent` on a restricted item. | |

### Known gaps

- **Search engines can't skip an item:** Orchard Core's indexing has no hook that leaves an item out of an index, so a
  restricted item is still indexed (with its roles). Anything that queries the index directly (the Lucene or
  Elasticsearch query APIs, Queries) must filter on `Content.ContentAccessControl.RoleNames` itself.
- **GraphQL paging and nested fields:** list queries are filtered after paging, so `first: 10` can return fewer
  items, and items reached through a content picker, a list or a taxonomy field are not checked by Orchard Core.
- **The editor's live preview** (`PreviewController.Draft`) builds an item from the posted editor and checks the
  permission without the item; it is reached only from an editor the user could already open.

You can attach the part and set **Restrict content?** using the content definitions user interface or a migration.

Here is an example of how to create or update a content type named `CustomContentType`, where access to its content items is restricted for all roles **except** "Administrator", "Authenticated", and "Anonymous".

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
                    ExcludedRoles = ["Administrator", "Authenticated", "Anonymous"],
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
