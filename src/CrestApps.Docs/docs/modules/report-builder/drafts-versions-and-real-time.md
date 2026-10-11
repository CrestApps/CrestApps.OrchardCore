---
sidebar_label: Drafts, Versions and Real Time
title: Drafts, Versions and Real-Time Editing
description: How the Report Builder autosaves drafts, detects conflicting changes, publishes immutable versions, and shows who else is editing through SignalR.
user_manual:
  - user-manual/report-builder/publish-and-versions
---

A designed report has a published version, which is what people run, and a **draft**, which the builder saves into a moment after each change. Nobody sees the draft until someone publishes it. How people use drafts and versions is described in the User Manual: [Publish and versions](../../user-manual/report-builder/publish-and-versions.md).

## Drafts and revisions

- **Autosave.** Each change is sent to `POST /Admin/reports/builder/{id}/draft` with the revision the page has, a moment after it is made. The first change to a new report creates its draft (`POST /Admin/reports/builder/drafts`), which gets the report's identifier at once and keeps it when first published; until then the report exists only as that draft, is listed under **Not published yet** for its owner and for people who manage every report, and can be continued or deleted (`POST /Admin/reports/designs/drafts/{id}/delete`). Saves are sent with `keepalive`, so a change made right before the page is closed or refreshed is still saved, and the page does not ask before leaving.
- **Revisions.** Every draft save, publish, discard and restore increases the report's revision, kept in its `ReportDesignDraft` document. A change based on an older revision is refused with `409 Conflict` and the name of the person who changed it last; the page offers to reload their changes or to keep its own (which sends the change again with `force`). Changes to one report are serialized with Orchard Core's `IDistributedLock`, so the check holds on several nodes; a change that cannot take the lock in time is refused and the page asks to try again.
- **Discarding** (`POST /Admin/reports/builder/{id}/draft/discard`) drops the draft and returns to the published version.

## Publishing and versions

- **Publishing** (`POST /Admin/reports/builder/save`) saves the report as before (title, sharing and data rules are checked), adds an immutable `ReportDesignVersion` only when the report's content differs from the latest version, and clears the draft. A report published before versions existed first gets its earlier state as version 1.
- **Versions** can be listed (`GET /Admin/reports/builder/{id}/versions`), previewed (`GET /Admin/reports/builder/{id}/versions/{number}`) and **restored** (`POST /Admin/reports/builder/{id}/versions/{number}/restore`): restoring copies a version into the draft, to be checked and published; the new version records which version it was restored from.
- Drafts and versions are separate YesSql documents with their own index tables (`ReportDesignDraftIndex`, `ReportDesignVersionIndex`), so autosaving one report never rewrites another. Deleting a report deletes its draft, its versions and its share links.
- **Retention.** At most `MaxVersions` versions are kept per report; the oldest are deleted when a report is published, and `0` keeps them all (see [Configuration](index.md#configuration)).

Every one of these endpoints checks `ManageAllReportDesigns` with the report as the resource (see [Permissions](permissions.md)). Reusable views have no drafts or versions: they are saved directly with `POST /Admin/reports/views/save`.

## Real time

When `OrchardCore.SignalR` is enabled, the builder connects to `ReportsHub` (at `/Communication/Hub/ReportsHub` under the tenant's path). No separate feature is needed: the hub is registered by a startup class marked with `[RequireFeatures("OrchardCore.SignalR")]`.

- **Presence.** A page subscribes to the report it edits; the hub checks that the user may edit it. Pages tell each other who arrived and left, and the builder shows the others' initials and a notice that changes can conflict. The server only relays these messages and keeps no list, so it works behind the Redis or Azure SignalR backplane.
- **Changes.** After a draft save, publish, discard, restore or delete is committed, `SignalRReportDesignNotifier` sends `ReportDesignChanged` (`kind`, `designId`, `revision`, `versionNumber`, `userName`) to the report's group. A page that is behind offers to reload. Group names are qualified by tenant with `TenantSignalRGroupName`.
- Without SignalR, the revision check still prevents silent overwrites; people find out when their next save is refused.

Other modules can react to report changes by replacing `IReportDesignNotifier`.
