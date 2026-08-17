---
sidebar_label: Commerce
sidebar_position: 13
title: Commerce
description: Registers the shared Commerce admin menu and its icon so every commerce-related module contributes its screens under a single, consistently branded top-level menu.
---

| | |
| --- | --- |
| **Feature Name** | Commerce |
| **Feature ID** | `CrestApps.OrchardCore.Commerce` |
| **Category** | Commerce |
| **Enabled** | By dependency only |

The **Commerce** module registers the shared **Commerce** top-level admin menu and its icon. Commerce-related modules — such as [Transactions](transactions) and [Taxation](taxation) — contribute their own screens under this single menu instead of each declaring their own copy of it.

## Why this module exists

Several modules add entries under a top-level **Commerce** menu. When each module declared the menu on its own, the menu icon appeared only when the specific module that carried it happened to be enabled, and it disappeared when that module was disabled. Multiple contributors to the same node also produced an inconsistent parent.

This module owns the top-level node, its identifier (`commerce`), and its icon, so the menu always renders consistently whenever any commerce-related feature is enabled.

## The feature

- **Commerce** (`CrestApps.OrchardCore.Commerce`) — Registers the Commerce admin menu and icon. The feature is **enabled by dependency only**; it offers no standalone functionality, so it is activated automatically when a module that depends on it is enabled and cannot be enabled on its own.

## Contributing to the Commerce menu

To add screens under the Commerce menu from another module:

1. Add a dependency on the `CrestApps.OrchardCore.Commerce` feature in the module manifest.

   ```csharp
   [assembly: Feature(
       Id = "My.Module",
       Category = "Commerce",
       Dependencies =
       [
           CommerceConstants.Features.Area,
       ]
   )]
   ```

2. In an `AdminNavigationProvider`, add children under the existing `S["Commerce"]` node. Do not set the node identifier or icon again; the Commerce module owns them.

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

`CommerceConstants.Features.Area` is exposed by `CrestApps.OrchardCore.Abstractions`, which commerce modules already reference.

## Enabling the feature

You do not enable **Commerce** directly. Enable a module that depends on it — for example [Transactions](transactions) or [Taxation](taxation) — and Commerce is enabled automatically.

## Related modules

- [Transactions](transactions) — a provider-agnostic ledger of outstanding obligations that contributes to the Commerce menu.
- [Taxation](taxation) — a provider-agnostic taxation framework that contributes to the Commerce menu.
