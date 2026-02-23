---
sidebar_label: Overview
sidebar_position: 0
title: Standard Modules
description: Overview of CrestApps standard modules that enhance Orchard Core CMS functionality.
---

# Standard Modules

CrestApps provides a set of standard modules that enhance core Orchard Core CMS functionality. These modules focus on user management, real-time communication, role-based access control, and shared resources.

## Available Modules

| Module | Feature ID | Description |
|--------|-----------|-------------|
| [Content Access Control](content-access-control) | `CrestApps.OrchardCore.ContentAccessControl` | Role-based content access restrictions |
| [Recipes](recipes) | `CrestApps.OrchardCore.Recipes` | JSON-Schema support for Orchard Core recipes |
| [Resources](resources) | `CrestApps.OrchardCore.Resources` | Shared scripts and stylesheets |
| [Roles](roles) | `CrestApps.OrchardCore.Roles` | Enhanced role management with RolePickerPart |
| [SignalR](signalr) | `CrestApps.OrchardCore.SignalR` | Real-time communication via SignalR |
| [Users](users) | `CrestApps.OrchardCore.Users` | Enhanced user management with display names and avatars |

## Installation

You can install all CrestApps modules at once:

```bash
dotnet add package CrestApps.OrchardCore.Cms.Core.Targets
```

Or install individual modules as needed:

```bash
dotnet add package CrestApps.OrchardCore.Users
dotnet add package CrestApps.OrchardCore.SignalR
# etc.
```

After installation, enable the desired features in the **Orchard Core Admin Dashboard** under **Tools > Features**.
