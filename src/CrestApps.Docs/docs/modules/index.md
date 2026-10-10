---
sidebar_label: Overview
sidebar_position: 0
title: Standard Modules
description: Technical overview of the CrestApps standard modules that extend Orchard Core CMS, with their feature IDs, packages and links to the User Manual.
user_manual:
  - user-manual/administration/users
  - user-manual/administration/roles
  - user-manual/administration/content-access-control
  - user-manual/administration/content-fields
  - user-manual/administration/import-and-export
  - user-manual/administration/do-not-call-lists
  - user-manual/administration/phone-number-verification
  - user-manual/administration/time-zones
  - user-manual/reports
  - user-manual/report-builder/index
---

# Standard Modules

The standard modules extend Orchard Core CMS with user and role enhancements, content tools, compliance and
phone-number services, reporting, shared front-end resources and real-time infrastructure. The AI, Omnichannel,
Telephony and Contact Center suites build on them.

This section is for developers and IT: feature IDs, dependencies, configuration, recipes and extension points.

:::tip[Day-to-day use]
The administrators who use these features in the browser have their own guides in the User Manual's
**Site Administration** section: [Users](../user-manual/administration/users.md),
[Roles and Permissions](../user-manual/administration/roles.md),
[Restrict Content by Role](../user-manual/administration/content-access-control.md),
[Content Fields](../user-manual/administration/content-fields.md),
[Bulk Import and Export](../user-manual/administration/import-and-export.md),
[Do Not Call Lists](../user-manual/administration/do-not-call-lists.md),
[Phone Number Verification](../user-manual/administration/phone-number-verification.md),
[Time Zones](../user-manual/administration/time-zones.md), [Reports](../user-manual/reports.md) and the [Report Builder](../user-manual/report-builder/index.md).
:::

## Modules

| Module | Feature ID | What it adds | User Manual |
| --- | --- | --- | --- |
| [Users](users.md) | `CrestApps.OrchardCore.Users` (dependency only), `CrestApps.OrchardCore.Users.DisplayName`, `CrestApps.OrchardCore.Users.Avatars` | User caching, display names, avatars, the reusable `UserPicker` and the user search endpoint | [Users](../user-manual/administration/users.md) |
| [Roles](roles.md) | `CrestApps.OrchardCore.Roles` | `RolePickerPart` | [Roles and Permissions](../user-manual/administration/roles.md) |
| [Content Access Control](content-access-control.md) | `CrestApps.OrchardCore.ContentAccessControl` | Role-based view restrictions on content items | [Restrict Content by Role](../user-manual/administration/content-access-control.md) |
| [Content Fields](content-fields.md) | `CrestApps.OrchardCore.ContentFields` | `PhoneField` | [Content Fields](../user-manual/administration/content-fields.md) |
| [Content Transfer](content-transfer.md) | `CrestApps.OrchardCore.ContentTransfer`, `CrestApps.OrchardCore.ContentTransfer.OpenXml` | Bulk CSV and Excel import and export with pluggable handlers and file formats | [Bulk Import and Export](../user-manual/administration/import-and-export.md) |
| [DNC Registry](dnc-registry.md) | `CrestApps.OrchardCore.DncRegistry` and its `.UsaFtc`, `.CanadaDncl`, `.Local` and `.Azure` features | National and local do-not-call registries and import enforcement | [Do Not Call Lists](../user-manual/administration/do-not-call-lists.md) |
| [Phone Number Verifications](phone-number-verifications.md) | `CrestApps.OrchardCore.PhoneNumbers.Verifications` (dependency only) | Provider-agnostic verification, content-part storage, background revalidation, queue and report | [Phone Number Verification](../user-manual/administration/phone-number-verification.md) |
| [AbstractAPI provider](phone-number-verifications-abstractapi.md) | `CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi` | AbstractAPI Phone Validation provider | [Phone Number Verification](../user-manual/administration/phone-number-verification.md) |
| [Veriphone provider](phone-number-verifications-veriphone.md) | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Veriphone` | Veriphone provider | [Phone Number Verification](../user-manual/administration/phone-number-verification.md) |
| [Twilio provider](phone-number-verifications-twilio.md) | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio` | Twilio Lookup provider | [Phone Number Verification](../user-manual/administration/phone-number-verification.md) |
| [Recipes](recipes.md) | `CrestApps.OrchardCore.Recipes` | JSON-Schema support for Orchard Core recipes | |
| [Reports](reports.md) | `CrestApps.OrchardCore.Reports`, `CrestApps.OrchardCore.Reports.OpenXml` | Shared Reports area, extensible filters, uniform renderer and CSV / Excel exports | [Reports](../user-manual/reports.md) |
| [Report Builder](report-builder/index.md) | `CrestApps.OrchardCore.Reports.Builder` | Drag-and-drop report designer with pluggable data sources, joins, formulas, charts, pivots, reusable views and sharing | [Report Builder](../user-manual/report-builder/index.md) |
| [Resources](resources.md) | `CrestApps.OrchardCore.Resources` | Shared scripts, stylesheets and view components | |
| [SignalR compatibility](signalr.md) | `CrestApps.OrchardCore.SignalR` | Deprecated compatibility feature for the Orchard Core SignalR module | |
| [Time Zones](time-zones.md) | `CrestApps.OrchardCore.TimeZones` | Friendly named time zone maps that replace the Orchard Core time zone list | [Time Zones](../user-manual/administration/time-zones.md) |
| [WebSockets](websockets.md) | `CrestApps.OrchardCore.WebSockets` (dependency only) | Per-tenant WebSocket middleware and a swappable connection registry | |

The [Recipe Schemas](workflow-activity-schemas.md) pages in this section document the JSON schemas the Recipes
module contributes for workflow activities, rule conditions, sitemap sources, deployment steps, admin menu nodes,
query sources, URL rewrite rules and placement node filters.

Feature IDs for every module are also listed in the [Feature ID Reference](../feature-reference.md).

## Installation

You can install all CrestApps modules at once:

```bash
dotnet add package CrestApps.OrchardCore.Cms.Core.Targets
```

Or install individual modules as needed:

```bash
dotnet add package CrestApps.OrchardCore.Users
# etc.
```

After installation, enable the features under **Tools** -> **Features**, or with the `Feature` recipe step. Features
marked "dependency only" do not appear as a toggle; they are enabled automatically by the features that need them.
Settings that the modules read from `appsettings.json` and environment variables are listed on the
[Configuration](../configuration.md) page.
