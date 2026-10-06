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

- It only acts on the `ViewContent` permission checked against a `ContentItem` resource. Edit, publish and delete
  checks are not affected.
- It collects the role names from every `RolePickerPart` on the content type (named or unnamed) whose settings have
  `IsContentRestricted` set to `true`.
- If the user is in any of those roles, the requirement succeeds. If roles were collected and the user is in none
  of them, the handler fails the requirement. If no roles were picked, the item is not restricted.
- It returns early when another handler has already succeeded the requirement.

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
