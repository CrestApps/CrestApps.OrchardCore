---
sidebar_label: "Addresses and Inbound Routing Plan"
title: Omnichannel Addresses and Inbound Routing — Project Plan
description: Turn channel endpoints into a capability-based inventory of the addresses the business owns, and move every kind of inbound routing, for calls and texts, into inbound entry points.
---

# Omnichannel Addresses and Inbound Routing — Project Plan

## Why

Inbound handling for a number can be set in up to five places today, and the phone side and the SMS side never read
each other's settings:

| Where | Channel | What it decides |
| --- | --- | --- |
| Phone channel endpoint, Inbound routing card (`PhoneEndpointRoutingSettings`) | Phone | The entry point that answers the number. Asked first. |
| Entry point, Dialed numbers (`ContactCenterEntryPoint.DialedNumbers`, typed text) | Phone | The entry point answers calls to the numbers typed on it. |
| Queue, Inbound channel endpoint (`ActivityQueue.InboundChannelEndpointId`) | Phone | Fallback when no entry point matched. |
| Subject flow, Channel endpoint (`SubjectFlowSettings.ChannelEndpointId`) | Phone | Tags the call's campaign and subject only. It does not route. |
| SMS channel endpoint, Inbound routing card (`MessagingEndpointRoutingSettings`) | SMS | Agent or queue, shared pool or routed, auto-reply. The only SMS routing. |

A number used for calls and texts is two endpoint records, one per channel. Entry point numbers are free text and are
matched as exact text, so a number typed in another format never matches.

The contact-center products this follows (Genesys Cloud, Amazon Connect, Twilio Flex, Five9) split the same way:
an inventory of the numbers the business owns, and routing objects that pick numbers from it. The number carries no
routing of its own.

## Decisions

| | Decision |
| --- | --- |
| D-1 | Channel endpoints become **Omnichannel Addresses**: an inventory of the addresses the business owns. Each record is one address of one **address type** (phone number; email address later), with a checkbox list of **capabilities**. |
| D-2 | Capabilities are contributed by features, each against an address type: Voice and SMS for phone numbers today, Email for email addresses later. A capability's settings (an SMS provider, the agents who dial from a number) appear on the address only while that capability is ticked. |
| D-3 | All inbound routing moves to **inbound entry points**. An entry point serves **one channel**, Voice or SMS. A number with both capabilities is assigned to one Voice entry point and one SMS entry point. |
| D-4 | Entry points pick their numbers from the addresses that have the matching capability. A number belongs to at most one enabled entry point per channel. |
| D-5 | Migration merges the Phone and SMS records of the same number into one address with both capabilities, and repoints every reference to the retired record. |
| D-6 | A typed entry point number with no matching record becomes a new phone number address with Voice ticked, so every number keeps routing as before. |
| D-7 | The entry point catalog moves to a feature with no voice dependency, so an SMS-only tenant can use entry points without Contact Center Voice. Voice-only cards (welcome message, IVR menu, voicemail, ring timeout) stay in Inbound Voice; SMS-only cards (distribution, auto-reply) come from Messaging. |

## The address model

`OmnichannelChannelEndpoint` keeps its name and its storage, so ids and stored documents survive:

- `AddressType` (new): `PhoneNumber` today, `EmailAddress` later. Fixed after creation.
- `Capabilities` (new): the capabilities ticked on the address, using the existing channel names (`Phone`, `SMS`), so
  activities, subject flows and reports that store a channel name keep their meaning.
- `Value`: the address, canonicalized per address type (E.164 for phone numbers). Unique per address type.
- `Channel` (kept, read-only): the single channel of a record written before this change. The migration turns it
  into `AddressType` + `Capabilities`; nothing new reads it.
- `ProviderName` stays where it is and means the SMS provider: voice calls use the tenant's voice provider, so only
  texts need a per-number provider today. A per-capability provider can follow when a second one does.

`GetByServiceAddressAsync(channel, address)` keeps its signature and its callers: it finds the address with that value
which has the capability for that channel. Every inbound and outbound lookup works unchanged through it.

Features register capabilities in place of `AddChannelEndpointSource`:

```csharp
services.AddOmnichannelAddressCapability(OmnichannelAddressTypes.PhoneNumber, OmnichannelConstants.Channels.SMS, capability =>
{
    capability.DisplayName = S["SMS"];
    capability.Description = S["Send and receive text messages on this number."];
});
```

Pickers that list addresses (queue, batch, subject flow, workspace From, entry point numbers) filter by capability
instead of listing every record.

## Phases

### Phase 1 — Omnichannel Addresses (built)

- Model: `AddressType`, `Capabilities`; capability registry replacing channel endpoint sources.
- Editor: pick the address type when creating; capabilities as a checkbox list of the registered capabilities for that
  type; capability cards shown only while their capability is ticked (checked on the server too).
- Validation: one address per value per address type (today nothing stops duplicates, and lookup takes the first).
- Lookup by capability; pickers filtered by capability.
- Migration (one data migration, tolerant of partial runs):
  1. Every record gets `AddressType` and `Capabilities` from its `Channel`.
  2. Records with the same canonical value are merged into the oldest one. Their capability settings are combined
     (SMS provider and routing from the SMS record, outbound line agents from the Phone record).
  3. The retired records' identifiers are kept on the survivor (`MergedItemIds`), so activities and AI voice session
     summaries, which can number in the thousands, are not rewritten: lookups by id resolve them, and the one query
     that filters activities by address (the live automated activity on a number) matches every id.
  4. Configuration is repointed: activity batches and subject flows in the migration; a queue's
     `InboundChannelEndpointId` matches by any id and is rewritten the next time the queue is saved (the field is
     retired in phase 2).
  5. Retired records are removed.
- Recipes and deployment: export the new shape; import both the new shape and the old `Channel` shape.
- Menu: Interaction Center > Management > **Omnichannel Addresses**. List badges show the address type, the
  capabilities and the providers.
- Bugs fixed here: duplicates allowed; pickers listing every endpoint whatever its channel; every phone record counted
  as an own number whether or not it was used.

### Phase 2 — Channel-neutral entry points, and voice routing in one place (built)

- New feature, **Inbound Entry Points**, depending only on queues and addresses. It owns the entry point catalog,
  store, manager, index, recipes, deployment and the General, Routing and Hours cards.
- Entry points get a `Channel` (Voice or SMS) and `AddressIds` (numbers picked from addresses with that capability).
- Inbound Voice keeps the voice processor, IVR, voicemail and their cards, shown only on Voice entry points.
- The resolver finds the entry point by address, not by typed text, which also fixes the exact-text matching.
- Migration: typed `DialedNumbers` become `AddressIds` (D-6); a phone address's chosen entry point becomes that entry
  point's number; a queue's `InboundChannelEndpointId` becomes a Voice entry point targeting that queue when no entry
  point already serves the number.
- Removed: the phone address Inbound routing card (#741) and the typed Dialed numbers field. The queue's Inbound
  channel endpoint field is hidden while inbound entry points are enabled; a tenant running Voice without them keeps it,
  because it is that tenant's only per-number routing.
- Kept for older recipes: entry points that still list typed numbers route them, compared in E.164 form.

### Phase 3 — SMS entry points (built)

- SMS Messaging Channel registers **Text messages** as an entry point channel. The workspace adds its settings to an
  entry point whose channel is a messaging channel, stored as `MessagingEntryPointSettings` in the entry point's
  properties: queue distribution (shared pool or routed), auto-reply, and a closed auto-reply sent in its place outside
  business hours. Messages still route while closed, so they wait for the agents.
- The call-only settings moved off the shared cards onto the call entry point's own: queue priority, closed action,
  overflow queue and closed message.
- `IMessagingInboundRoutingResolver` finds the enabled entry point that answers the number on the conversation's
  channel. `MessagingConversationRouter` resolves it once per pass into `MessagingRoutingContext.Routing`, and the
  auto-reply, routed-queue, number and hand-off routers read it there in place of the address's routing settings.
- Migration (`MessagingEntryPointMigrations`): each number's `MessagingEndpointRoutingSettings` becomes an enabled text
  entry point named after the number (with " (SMS)" when the name is taken), unless another enabled text entry point
  already answers it; a number with no target gets none. The settings are removed from the number either way. The SMS
  portal import runs the same move after it copies the portal's routing onto the numbers.
- The Messaging workspace depends on Inbound Entry Points, so it brings Work Distribution's queues and the agents.
- Removed: the address Inbound routing card for texts (`MessagingEndpointRoutingDisplayDriver`).

### Phase 4 — Follow-ups

- Caller ID pickers (built): the dialer profile Caller ID and the Telnyx default outbound caller ID pick from the
  addresses used for voice calls (`GetCallerIdOptionsAsync`), still storing the number, so no migration is needed; a
  stored number that is not an address stays selected. Asterisk's outbound caller id stays free text, because Asterisk
  accepts caller identifiers that are not phone numbers.
- Inbound AI voice: an entry point that hands the call to an AI profile ("Answer calls at the front desk" has no
  inbound path today).
- Removed the unused `OmnichannelCampaign.ChannelEndpointId` (built). Older recipes that carry it still import; the
  value is ignored.

## Testing

Each phase ships with:

- unit tests for the model, validation, lookup and pickers;
- migration tests on a real SQLite database: merging, repointing, partial reruns, old-shape recipes;
- a feature activation pass for every feature combination, including SMS-only tenants;
- a UI pass on a copy of the dev tenant before a live check of a call and a text to a migrated number.
