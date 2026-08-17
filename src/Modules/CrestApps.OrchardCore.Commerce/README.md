# CrestApps OrchardCore Commerce

The Commerce module registers the shared **Commerce** admin menu and its icon. Commerce-related modules
(such as Transactions and Taxation) contribute their own screens under this single top-level menu instead of
each creating their own copy of it.

## Why this module exists

Several modules add entries under a top-level **Commerce** menu. When each module declared the menu on its
own, the menu icon appeared only when a specific module happened to provide it, and it disappeared when that
module was disabled. This module owns the top-level node, its identifier, and its icon, so the menu always
renders consistently whenever any commerce-related feature is enabled.

## Feature

- **Commerce** (`CrestApps.OrchardCore.Commerce`) — Registers the Commerce admin menu and icon. The feature
  is **enabled by dependency only**; it offers no standalone functionality, so it is activated automatically
  when a module that depends on it is enabled.

## Contributing to the Commerce menu

To add screens under the Commerce menu from another module:

1. Add a dependency on the `CrestApps.OrchardCore.Commerce` feature in the module manifest.
2. In an `AdminNavigationProvider`, add children under the existing `S["Commerce"]` node. Do not set the node
   identifier or icon again; the Commerce module owns them.

```csharp
builder
    .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
        .Add(S["My Screen"], S["My Screen"].PrefixPosition(), item => item
            .Action("Index", "Admin", "My.Module")
            .Permission(MyPermissions.ManageThings)
            .LocalNav()
        )
    );
```

## License

This project is licensed under the MIT License.
