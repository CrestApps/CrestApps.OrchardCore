# Content Access Control

Enables you to control access to your content by assigning specific roles, ensuring only authorized users can view or manage items.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Content Access Control | `CrestApps.OrchardCore.ContentAccessControl` | Enables you to control access to your content by assigning specific roles, ensuring only authorized users can view or manage items. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

A restricted item is refused to whoever isn't in its roles when they view, preview or clone it, and it is left out of
sitemaps, RSS feeds and the search results page; its search document carries its roles. Search engines can't skip an
item, GraphQL nested fields aren't checked and GraphQL lists are filtered after paging: see **Known gaps** in the
documentation.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Roles`](../CrestApps.OrchardCore.Roles/README.md)

## Documentation

See [Content Access Control Feature](../../CrestApps.Docs/docs/modules/content-access-control.md) in the technical manual.
