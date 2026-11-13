---
sidebar_label: Timeline
title: Timeline
description: "The shared Timeline building block: comments, internal notes and events on any record (a RecordRef), the timeline service and entry handlers, access providers per record kind, the timeline card any module shows on its record pages, mentions through Orchard Core notifications, and the recipe and deployment step."
user_manual: user-manual/administration/timeline
---

**Timeline** keeps what is said and what happens on a record: comments, internal notes and events, newest first, with who wrote them, the field changes they record and their attachments. Any module can give its records a timeline. Work order notes and order notes live in it, and ticket replies will too.

| Feature | Id | Depends on |
| --- | --- | --- |
| Timeline | `CrestApps.OrchardCore.Timeline` | nothing |

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Timeline.Abstractions` | `TimelineEntry`, `TimelineVisibility`, `TimelineFieldChange`, `TimelineQuery`, `TimelinePage`, `TimelineResult`, `ITimelineService`, `ITimelineEntryHandler`, `ITimelineAccessProvider`, `ITimelineAccessService`, `TimelineConstants`, `TimelinePermissions` and the registration helpers |
| `CrestApps.OrchardCore.Timeline.Core` | `TimelineEntryStore` and its index (collection `Timeline`), `DefaultTimelineService`, `DefaultTimelineAccessService` and `AddTimelineCore()` |
| `CrestApps.OrchardCore.Timeline` (module) | The `Timeline` view component, the add and redact endpoints, mentions, the migration and the recipe and deployment step |

## Entries

| Member | Meaning |
| --- | --- |
| `Subject` | The record, a `RecordRef` (`Kind:Id`) |
| `Kind` | An open string: `Comment`, `Note`, `Event`, or a kind a module adds, such as a ticket's `Reply` |
| `Visibility` | `Public` (everyone who may read the record, the customer included) or `Internal` |
| `Author` | A `PartyRef`; empty for the site. `AuthorName` keeps a name only for an author no party source knows, such as a guest who wrote by e-mail |
| `Body` | Plain text with line breaks |
| `Changes` | `{ Field, From, To }`, as people read them |
| `AttachmentIds` | The entry's attachments |
| `Channel` | Where it came in or went out, such as `Email` or `Portal`; empty for the admin |
| `RedactedUtc`, `RedactedBy` | Set when the body was redacted |

Entries are kept as written. A body can only be redacted (`ITimelineService.RedactAsync`), which keeps the entry and its changes; the timeline card's **Redact** also deletes the entry's files.

## Writing to a timeline

```csharp
await timelineService.AddAsync(new TimelineEntry
{
    Subject = new RecordRef(WorkOrdersConstants.RecordKind, workOrder.ItemId),
    Kind = TimelineConstants.Kinds.Event,
    Visibility = TimelineVisibility.Internal,
    Author = new PartyRef(PartyKinds.User, userId),
    Changes = [new TimelineFieldChange { Field = "Status", From = "New", To = "In progress" }],
});
```

`AddAsync` refuses an entry without a record, a kind, or a body, change or attachment, and one whose body is longer than 20,000 characters. `ITimelineEntryHandler`s (`AddTimelineEntryHandler<T>()`) may refuse an entry in `AddingAsync` and act on it in `AddedAsync` and `RedactedAsync`. `ListAsync(TimelineQuery)` pages a record's entries newest first, with or without internal entries and for some kinds only. `DeleteAllAsync(subject)` removes a deleted record's timeline.

## Access

`ITimelineService` doesn't check who may read or write; a page that acts for a user asks `ITimelineAccessService`, which asks the `ITimelineAccessProvider` registered for the record's kind. A kind without a provider is refused, so a record's timeline is never shown by mistake.

```csharp
public sealed class WorkOrderTimelineAccess : ITimelineAccessProvider
{
    public string SubjectKind => WorkOrdersConstants.RecordKind;

    public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default) => ...;

    public Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default) => ...;
}

services.AddTimelineAccessProvider<WorkOrderTimelineAccess>();
```

Content items (the record kind `Content`, such as accounts, contacts and leads) come with a provider: internal entries are for people who may edit the item, public ones for signed-in people who may view it, and a visitor who isn't signed in reads nothing, even on a site where anyone may view content.

`RedactTimelineEntries` (**Redact timeline entries**) lets a user redact an entry on any record they may read; administrators have it by default.

## The timeline card

A module shows a record's timeline on its own page by invoking the view component by name:

```cshtml
@await Component.InvokeAsync("Timeline", new { subject = $"WorkOrder:{Model.WorkOrder.ItemId}", title = T["Notes"].Value })
```

The card renders nothing when the user may not read the timeline. It shows the entries the user may read, with the author's current name from the party directory, the time in the user's time zone, the field changes and an **Internal** badge, and a composer with **Add note** (an internal `Note`) and **Add comment** (a public `Comment`) as the access provider allows. The composer posts to `Admin/timeline/add` and comes back to the page; **Redact** posts to `Admin/timeline/redact/{id}`. Optional arguments are `returnUrl` and `kinds` (comma-separated kinds to show).

## Mentions

With Orchard Core **Notifications** enabled, a user mentioned as `@username` in an entry gets a notification with a link to the record, unless they may not read the entry. An e-mail address is not a mention.

## Recipe and deployment step

The `TimelineEntries` step adds entries, such as history brought from another system. An entry whose `ItemId` is already stored is left as it is.

```json
{
  "name": "TimelineEntries",
  "Entries": [
    {
      "Subject": "WorkOrder:4abc0000000000000000000000",
      "Kind": "Note",
      "Visibility": "Internal",
      "Author": { "Kind": "User", "Id": "4user000000000000000000000" },
      "Body": "Customer asked for a morning visit.",
      "CreatedUtc": "2026-11-02T14:30:00Z"
    }
  ]
}
```

The **Timeline entries** deployment step exports the entries of the record kinds it names (`SubjectKinds`, such as `WorkOrder,Order`), oldest first. Both have schemas for CrestApps Recipes.
