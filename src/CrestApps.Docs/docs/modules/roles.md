---
sidebar_label: Roles
sidebar_position: 3
title: Enhanced Roles
description: Extends the Orchard Core Roles module with additional reusable components like RolePickerPart.
user_manual:
  - user-manual/administration/roles
---

| | |
| --- | --- |
| **Feature Name** | Enhanced Roles |
| **Feature ID** | `CrestApps.OrchardCore.Roles` |

Provides a way to enhance the role management experience.

## RolePickerPart

`RolePickerPart` (display name **Role Picker**) is an attachable, reusable content part that stores a list of
role names on a content item (`RolePickerPart.RoleNames`). The editor uses an enhanced dropdown (bootstrap-select)
with live search and, when multiple selection is enabled, select-all and deselect-all actions. The
[Content Access Control](content-access-control.md) feature builds on it to restrict who can view an item.

How administrators attach the part and how editors pick roles is described in the User Manual:
[Roles and Permissions](../user-manual/administration/roles.md#let-editors-pick-roles-on-a-content-item).
That page also explains roles and permissions in plain words for the people who manage access.

### Settings

| `RolePickerPartSettings` property | Admin label | Description |
| --- | --- | --- |
| `ExcludedRoles` | **Exclude roles** | Roles removed from the picker. Excluded roles are also stripped from the saved value. |
| `Required` | **Required?** | At least one role must be selected. |
| `AllowSelectMultiple` | **Allow multiple?** | Allows more than one role. When off, saving more than one role fails validation. |
| `Hint` | **Hint** | Help text shown under the picker. |

You can attach the part with the Orchard Core content types UI or with a migration. For example:

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
                .WithDisplayName("Roles")
                .WithSettings(new RolePickerPartSettings()
                {
                    AllowSelectMultiple = true,
                    Required = true,
                    Hint = "Select one or more roles",
                    ExcludedRoles = ["Authenticated", "Anonymous"],
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
