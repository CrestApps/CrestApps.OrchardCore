# Telnyx

Integrates the Telnyx voice platform with the Telephony soft phone.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Telnyx | `CrestApps.OrchardCore.Telnyx` | Provides the Telnyx telephony provider, its browser WebRTC soft phone, and signed call-event webhooks. When Contact Center Voice is also enabled, the Telnyx provider automatically participates in contact center call orchestration. |
| Telnyx SMS | `CrestApps.OrchardCore.Telnyx.Sms` | Adds the Telnyx SMS/MMS provider and its signed inbound and delivery-receipt messaging webhook, so Telnyx numbers can send and receive text messages through the SMS Communication Portal. Configured from the OrchardCore_Sms_Telnyx appsettings section or the Telnyx SMS settings on the SMS settings screen. |
| Telnyx AI Voice Agent | `CrestApps.OrchardCore.Telnyx.AiVoice` | Carries an automated voice conversation over Telnyx: the Phone omnichannel processor dials a contact, and Telnyx text-to-speech and real-time transcription supply the audio for the provider-neutral Automated Voice conversation. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Telephony`](../CrestApps.OrchardCore.Telephony/README.md)
- [`CrestApps.OrchardCore.WebSockets`](../CrestApps.OrchardCore.WebSockets/README.md)
- `OrchardCore.Sms`
- [`CrestApps.OrchardCore.Omnichannel.Voice`](../CrestApps.OrchardCore.Omnichannel.Voice/README.md)
- [`CrestApps.OrchardCore.AI`](../CrestApps.OrchardCore.AI/README.md)
- `CrestApps.OrchardCore.AI.Chat.Core`
- [`CrestApps.OrchardCore.Omnichannel.Managements`](../CrestApps.OrchardCore.Omnichannel.Managements/README.md)

## Extension points

- `ITelnyxAgentCredentialStore` (CrestApps.OrchardCore.Telnyx.Core): Persists and queries the durable mapping between an authenticated user and the browser SIP credential minted for their Telnyx soft phone.
- `ITelnyxAgentEndpointResolver` (CrestApps.OrchardCore.Telnyx.Core): Resolves the SIP address an agent's browser is registered on, so every path that dials an agent picks the same credential — the one that is actually listening.
- `ITelnyxAiVoiceEventHandler` (CrestApps.OrchardCore.Telnyx.Core): Handles the Telnyx call-control events of an outbound leg the platform dialed for an automated AI voice agent (client_state intent `AiVoiceLegIntent`).
- `ITelnyxAutomaticCallRecorder` (CrestApps.OrchardCore.Telnyx.Core): Records the calls that are not Contact Center interactions when the tenant records every call.
- `ITelnyxInboundCallRouter` (CrestApps.OrchardCore.Telnyx.Core): Routes a parsed Telnyx inbound call when a higher-level voice feature can accept it.
- and 11 more contracts in the same projects.

## Documentation

See [Telnyx Voice Provider](../../CrestApps.Docs/docs/telephony/telnyx.md) in the technical manual.
