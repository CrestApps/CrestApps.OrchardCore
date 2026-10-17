---
sidebar_label: Permissions and Security
title: Report Builder Permissions and Security
description: The Report Builder permissions, how designed reports and views are authorized per resource, the owner's-access model, share links and limits.
user_manual:
  - user-manual/report-builder/share-and-run
  - user-manual/report-builder/getting-started
---

The Report Builder authorizes designed reports and views the way Orchard Core authorizes content items: controllers always check a broad permission with the report or view as the **resource**, and an authorization handler grants that permission per resource to the owner and to the people the report is shared with. A saved report then reads its data with its **owner's** access.

## Permissions

The permissions are declared in `ReportDesignerPermissions` (`CrestApps.OrchardCore.Reports.Designer`) and offered by the Report Builder feature:

| Key | Name in the role editor | Implied by | Security critical |
| --- | --- | --- | --- |
| `ManageAllReportDesigns` | Manage all custom reports and views | | Yes |
| `ManageOwnReportDesigns` | Build reports and manage own custom reports and views | `ManageAllReportDesigns` | No |
| `ViewAllReportDesigns` | View all custom reports | `ManageAllReportDesigns` | No |
| `ShareReportsPublicly` | Share custom reports publicly and through share links | | Yes |

The `Administrator` stereotype gets all four. No other role gets any by default.

## How a report or view is authorized

Every action on an existing report or view is checked against one of two broad permissions, with the item as the resource:

- **`ManageAllReportDesigns`** to change it: edit, save a draft, publish, discard, list, preview and restore versions, delete, follow its real-time changes, refresh a view, and manage share links.
- **`ViewAllReportDesigns`** to run it: run, export, open it outside the admin, and clone it.

A role that holds the permission passes the check for every item. Otherwise `ReportDesignAuthorizationHandler` (`Designer/Handlers`) grants it for the one item:

| Resource | `ViewAllReportDesigns` (run, read) is granted to | `ManageAllReportDesigns` (change) is granted to |
| --- | --- | --- |
| A report (`ReportDesign`) | Its owner; the users named in `SharedUserNames`; members of the roles in `SharedRoles`; everyone, signed in or not, when `SharedRoles` holds `Anonymous`; every signed-in person when it holds `Authenticated`. | Its owner, when they hold `ManageOwnReportDesigns`. |
| A view (`ReportView`) | Every signed-in user who holds `ManageOwnReportDesigns`: views are shared building blocks for designers. | Its owner, when they hold `ManageOwnReportDesigns`. |

The owner is matched by user ID (`OwnerId` against the `NameIdentifier` claim); shared users are matched by user name, ignoring case.

Actions that do not concern one existing item check `ManageOwnReportDesigns` without a resource: opening the builder on a new report or view, creating a draft, the preview, the builder's JSON endpoints (data sources, data sets, schemas, functions, plan, user search), saving a new view, and the list of views.

| Action | Check |
| --- | --- |
| List reports (`/Admin/reports/designs`) | Lists each report that passes `ViewAllReportDesigns` for it; **Edit** and **Delete** show when it passes `ManageAllReportDesigns`. Unpublished drafts are listed for whoever passes `ManageAllReportDesigns` on them. |
| Create a report or view | `ManageOwnReportDesigns`. |
| Edit, save a draft, publish, discard, list or restore versions, delete a report | `ManageAllReportDesigns` on the report. |
| Run a report, in the admin or at `/reports/view/{id}` | `ViewAllReportDesigns` on the report. |
| Export a report | `ViewAllReportDesigns` on the report, and the report's `AllowExport`, unless the user also passes `ManageAllReportDesigns` on it. |
| Clone a report | `ViewAllReportDesigns` on the report and `ManageOwnReportDesigns`. The copy is owned by the person cloning and shared with nobody. |
| Share with the `Anonymous` role | `ShareReportsPublicly`, unless the report was already shared with `Anonymous`. |
| List, create or revoke share links | `ManageAllReportDesigns` on the report and `ShareReportsPublicly`. |
| Open a share link (`/reports/shared/{token}`) | The token only; with **Require sign-in**, a signed-in user. |
| Edit, save, delete or refresh a view | `ManageAllReportDesigns` on the view. |
| Use a view as a data set | `ViewAllReportDesigns` on the view. |

The admin menu follows the same rules: **Report Builder** for people who hold `ManageOwnReportDesigns`, **Shared Reports** for people who can run at least one report, **Report Views** for designers, and each report pinned to the menu for the people who pass `ViewAllReportDesigns` on it.

To check a permission for a report in your own code, authorize with the report as the resource:

```csharp
if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ViewAllReportDesigns, design))
{
    return Forbid();
}
```

## A saved report reads data with its owner's access

**A saved report reads data with its owner's current access**, whoever runs it. The owner vouches for what the report shows; viewers need no access to the underlying data. When the owner is deleted or disabled, or loses access to a data set, the report stops running with a message that asks someone to clone it and share the copy again. The builder preview reads with the designer's own access.

A scheduled view refreshes with its owner's access in the same way; see [Scheduled views](large-data.md#scheduled-views).

## Data sources are the security boundary

A data source must hide every data set the principal it is given may not read: `GetDataSetsAsync` lists only those, and `GetSchemaAsync` returns `null` for the others, so the engine refuses them. The principal is the designer while designing and the owner while a saved report runs. See [Add your own data source](custom-data-sources.md#security).

The **Content items** source lists a content type only when the principal holds `ViewContent` for it, including the type-specific permission of a securable type. Because Orchard Core grants `ViewContent` broadly by default, make content types that hold sensitive data **Securable** to control which report builders can report on them. Each built-in source's permission is listed in [Built-in data sources](data-sources.md).

## Viewers and filters

Exposed filter values from the query string override only filters marked as exposed. Fixed filters cannot be changed or removed by a viewer, and they are not shown to viewers.

## Share links

- Share link tokens hold 256 random bits. Only their SHA-256 hash is stored, and the full link is shown once.
- Links can expire, be revoked, require sign-in, and allow or deny export.
- Pages opened through a link send `X-Robots-Tag: noindex, nofollow` and `Referrer-Policy: no-referrer`.
- Every link opening, export, creation and revocation is logged.
- Deleting a report deletes its share links. Share links are never exported by the deployment step.

## Limits

Designer payloads are limited to 1 MB, formulas are nested at most 64 levels, views read other views at most 8 levels deep, and a view that reads itself is refused. The size of one run is limited by the settings in [Configuration](index.md#configuration).
