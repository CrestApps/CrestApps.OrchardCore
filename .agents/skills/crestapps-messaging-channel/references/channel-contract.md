# The `IMessagingChannel` contract

Namespace `CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels`
(project `src/Core/CrestApps.OrchardCore.Omnichannel.Messaging.Core`). Every member is required; there are no
optional interfaces. The reference is `SmsMessagingChannel`.

| Member | What the workspace uses it for | SMS reference | Notes for a new channel |
| --- | --- | --- | --- |
| `string Name` | Stored on every conversation, message and endpoint; matched against `OmnichannelMessage.Channel` and `OmnichannelChannelEndpoint.Channel`. | `OmnichannelConstants.Channels.Sms` (`"SMS"`) | Permanent. Reuse `OmnichannelConstants.Channels.Email` for email. |
| `LocalizedString DisplayName` | Tab tooltip, composer labels, validation messages, broadcasts list. | `S["SMS"]` | Localize through `IStringLocalizer<TChannel>`. |
| `string IconCssClass` | The channel tab icon, the list row icon, the composer badge. | `fa-solid fa-comment-sms` | Font Awesome classes; brand icons are `fa-brands fa-whatsapp`, `fa-brands fa-facebook-messenger`, etc. Email: `fa-solid fa-envelope`. |
| `int Order` | Tab order (lowest first). | `0` | Pick a gap (10, 20…) so channels can be slotted in. |
| `MessagingChannelCapabilities Capabilities` | Composer adaptation and service rules. | Delivery receipts, broadcast, quiet hours. | See the table below. Return a static instance. |
| `string NormalizeAddress(string)` | The conversation key; applied to inbound addresses, composer recipients, endpoint values (through the workspace's `IChannelEndpointAddressPolicy`) and delivery receipts. | Strips whitespace (endpoints are also canonicalized to E.164 by the phone service). | Must be idempotent and return `null` for empty input. Email: trim + lower-case + strip `mailto:`. WhatsApp: E.164 without `whatsapp:` prefix. Messenger: the page-scoped user id as-is. |
| `string FormatAddress(string)` | Display everywhere an address is shown. | `PhoneDisplayFormatter.Format` | Return the input when there is nothing to format. |
| `bool IsValidAddress(string)` | Composer and broadcast validation; endpoint validation for channels the Omnichannel endpoint handler does not already validate. | 7–15 digits, optional leading `+`. | Keep it permissive enough for real addresses; the provider is the authority on reachability. |
| `Task<MessageDispatchResult> SendAsync(MessagingOutboundMessage, CancellationToken)` | Every outbound: agent replies, new conversations, broadcasts, auto-replies, outbox retries. | Routes through `ISmsDispatcher` to the provider pinned on the sending number. | **Never throw** for a provider refusal — return `MessageDispatchResult.Failed(...)`; the workspace schedules retries from it. Return `Success(providerMessageId)` so delivery receipts can match exactly. Honour `Subject` / `MediaUrls` only when the capability says so. |
| `bool IsOptedOut(ContentItem contact)` | Refuses agent sends and suppresses automation. | `OmnichannelContactPart.DoNotSms`. | Read the flag that means "do not contact on this channel" (email: `OmnichannelContactPart.DoNotEmail`; update it with `SetDoNotEmail`). Must be side-effect free (`TryGet`, not `As`). |
| `IReadOnlyList<string> GetContactAddresses(ContentItem contact)` | Channel tabs ("start a conversation on this channel"), composer contact search results. | Phone numbers from the contact's `ContactMethods` bag, cell first, normalized. | Return normalized addresses, preferred first; empty when none. |
| `Task<IReadOnlyList<string>> FindContactIdsAsync(string address, …)` | Links a new conversation to its CRM contact; lists every contact sharing an address. | `OmnichannelContactIndex` normalized cell/home numbers. | Query an index, never load all contacts. `OmnichannelContactIndex.PrimaryEmailAddress` exists for email. |
| `Task<IReadOnlyList<string>> SearchContactIdsByAddressAsync(string term, contactTypes, take, …)` | Composer contact search by address (name search is done by the workspace). | Digits "contains" match on the phone index, ≥ 3 digits. | Return `[]` for terms too short to be meaningful; always bound with `take`. |

## `MessagingChannelCapabilities`

| Capability | Effect in the workspace | Typical values |
| --- | --- | --- |
| `SupportsSubject` | Composer shows a subject line; `MessagingOutboundMessage.Subject` is filled. | Email: `true`. Chat apps: `false`. |
| `SupportsMedia` | Reserved for attachments (`MediaUrls`). | WhatsApp/MMS: `true` only once the send path uploads media. |
| `SupportsDeliveryReceipts` | Delivery ticks on outbound bubbles. | `true` when the provider posts status callbacks you wire to `ApplyDeliveryReceiptAsync`. |
| `SupportsBroadcast` | Multi-recipient composer and the Broadcasts screen accept the channel. | `false` for channels that forbid unsolicited bulk sends (WhatsApp outside templates). |
| `ObservesQuietHours` | Composer warns outside the queue's business hours in the contact's time zone. | SMS/WhatsApp: `true`. Email: `false`. |
| `MaxBodyLength` | Composer character counter and `maxlength`. | Set when the provider has a hard limit. |

If the channel needs a rule the workspace should apply generically (a 24-hour reply window, template-only first
messages…), add a capability here **and** implement it in the workspace for all channels, rather than hard-coding
the channel in the workspace.

## Provider seam

Keep provider calls behind an interface you can fake in tests, as SMS does with `ISmsDispatcher`:

- One provider: inject its client directly into the channel.
- Several providers per channel (SMS: Twilio, Telnyx, Azure): pin the provider on the endpoint
  (`OmnichannelChannelEndpoint.ProviderName`) with a `DisplayDriver<OmnichannelChannelEndpoint>` that only renders
  for your channel, and resolve provider → tenant default → fail in a dispatcher.
- Outbound HTTP goes through a named resilient `HttpClient` (see the phone-provider skill's
  `webhooks-and-resilience.md`): retries with jitter, circuit breaker, no automatic replay of non-idempotent POSTs.
