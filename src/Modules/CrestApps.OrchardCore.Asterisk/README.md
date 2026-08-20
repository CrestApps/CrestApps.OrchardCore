# CrestApps.OrchardCore.Asterisk

Integrates the [Asterisk](https://www.asterisk.org/) telephony platform with the CrestApps Telephony and Contact Center layers. It provides the Asterisk `ITelephonyProvider`, an ARI (Asterisk REST Interface) client, a real-time voice event listener, and the Contact Center voice/media adapters that let the Voice Contact Center Call Router execute calls on Asterisk.

Asterisk-specific concepts stay inside this module; the shared Telephony and Contact Center modules remain provider-agnostic.

## Features

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Asterisk | `CrestApps.OrchardCore.Asterisk` | Provides the Asterisk telephony provider and its settings. Depends on `CrestApps.OrchardCore.Telephony`. |

The Asterisk contact center **voice** adapter (real-time phone-call events and call execution) and **media** adapter (bidirectional RTP media sessions) are no longer separate features. They are integration glue that activates automatically: the voice adapter whenever the **Asterisk** feature and **Contact Center Voice** are both enabled, and the media adapter whenever the **Asterisk** feature and **Contact Center Voice Media** are both enabled. An operator never enables a per-provider toggle that has to match the provider they already configured.

## Installation

Install the package into the web/startup project and enable the Asterisk feature:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Asterisk"
      ]
    }
  ]
}
```

The **Telephony** feature must also be enabled (it is a dependency of Asterisk). Enable a Contact Center voice capability — such as **Contact Center Inbound Voice** or the **Outbound Dialer** — to turn on **Contact Center Voice**; the Asterisk contact center voice adapter then activates on its own.

## Configuration

Configure the Asterisk connection under **Settings → Communication → Telephony → Asterisk**: the ARI base URL, username, password, and application name; the outbound caller ID, endpoint and voicemail templates; and the WebRTC/soft-phone settings (WebSocket URL, SIP domain, TURN, ICE policy, codecs, and PJSIP realtime provider, connection string, and table prefix). Credentials are stored server-side and protected by the tenant's data-protection configuration.

The real-time listener and coordination behavior — including the maximum accepted realtime message size, reconnect timing, credential-lock, and HTTP-timeout options — are **not** part of the admin settings screen. They are bound from the shell configuration section `CrestApps:Asterisk:Coordination` (for example in `appsettings.json` or environment variables) and validated at startup.

Select Asterisk as the default telephony provider only after the Asterisk feature is enabled and configured.

> **Deployment constraint.** ARI channel ownership is currently single-active-process per tenant. Run a single active telephony process per tenant, or front it with an ownership-coordination primitive. See the operator documentation before scaling out.

## Usage

- Application and Contact Center code interacts with Asterisk only through the shared `ITelephonyService` / Contact Center voice abstractions; direct use of the ARI client is reserved for framework infrastructure.
- The real-time listener translates Asterisk events (channel, bridge, and recording state) into Contact Center voice orchestration signals.
- Host and shutdown cancellation propagate as `OperationCanceledException`; ambiguous outcomes retain their durable binding records so the age-gated reconciler can reclaim resources that commit late.

## Dependencies

- `CrestApps.OrchardCore.Telephony`
- `CrestApps.OrchardCore.ContactCenter` (Voice) — for the Contact Center voice and media features

## Documentation

See the [Asterisk provider documentation](https://orchardcore.crestapps.com/telephony/asterisk) for connection setup, deployment topology, and troubleshooting.
