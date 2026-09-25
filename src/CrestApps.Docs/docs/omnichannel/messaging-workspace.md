---
sidebar_label: Messaging Workspace
sidebar_position: 4
title: Omnichannel Messaging Workspace
description: A human-operated, channel-agnostic messaging inbox for Orchard Core. SMS is the first channel; email, WhatsApp, Messenger and others plug in as further channel features.
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel Messaging Workspace |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Messaging` |
| **Channel features** | `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` (SMS Messaging Channel) |
| **Optional add-on** | `CrestApps.OrchardCore.Omnichannel.Messaging.RoutedDistribution` |
| **Category** | Contact Center |
| **Admin menu** | **Messaging** |

The **Messaging Workspace** is a **human-operated** inbox for every non-voice channel. Agents use one screen to send and receive on any channel for the same customer. Picture a list of customers on the left and the selected customer's conversation on the right, with a row of channel icons above it. Switching icons keeps the same view; it only changes which channel messages are read from and sent to.

The workspace itself carries **no channel**. Each channel is a separate feature that plugs into it:

| Channel | Feature | Status |
| --- | --- | --- |
| SMS | **SMS Messaging Channel** (`CrestApps.OrchardCore.Omnichannel.Messaging.Sms`) | Available |
| Email, WhatsApp, Facebook Messenger, Telegram, … | Future channel features | Build one with the channel contract below |

It is the human counterpart to [SMS Automation](sms), which lets an **AI agent** carry an SMS conversation on its own. Both can run on the same numbers. An automated activity handles a conversation until it hands off, and from then on the workspace owns the thread.

## The workspace

- **Customer list.** Every customer with a conversation, most recent first. A customer who wrote on several channels appears **once**, with the unread messages of all their channels added together and an icon for each channel they used. Filter by *All*, *Mine* or *Unassigned*, and by channel when more than one is enabled.
- **Channel tabs.** Above the open conversation, one icon per enabled channel:
  - the channel on screen is highlighted;
  - a channel with unread messages carries a **red badge** with the count, updated live;
  - a channel where the customer has an address but no conversation yet opens the composer on that channel;
  - a channel where the customer cannot be reached is greyed out.
- **Conversation.** The thread, the composer (canned-response templates, Enter to send), and the customer card with every contact record that matches the address.
- **Claim, assign, transfer, close, spam, reopen.** The same on every channel.
- **Broadcasts.** One message to many recipients as individual 1:1 threads (not a group chat), on any channel that supports them.
- **Real time.** New messages, delivery receipts and assignment changes are pushed over the workspace's own SignalR hub. A new message for the open conversation is appended, one for the same customer on another channel badges that channel's tab, and any new message moves its customer to the top of the list.

## Enable the features

1. Go to **Tools → Features**.
2. Enable **SMS Messaging Channel**. This also enables the **Omnichannel Messaging Workspace** and everything it needs (Channel Endpoints, Contact Center Agent Services, SignalR, Orchard Core SMS).
3. Optionally enable **Omnichannel Messaging Routed Distribution** to push department conversations to the least-loaded available agent. This needs Contact Center Work Distribution.

Enabling the workspace on its own gives you the inbox with nothing to send or receive on. That is expected: channels come from channel features.

### Dependencies (and why they are minimal)

| Dependency | Why |
| --- | --- |
| **Omnichannel Channel Endpoints** | Every address you send from (an SMS number today; a mailbox or WhatsApp number later) is a [channel endpoint](management#channel-endpoint). It carries the provider and the inbound routing. |
| **Contact Center Agent Services** (dependency-only) | Operators are Contact Center **agent profiles**. A bare profile is created automatically the first time a permitted user opens the workspace, so no Contact Center administration is required. |
| **Orchard Core SignalR** | The workspace's own real-time hub. |
| **Orchard Core SMS** *(SMS channel only)* | The SMS provider abstraction and the tenant default provider. |

The workspace does **not** require Contact Center Voice, Work Distribution or the Agents administration, and it does not pull in the Omnichannel Management CRM.

## Setting up SMS

1. **Configure an SMS provider.** Enable at least one, for example [Telnyx SMS](../telephony/telnyx#telnyx-sms) or Twilio, and pick the tenant **default provider** at **Settings → SMS**.
2. **Add your numbers as SMS channel endpoints** in **Interaction Center → Channel Endpoints**. Each SMS endpoint can pin the **provider** that owns the number; leave it empty to use the tenant default.
3. **Set the inbound routing** on the endpoint:
   - **Target**: an **agent** (personal number) or a **queue** (department number).
   - **Distribution mode**: **Shared pool** (agents claim conversations) or **Routed** (pushed to an agent by the routed-distribution feature).
   - **Auto-reply**: an optional acknowledgement, sent at most once a day per conversation.
4. **Grant the permissions** below to the roles that staff the inbox.
5. **Point the provider webhook at Orchard Core** so inbound messages and delivery receipts arrive (for example the [Telnyx SMS webhook](../telephony/telnyx#telnyx-sms)).
6. Open **Messaging → Inbox**.

A **Send SMS** button appears beside phone-number fields on admin pages. It opens the customer's existing SMS conversation, or the composer when there is none.

### SMS compliance

The SMS channel applies the carrier keywords, whatever the workspace is doing:

- **STOP** (and its synonyms) closes the conversation, sets the contact's **Do not SMS** flag and sends one confirmation.
- **START** reverses an opt-out and confirms. It is ignored when the contact is not opted out, so an ordinary "Yes" stays part of the conversation.
- **HELP** says who is texting.

A keyword silences the endpoint's auto-reply for that message. Replies can be customised under the `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies` configuration section (`StopMessage`, `HelpMessage`, `StartMessage`). SMS also observes **quiet hours**: outside the destination queue's business hours, in the contact's local time, the composer warns before sending.

## Permissions

| Permission | Grants |
| --- | --- |
| `UseMessagingWorkspace` | Use the workspace on the endpoints you own or serve. |
| `ViewAllMessagingConversations` | See every conversation (supervisors), and transfer them. |
| `SendGroupMessages` | Send broadcasts and multi-recipient messages. |
| `SendMessagesDuringQuietHours` | Send outside business hours on a channel that observes quiet hours without being told off. |
| `ManageMessaging` | Manage templates and endpoint routing. |

Permissions apply to every channel; there is no per-channel permission.

## Configuration

| Section | Settings |
| --- | --- |
| `CrestApps:Omnichannel:Messaging` | `InboxPageSize`, `ConversationLockTimeoutSeconds`, `ConversationLockExpirationSeconds`, `OutboxBatchSize`, `MaxMessagesPerPassPerEndpoint` |
| `CrestApps:Omnichannel:Messaging:RoutedDistribution` | Routed (push) distribution tunables |
| `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies` | `StopMessage`, `HelpMessage`, `StartMessage` |

## Adding a channel

A channel is a feature that implements `IMessagingChannel` (in `CrestApps.OrchardCore.Omnichannel.Messaging.Core`) and registers it:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMessagingChannel<WhatsAppMessagingChannel>();

        // Let tenants add WhatsApp numbers as channel endpoints.
        services.AddChannelEndpointSource("WhatsApp", source =>
        {
            source.DisplayName = S["WhatsApp"];
        });
    }
}
```

The channel answers only what differs between channels:

| Member | Purpose |
| --- | --- |
| `Name`, `DisplayName`, `IconCssClass`, `Order` | Identity. `Name` is stored on every conversation, message and endpoint of the channel, so it must never change. |
| `Capabilities` | Subject line, media, delivery receipts, broadcasts, quiet hours, maximum body length. The composer and services adapt to them. |
| `NormalizeAddress`, `FormatAddress`, `IsValidAddress` | How an address is stored, shown and validated. The normalized address is the conversation key. |
| `SendAsync` | Hand one outbound message to the provider serving the sending endpoint. |
| `IsOptedOut`, `GetContactAddresses`, `FindContactIdsAsync`, `SearchContactIdsByAddressAsync` | How a CRM contact is reached, recognised and opted out on the channel. |

Inbound traffic reaches the workspace when the channel's receiver (a webhook, an event handler) calls `IMessagingInboundProcessor.ProcessAsync` with a normalized `OmnichannelMessage` whose `Channel` is the channel's name. The workspace then finds or creates the conversation, routes it, starts the first-response clock, stores the message and notifies the inbox. Rules that belong to one channel only (as the carrier keywords belong to SMS) go in an `IMessagingInboundHandler`. Delivery receipts go to `IMessagingConversationService.ApplyDeliveryReceiptAsync`, naming the channel.

Everything else — routing, ownership, SLA, templates, broadcasts, retries, AI hand-off, permissions and the UI — is shared, so a new channel gets all of it without writing any.

## Upgrading from the SMS Portal

The SMS-only **SMS Portal** feature (`CrestApps.OrchardCore.Omnichannel.Sms.Portal`) has been replaced by the workspace and the SMS channel. To move a tenant across:

1. Enable **SMS Messaging Channel**. The old feature no longer exists, so it drops off the tenant on its own.
2. The first time the workspace is enabled, its migration imports what the portal stored: conversations (with their message history, which already lives in the shared message store), templates, broadcasts, the inbound routing of each SMS endpoint, and the portal permissions granted to roles (each role gets the workspace permission that replaces it).
3. Update any appsettings entries: `CrestApps:Sms:Workspace` is now `CrestApps:Omnichannel:Messaging`, `CrestApps:Sms:RoutedDistribution` is now `CrestApps:Omnichannel:Messaging:RoutedDistribution`, and `CrestApps:Sms:Portal:KeywordReplies` is now `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies`.
4. Agents turn their **Available** toggle back on. The routed-assignment availability was stored under the portal's name and is not carried over.
