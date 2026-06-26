---
sidebar_label: DialPad
sidebar_position: 1
title: DialPad Telephony Provider
description: Integrate the DialPad platform as a telephony provider for the Orchard Core soft phone.
---

| | |
| --- | --- |
| **Feature Name** | DialPad |
| **Feature ID** | `CrestApps.OrchardCore.DialPad` |

The **DialPad** module integrates the [DialPad](https://www.dialpad.com/) platform as a provider for
the [Telephony](./) soft phone. It implements the provider-agnostic `ITelephonyProvider` contract
and performs all call control server-side through the DialPad REST API, so the browser never needs a
DialPad SDK or token.

## Dependencies

Enabling **DialPad** automatically enables the **Telephony** feature it depends on. The DialPad
module depends only on `CrestApps.OrchardCore.Telephony.Abstractions`, keeping it decoupled from the
soft phone and the hub.

## Configuration

Configure DialPad on the **DialPad** tab under **Settings → Communication → Telephony**. You need the
`Manage telephony settings` permission. DialPad supports two authentication modes. API key
authentication is the simplest integration path because one DialPad account places calls for the
tenant. OAuth 2.0 is recommended for production multiuser integrations where each soft phone user
connects their own DialPad account.

| Setting | Description |
| --- | --- |
| **Enable DialPad provider** | Turns the provider on and makes it selectable as the default provider. |
| **Authentication type** | Select **API key** or **OAuth 2.0**. The default **Select authentication type** option keeps DialPad disabled until an authentication mode is chosen. |
| **API key** | The DialPad API key used when **API key** authentication is selected. Stored encrypted with the data protection provider. |
| **OAuth client id** | The OAuth client id issued by DialPad. Required when **OAuth 2.0** authentication is selected. |
| **OAuth client secret** | The OAuth client secret issued by DialPad. Stored encrypted with the data protection provider. Required when **OAuth 2.0** authentication is selected. |
| **OAuth scopes** | Optional. The space-separated OAuth scopes requested during authorization. |
| **Outbound caller id** | The phone number presented to recipients on outbound calls. Include a country code, for example `+1`. |
| **User id** | The DialPad user id that places outbound calls when **API key** authentication is selected. |

DialPad API calls use the provider's fixed REST endpoint, `https://dialpad.com/api/v2/`, so there is
no tenant-level API base URL field to configure.

When you enable DialPad and no default provider is set yet, DialPad becomes the default
automatically. When you disable DialPad while it is the default provider, the default is cleared and
the soft phone is disabled until another provider is selected.

Secrets (the API key and the OAuth client secret) are encrypted before they are persisted. When a
secret has already been saved the field is left empty; enter a new value only when you want to
replace the stored secret.

The settings editor validates the selected authentication mode before saving. API key authentication
requires both the API key and the DialPad user id. OAuth 2.0 requires the client id and client
secret. Missing values are reported next to the matching fields so administrators know exactly what
must be provided.

### Authenticating with an API key

Select **API key** when one DialPad account should place calls for the tenant. Enter the DialPad API
key, the DialPad user id that places outbound calls, and optionally an outbound caller id. This mode
does not require each soft phone user to connect their own DialPad account; all provider calls use
the account that owns the API key.

### Authenticating users with OAuth 2.0

Select **OAuth 2.0** when each soft phone user should connect their own DialPad account. Visit
[developers.dialpad.com](https://developers.dialpad.com/) for DialPad's current instructions on
obtaining OAuth 2.0 access, creating an OAuth application, and configuring the required credentials.

To configure OAuth 2.0:

1. Register an OAuth application in the DialPad admin portal to obtain a client id and client secret.
2. Add `{scheme}://{host}/Telephony/Connect/Callback` (with your tenant URL prefix when one is
   configured) as an allowed redirect URI on the DialPad OAuth application.
3. Enter the client id, client secret, and any scopes on the DialPad settings tab.

Each user then sees a **Connect to provider** button in the soft phone and connects their own DialPad
account. DialPad uses the `https://dialpad.com/oauth2/authorize` and `https://dialpad.com/oauth2/token`
endpoints. The user's access and refresh tokens are stored **encrypted on the user's account**, and
outbound calls are placed with the connected user's access token. Tokens are refreshed automatically
when they expire.

## Capabilities

The DialPad provider advertises support for dialing, hang up, hold, resume, mute, transfer, merge,
sending DTMF digits, and receiving inbound calls. The soft phone UI uses these capabilities to decide
which controls to display.

## How call control works

The soft phone sends a request to the `TelephonyHub`, which resolves the DialPad provider and calls
the DialPad REST API on the server. For example, a dial request issues an authenticated `POST` to the
`call` endpoint with the destination number, caller id, and user id; subsequent operations target the
`call/{id}/{action}` endpoints. Because all control happens server-side, the API key never reaches
the browser.

## Registering the provider in code

The provider is registered by the module's startup using the shared extension method:

```csharp
services
    .AddDialPadTelephonyProvider()
    .AddSiteDisplayDriver<DialPadSettingsDisplayDriver>();
```

`AddDialPadTelephonyProvider` registers the named HTTP client and an
`IConfigureOptions<TelephonyProviderOptions>` that reflects whether DialPad is enabled based on the
tenant settings.
