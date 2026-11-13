# Timeline

Comments, internal notes and events on any record (a work order, an order, an account, a ticket), newest first, with who wrote them, the field changes they record and their attachments.

## Feature

| Feature | Id |
| --- | --- |
| Timeline | `CrestApps.OrchardCore.Timeline` |

## Using it from another module

1. Reference `CrestApps.OrchardCore.Timeline.Abstractions` and depend on the feature (or gate the integration with `[RequireFeatures(TimelineConstants.Features.Timeline)]`).
2. Register who may read and write your records' timelines: `services.AddTimelineAccessProvider<MyAccess>()`. A record kind without a provider can't be read or written through the shared pages.
3. Write entries with `ITimelineService.AddAsync` (a `RecordRef` subject, a kind such as `Note` or `Event`, a visibility, an author `PartyRef`).
4. Show the timeline on your record page: `@await Component.InvokeAsync("Timeline", new { subject = "Kind:Id" })`.

Content items are the record kind `Content`; their timelines follow the item's View and Edit permissions.

## Recipe step

`TimelineEntries` adds entries (an entry whose `ItemId` is stored is left alone). The **Timeline entries** deployment step exports the entries of the record kinds it names.

## Permissions

`RedactTimelineEntries` lets a user redact the text of an entry on a record they may read.

## Documentation

- Technical manual: `src/CrestApps.Docs/docs/modules/timeline.md`
- User manual: `src/CrestApps.Docs/docs/user-manual/administration/timeline.md`
