---
name: crestapps-messaging-channel
description: >
  Skill for adding a new non-voice messaging channel (for example Email, WhatsApp, Facebook Messenger,
  Instagram, Telegram, web chat, Google Business Messages, or RCS) to the CrestApps.OrchardCore Omnichannel
  Messaging workspace, modeled on the reference SMS channel. Covers the IMessagingChannel contract, channel
  capabilities, address normalization, outbound send through a provider, inbound delivery into the workspace
  (webhook, durable provider inbox, IMessagingInboundProcessor), channel-only inbound rules
  (IMessagingInboundHandler), delivery receipts, opt-out, contact lookup, channel endpoints, the module/feature
  layout, DI registration, tests, and docs. Use this skill whenever the request is to "add a messaging
  channel", "add email/WhatsApp/Messenger to the messaging workspace", "integrate <messaging vendor> into the
  inbox", implement IMessagingChannel, or extend the Omnichannel Messaging workspace with a new channel.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Adding a Messaging Channel to the Omnichannel Messaging Workspace

The **Omnichannel Messaging workspace** (`CrestApps.OrchardCore.Omnichannel.Messaging`) is a channel-agnostic,
human-operated inbox. Agents see a list of customers and, beside it, one customer's conversation with a **tab per
channel**. The workspace itself owns *everything that is the same for every channel*:

- conversations, routing (agent/queue/shared pool/routed push), ownership, claim/transfer/close;
- first-response SLA, quiet hours (for channels that observe them), templates, broadcasts;
- outbound retries (the outbox), AI-to-human hand-off, permissions, the SignalR hub and the whole UI;
- storing messages in the shared `OmnichannelMessage` store and the customer view across channels.

A **channel** answers only what genuinely differs: how an address is written, how a message leaves, how a message
arrives, and how a contact is reached and opted out. **SMS** (`CrestApps.OrchardCore.Omnichannel.Messaging.Sms`) is
the reference channel — copy its shape.

> The user-facing reference is `src/CrestApps.Docs/docs/omnichannel/messaging-workspace.md` (section *Adding a
> channel*). Read it first, keep it in sync, and treat this skill as the build workflow.

## Golden rules

1. **A channel is a feature, never a workspace change.** Do not add `if (channel == "X")` anywhere in the workspace
   (`Omnichannel.Messaging*`). If the workspace needs to know something about a channel, it belongs on
   `IMessagingChannel` or `MessagingChannelCapabilities` — extend the contract (and the SMS channel) instead.
2. **The channel `Name` is permanent.** It is stored on every conversation, message and endpoint of the channel.
   Reuse `OmnichannelConstants.Channels.*` when one exists (`Email` does), otherwise add a constant there.
3. **The normalized address is the conversation key.** `NormalizeAddress` must map every spelling of one address to
   the same string (case, whitespace, punctuation, `mailto:`, `whatsapp:` prefixes…). Get this wrong and one
   customer forks into several threads.
4. **Inbound must be durable and idempotent.** Verify the provider signature, commit the delivery to the durable
   provider webhook inbox (keyed on the provider's message id), and only then process it. A redelivered webhook must
   not create a second message. Never drop a message silently; let the inbox retry.
5. **Respect opt-out everywhere.** `IsOptedOut` gates every agent send and every automated message (auto-reply).
   Compliance replies that must reach an opted-out contact (like SMS STOP confirmations) go straight to the
   provider from an `IMessagingInboundHandler`, not through the conversation service.
6. **Fail closed.** A capability you do not support is `false` in `MessagingChannelCapabilities`; the workspace then
   hides or refuses it. Never advertise media, subjects or broadcasts you cannot deliver.
7. **Test-driven.** Write the failing test first (see `AGENTS.md` → *Test-Driven Development*). Every piece of a
   channel is testable: the channel over a mocked provider seam, the webhook parser, the inbound handler.
8. **Docs and skill in the same change.** Update the docs page, the feature reference and the changelog; if you
   change the contract, update this skill.

## The reference implementation (study these)

| Concern | Reference files |
| --- | --- |
| The channel contract | `src/Core/CrestApps.OrchardCore.Omnichannel.Messaging.Core/Channels/IMessagingChannel.cs`, `MessagingChannelCapabilities.cs`, `MessagingOutboundMessage.cs` |
| Channel registration | `Channels/MessagingChannelServiceCollectionExtensions.cs` (`AddMessagingChannel<T>()`) |
| Channel-only inbound rules | `Channels/IMessagingInboundHandler.cs`, `MessagingInboundContext.cs` |
| Inbound pipeline (do not reimplement) | `Services/IMessagingInboundProcessor.cs`, `MessagingInboundProcessor.cs` |
| Send / receipts (do not reimplement) | `Services/IMessagingConversationService.cs` (`ApplyDeliveryReceiptAsync`) |
| **SMS channel** | `src/Core/CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Core/Services/SmsMessagingChannel.cs` |
| SMS provider seam | `Sms.Core/Services/ISmsDispatcher.cs`, `SmsDispatcher.cs`, `ISmsDispatchProvider.cs` |
| SMS inbound receiver | `Sms.Core/Services/SmsReceivedMessagingEventHandler.cs`, `SmsInboundInboxHandler.cs` |
| SMS compliance rules | `Sms.Core/Services/SmsKeywordInboundHandler.cs`, `SmsKeywordPolicy.cs` |
| SMS feature | `src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging.Sms/Manifest.cs`, `Startup.cs`, `Drivers/SmsEndpointProviderDisplayDriver.cs` |
| Provider webhook (signed, durable) | `src/Modules/CrestApps.OrchardCore.Telnyx/Endpoints/TelnyxSmsWebhookEndpoint.cs` |
| Tests to copy | `tests/CrestApps.OrchardCore.Tests/Omnichannel/Messaging/MessagingChannelWorkspaceTests.cs`, `SmsInboundProcessorTests.cs`, `MessagingTestChannels.cs` |

## Workflow

1. **Decide the name, capabilities and provider seam.** Read [references/channel-contract.md](references/channel-contract.md).
2. **Scaffold the projects and feature.** Follow [references/module-scaffold.md](references/module-scaffold.md): a
   `*.Core` project for testable logic and a module for the feature, depending on
   `MessagingConstants.Feature.Workspace`.
3. **Implement `IMessagingChannel`** and register it with `services.AddMessagingChannel<TChannel>()`.
4. **Register the channel as a channel-endpoint source** (`services.AddChannelEndpointSource(name, …)`) so tenants
   can add the addresses they send from, plus a `DisplayDriver<OmnichannelChannelEndpoint>` for per-endpoint
   provider settings if the channel has several providers. The workspace already normalizes and validates endpoint
   addresses through your channel and adds the inbound-routing editor to your endpoints.
5. **Wire inbound and delivery receipts.** Follow [references/inbound-and-delivery.md](references/inbound-and-delivery.md).
6. **Add channel-only rules** (keywords, consent windows, provider-specific replies) as `IMessagingInboundHandler`s.
7. **Test** (see the checklist below), build with `dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror`,
   and run the feature activation tests.
8. **Document**: docs page section, feature reference row, changelog entry. Update this skill if the contract moved.

## Done checklist

- [ ] Channel `Name` constant exists (reused from `OmnichannelConstants.Channels` or added there).
- [ ] `IMessagingChannel` implemented: identity, `Order`, capabilities, `NormalizeAddress`, `FormatAddress`,
      `IsValidAddress`, `SendAsync`, `IsOptedOut`, `GetContactAddresses`, `FindContactIdsAsync`,
      `SearchContactIdsByAddressAsync`.
- [ ] `AddMessagingChannel<T>()` and `AddChannelEndpointSource(...)` registered in the channel feature's startup.
- [ ] Inbound: signed webhook → durable provider inbox → `IMessagingInboundProcessor.ProcessAsync` with a normalized
      `OmnichannelMessage` (`Channel`, `ServiceAddress`, `CustomerAddress`, `Content`, `IsInbound = true`,
      `ProviderMessageId`, `CreatedUtc`).
- [ ] Delivery receipts call `IMessagingConversationService.ApplyDeliveryReceiptAsync` with `Channel` set, when the
      provider reports them (and `SupportsDeliveryReceipts = true`).
- [ ] Opt-out read in `IsOptedOut`; opt-out/opt-in set by the channel's own inbound rules when the channel has them.
- [ ] Feature manifest depends on the workspace feature (and the provider features it needs), category
      **Contact Center**.
- [ ] Projects added to `CrestApps.OrchardCore.slnx`, the CMS targets project, the test project, and the
      architecture scan lists (see module-scaffold).
- [ ] Tests: channel unit tests, inbound pipeline test through the real processor, webhook parser/signature tests,
      a feature activation test proving the channel appears in `IMessagingChannelResolver`.
- [ ] Public API baseline created for the new `*.Core` assembly.
- [ ] Docs + changelog updated; no workspace file mentions the new channel by name.
