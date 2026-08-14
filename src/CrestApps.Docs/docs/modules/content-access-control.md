---
sidebar_label: Content Access Control
sidebar_position: 4
title: Content Access Control Feature
description: Role-based content access restrictions for Orchard Core content items.
---

| | |
| --- | --- |
| **Feature Name** | Content Access Control |
| **Feature ID** | `CrestApps.OrchardCore.ContentAccessControl` |

Provides a way to control who can access content items.

## Overview

The screencast below shows the **Restrict content?** setting on the *Page* content type's **Role Picker** part, then creates a page and uses the improved role selector — a searchable multi-select with **Select All / Deselect All** actions and tick indicators — to grant access to specific roles before publishing.

<video controls preload="metadata" width="100%" aria-label="Screen cast of the Restrict content setting and the improved role selector picking roles on a page">
  <source src="/img/docs/content-access-control.mp4" type="video/mp4" />
</video>

This feature allows you to restrict access to content items based on user roles. Once enabled, you can add the `RolePickerPart` to any content type. This part lets you specify one or more roles required to access the content item. You can attach the part using the content definitions user interface or by adding a migration, as shown below:

> Note: You must set the `Restrict content?` setting to `true` to enable the access control feature. This is part of the `RolePickerPart` settings, which can be configured via the user interface or through a migration.

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
