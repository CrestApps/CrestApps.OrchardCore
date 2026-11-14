---
sidebar_label: Timeline
title: Timeline
description: "The shared Timeline building block: comments, internal notes and events on any record (a RecordRef), the timeline service and entry handlers, access providers per record kind, the timeline card any module shows on its record pages, mentions through Orchard Core notifications, and the recipe and deployment step."
user_manual: user-manual/administration/timeline
---

**Timeline** keeps what is said and what happens on a record: comments, internal notes and events, newest first, with who wrote them, the field changes they record and their attachments. Any module can give its records a timeline. Work order notes, order notes and the notes of accounts and contacts live in it.

| Feature | Id | Depends on |
| --- | --- | --- |
| Timeline | `CrestApps.OrchardCore.Timeline` | nothing |

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Timeline.Abstractions` | `TimelineEntry`, `TimelineVisibility`, `TimelineFieldChange`, `TimelineQuery`, `TimelinePage`, `TimelineResult`, `TimelineComposerKind`, `TimelineOptions`, `TimelineEntryIds`, `ITimelineService`, `ITimelineEntryHandler`, `ITimelineAccessProvider`, `ITimelineAccessService`, `TimelineConstants`, `TimelinePermissions` and the registration helpers |
| `CrestApps.OrchardCore.Timeline.Core` | `TimelineEntryStore` and its index (collection `Timeline`), `DefaultTimelineService`, `DefaultTimelineAccessService` and `AddTimelineCore()` |
| `CrestApps.OrchardCore.Timeline` (module) | The `Timeline` view component, the add and redact endpoints, mentions, the migration and the recipe and deployment step |

## Entries

| Member | Meaning |
| --- | --- |
| `Subject` | The record, a `RecordRef` (`Kind:Id`) |
| `Kind` | An open string: `Comment`, `Note`, `Event`, or a kind a module adds, such as a `Reply` |
| `Visibility` | `Public` (everyone who may read the record, the customer included) or `Internal` |
| `Author` | A `PartyRef`; empty for the site. `AuthorName` keeps a name only for an author no party source knows, such as a guest who wrote by e-mail |
| `Body` | Plain text with line breaks |
| `Changes` | `{ Field, From, To }`, as people read them |
| `AttachmentIds` | The entry's attachments |
| `Channel` | Where it came in or went out, such as `Email` or `Portal`; empty for the admin |
| `ExternalId` | The id it has outside the site, such as an e-mail's message id (indexed, up to 255 characters) |
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

`AddAsync` refuses an entry without a record, a kind, or a body, change or attachment, one whose body is longer than 20,000 characters, and one whose record, kind, channel or author is longer than the 64 characters the index holds. `ITimelineEntryHandler`s (`AddTimelineEntryHandler<T>()`) may refuse an entry in `AddingAsync` and act on it in `AddedAsync`, `RedactedAsync`, `UpdatedAsync` and `DeletedAsync`. `ListAsync(TimelineQuery)` pages a record's entries newest first (entries of the same time in a fixed order), with or without internal entries and for some kinds only.

Other paths:

- **`ImportAsync`** brings in history that happened elsewhere, such as notes an older version kept on a record. It checks and stamps an entry like `AddAsync` but keeps a long body whole and tells no handler, so nothing is announced. An entry whose id is already stored is left as it is: give the entry an id made from where it came from with `TimelineEntryIds.FromSource(...)`, and importing again adds nothing.
- **`UpdateAsync(entryId, change)`** saves a change that doesn't rewrite what an entry says: the `change` callback gets a `TimelineEntryChanges` with only the entry's properties (a delivery status a channel keeps), its channel and the `ExternalId` it was sent under; `FindByExternalIdAsync` finds the entry again. Entries read through `FindAsync`, `FindByExternalIdAsync` and `ListAsync` are copies, so changing one never reaches the stored entry.
- **`DeleteAllAsync(subject)`** removes a deleted record's timeline and tells the handlers of each entry, so its files go too. A content item's timeline is deleted when the item is removed for good.

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

Content items (the record kind `Content`, such as accounts, contacts and leads) come with a provider. Staff, who read and write internal entries, are the people who may edit the item itself (so a rule that decides per item, such as content access control, is honored) and every item of the type: owning an item isn't enough, so on a type its owners may edit, an owner doesn't read the staff's notes. Public entries are read by everyone who may view the item, which on many sites is every registered user, so a content type's timeline takes them only when a module opts the type in:

```csharp
services.AllowPublicTimelineEntries("Project");
```

A visitor who isn't signed in reads nothing, even on a site where anyone may view content. The work order and order providers let an entry go only on a record that exists.

`RedactTimelineEntries` (**Redact timeline entries**) lets a user redact an entry on any record they may read; administrators have it by default.

## The timeline card

A module shows a record's timeline on its own page by invoking the view component by name:

```cshtml
@await Component.InvokeAsync("Timeline", new { subject = $"WorkOrder:{Model.WorkOrder.ItemId}", title = T["Notes"].Value })
```

The card renders nothing when the user may not read the timeline. It shows the entries the user may read, 50 at a time with **Older** and **Newer** links, with the author's current name from the party directory, the time in the user's time zone, the field changes and an **Internal** badge. Its composer offers one button for each kind in `TimelineOptions.ComposerKinds` the access provider lets the user add: **Add note** (an internal `Note`) and **Add comment** (a public `Comment`) by default. The composer posts to `Admin/timeline/add` and comes back to the page, with the tenant's path base; a refused file or entry keeps what was typed. **Redact** asks for confirmation, then posts to `Admin/timeline/redact/{id}`. Optional arguments are `returnUrl` and `kinds` (comma-separated kinds to show).

A module adds a button for its own kind of entry:

```csharp
services.AddTimelineComposerKind(new TimelineComposerKind
{
    Kind = "Reply",
    Visibility = TimelineVisibility.Public,
    ButtonText = LocalizationSource.Create<Startup>("Reply"),
    SubjectKinds = { "Inspection" },
});
```

The composer is the `TimelineComposer` shape, with the alternate `TimelineComposer__{Kind}` for one record kind. A module that needs more than a button, such as a reply that also changes the record's status, overrides it for its records and posts to its own action.

## Mentions

With Orchard Core **Notifications** enabled, a user mentioned as `@username` in an entry gets a notification with a link to the record, unless they may not read the entry. The notification doesn't carry the entry's text, so a body redacted later isn't kept in it. An e-mail address is not a mention.

## Data set and erasure

With Data Pipelines Core on, the **Timeline** data source has a `TimelineEntries` data set (one row per entry: its record, kind, visibility, channel, text, author, attachments and changes count) for the Report Builder and data pipelines. Each record kind's access provider decides which rows a reader gets, so an entry of a record the reader can't see, or of a kind with no provider, is never shown.

For a request to erase a person's data, `ITimelineService.ScrubAsync(subject, scrub)` rewrites the body, the author's name and the recorded change values of every entry on a record through the function given, such as `PersonalDataAnonymizeContext.Scrub`. Nothing else of an entry changes. Tickets uses it for the tickets a person asked for.

## Recipe and deployment step

The `TimelineEntries` step adds entries through the import path, such as history brought from another system: each is checked and stamped, nothing is announced, and an entry whose `ItemId` is already stored is left as it is.

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

The **Timeline entries** deployment step exports every entry of the record kinds it names (`SubjectKinds`, such as `WorkOrder,Order`), oldest first. Both have schemas for CrestApps Recipes.
