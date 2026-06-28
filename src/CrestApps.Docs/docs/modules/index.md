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
| [Content Fields](content-fields) | `CrestApps.OrchardCore.ContentFields` | Custom Orchard Core content field editors |
| [Content Transfer](content-transfer) | `CrestApps.OrchardCore.ContentTransfer` | Bulk Excel import and export for content items |
| [DNC Registry](dnc-registry) | `CrestApps.OrchardCore.DncRegistry` | National do-not-call registry integrations and import compliance settings |
| [Phone Number Verifications](phone-number-verifications) | `CrestApps.OrchardCore.PhoneNumbers.Verifications` | Provider-agnostic phone number verification with content-part storage, reporting, and background revalidation |
| [Phone Number Verifications - AbstractAPI](phone-number-verifications-abstractapi) | `CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi` | AbstractAPI provider for phone number verification |
| [Phone Number Verifications - Veriphone](phone-number-verifications-veriphone) | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Veriphone` | Veriphone provider for phone number verification |
| [Phone Number Verifications - Twilio](phone-number-verifications-twilio) | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio` | Twilio Lookup provider for phone number verification |
| [Recipes](recipes) | `CrestApps.OrchardCore.Recipes` | JSON-Schema support for Orchard Core recipes |
| [Resources](resources) | `CrestApps.OrchardCore.Resources` | Shared scripts and stylesheets |
| [Roles](roles) | `CrestApps.OrchardCore.Roles` | Enhanced role management with RolePickerPart |
| [SignalR](signalr) | `CrestApps.OrchardCore.SignalR` | Real-time communication via SignalR |
| [Time Zones](time-zones) | `CrestApps.OrchardCore.TimeZones` | Friendly named time zone maps and grouped time zone selection |
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
