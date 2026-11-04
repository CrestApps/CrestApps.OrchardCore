---
sidebar_label: Outreach
title: Outreach
description: The outreach engine, the one bulk-send engine. A run sends to an audience one page at a time on the leased work queue, skips recipients a policy rules out (opt-outs, consent), holds text messages in quiet hours, retries a failed batch without sending twice, and reports its progress to the record it sends for. Messaging broadcasts and Marketing campaigns send through it.
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Outreach` (Outreach, enabled by the features that send through it) |
| **Core** | `CrestApps.OrchardCore.Outreach.Core` |
| **Abstractions** | `CrestApps.OrchardCore.Outreach.Abstractions` |
| **Category** | Communication |
| **Depends on** | Nothing. `OrchardCore.Email`, when enabled, adds the e-mail sender; Business Hours, when enabled, gives runs their sending hours |
| **Used by** | [Messaging broadcasts](../omnichannel/messaging-workspace.md#broadcasts-and-templates), [Marketing campaigns](marketing.md#campaigns) |

Every bulk send in the solution goes through one engine instead of each module looping over its own recipient list.
A module describes a send as an **outreach run** and plugs in what is specific to it; the engine pages the audience,
applies the policies, sends, retries and keeps count.

## A run

`OutreachRun` (collection `Outreach`, `OutreachRunIndex`) names:

| Part | Property | Who provides it |
| --- | --- | --- |
| Who it is for | `OwnerType`, `OwnerId` | The record the run sends for, such as a broadcast or a campaign |
| Who it goes to | `AudienceSource` | An `IOutreachAudienceSource`, which pages recipients with a cursor |
| How it is sent | `Sender`, `Channel`, `SenderAddress`, `ActorId` | An `IOutreachChannelSender` (`Email`, `Messaging`, …) |
| What is sent | `Template` (`Subject`, `Body`, `IsHtml`), `Renderer` | An `IOutreachTemplateRenderer`; without one the template is sent as it is (`Text`) |
| When it may send | `SendingHoursScheduleId` | A Business Hours schedule, for senders that observe quiet hours |
| How far it got | `Status`, `Cursor`, `BatchesCompleted`, `Sent`, `Skipped`, `Failed` | The engine |

`IOutreachEngine.StartAsync(run)` stores the run and queues its first batch in the caller's unit of work, so starting a
run is atomic with the change that asked for it. A run may start with counters and a cursor, which carries over
progress made before it existed. `CancelAsync(runId)` stops a run; batches still queued end without sending.

## How a run is sent

Each page of the audience is an `OutreachBatch` on the [leased work queue](commerce-extensions.md#the-rest-of-the-store)
(`ILeasedWorkQueue<T>`). The **Outreach** background task runs every minute and processes the due batches, each in its own
scope. A batch:

1. Is claimed for five minutes, so only one node sends it.
2. Waits without using an attempt when its run's sender, source or renderer is not enabled, or when the sender
   observes quiet hours (`IOutreachChannelSender.ObservesQuietHours`) and the run's sending hours are closed: the batch
   is due again when the schedule next opens.
3. Reads one page from the audience source at its cursor (`PageSize`, 50 by default).
4. For each recipient: skips it when the source marked it (`SkipReason`), it has no address, or an
   `IOutreachRecipientPolicy` gives a reason (opted out, no consent); otherwise renders the template and sends. The
   batch saves the recipient's key after each one, together with what the sender stored, so a retried batch never
   sends twice to the same recipient.
5. When the page is done, adds its counts to the run, queues the next page and notifies the
   `IOutreachRunObserver`s, all in the transaction that completes the batch. The run completes after the last page.

A sender throws for a transient failure. The batch is then retried with an exponential back-off (1 minute, doubling, up
to 30 minutes), and after five failed attempts the run fails with the error. A send the sender refuses is counted as
failed and the run carries on. Completed batches are removed 30 days later by the `OutreachBatches` retention policy;
the run keeps the totals.

## Extending it

Register the pieces in your feature's startup with the helpers from `CrestApps.OrchardCore.Outreach.Core`, and depend on
the `OutreachConstants.Feature` feature:

```csharp
services
    .AddOutreachAudienceSource<MyAudienceSource>()
    .AddOutreachChannelSender<MyChannelSender>()
    .AddOutreachTemplateRenderer<MyTemplateRenderer>()
    .AddOutreachRecipientPolicy<MyOptOutPolicy>()
    .AddOutreachRunObserver<MyRecordObserver>();
```

- An **audience source** returns `OutreachAudiencePage` (`Recipients`, `NextCursor`, `null` on the last page). Give each
  recipient a stable `Key` within the run.
- A **channel sender** returns `OutreachSendResult.Success()` or `Failure(error)`, and throws when the failure is
  transient. The e-mail sender (`EmailOutreachChannelSender`, name `Email`) adds a plain-text part to HTML messages.
- A **recipient policy** returns a skip reason (`OutreachConstants.SkipReasons`) or `null`. Policies decide
  do-not-contact questions; reuse `IContactOptOutResolver` for opt-outs rather than reading contact flags.
- A **run observer** copies the run's counters and status onto your record. It runs in the batch's transaction and
  must not commit.

## The two built-in senders of runs

| Record | Source | Sender | Renderer | Policies |
| --- | --- | --- | --- | --- |
| Messaging broadcast | `MessagingBroadcastRecipients`: the broadcast's recipient list, by position | `Messaging`: each recipient in their own 1:1 thread, quiet hours where the channel observes them | `Text` | Opt-outs through `IContactOptOutResolver` |
| Marketing campaign | `MarketingSegment`: the segment's customers, by document id; customers who don't accept marketing are skipped | `Email` | `MarketingCampaign`: the campaign's Liquid and the customer's unsubscribe link | |
