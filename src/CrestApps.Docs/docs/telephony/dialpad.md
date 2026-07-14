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

Enabling **DialPad** automatically enables the **Telephony** feature it depends on. The DialPad module compiles only against the Telephony and Contact Center abstraction packages, keeping it decoupled from their implementation assemblies, the soft phone, and the hub.

The base DialPad feature does not require Contact Center. Install the Contact Center module package before enabling `CrestApps.OrchardCore.DialPad.ContactCenterVoice`; its manifest dependency then enables Contact Center Voice for that tenant.

## Configuration

Configure DialPad on the **DialPad** tab under **Settings → Communication → Telephony**. You need the
`Manage telephony settings` permission. DialPad supports two authentication modes. API key
authentication is the simplest integration path because one DialPad account places calls for the
tenant. OAuth 2.0 is recommended for production multiuser integrations where each soft phone user
connects their own DialPad account.

| Setting | Description |
| --- | --- |
| **Enable DialPad provider** | Turns the provider on and makes it selectable as the default provider. |
| **Environment** | Select **Production** (`dialpad.com`) or **Sandbox** (`sandbox.dialpad.com`). This applies to both the REST API and the OAuth 2.0 endpoints, so developers can validate an integration against the sandbox before going live. |
| **Authentication type** | Select **API key** or **OAuth 2.0**. The default **Select authentication type** option keeps DialPad disabled until an authentication mode is chosen. |
| **API key** | The DialPad API key used when **API key** authentication is selected. Stored encrypted with the data protection provider. |
| **OAuth client id** | The OAuth client id issued by DialPad. Required when **OAuth 2.0** authentication is selected. |
| **OAuth client secret** | The OAuth client secret issued by DialPad. Stored encrypted with the data protection provider. Required when **OAuth 2.0** authentication is selected. |
| **OAuth scopes** | Optional. The space-separated OAuth scopes requested during authorization. The `offline_access` scope is always added automatically so access tokens can be refreshed. |
| **Outbound caller id** | The phone number presented to recipients on outbound calls. Include a country code, for example `+1`. |
| **User id** | The DialPad user id that places outbound calls when **API key** authentication is selected. |
| **Webhook signing secret** | Required when DialPad Contact Center Voice is enabled. The secret DialPad uses to sign inbound call-event webhooks (HS256 JWT). Stored encrypted with the data protection provider. Used to validate webhooks posted to `/api/dialpad/webhook/call` for the Contact Center inbound flow. |

DialPad API calls use the environment's fixed REST endpoint (`https://dialpad.com/api/v2/` for production or
`https://sandbox.dialpad.com/api/v2/` for sandbox), so there is no tenant-level API base URL field to configure.

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
account. DialPad implements the "three-legged" OAuth 2.0 authorization code flow (RFC 6749 §4.1), and the
provider follows DialPad's documented requirements:

- **PKCE** ([RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636)) is always used. A per-request
  code verifier is generated, its `S256` challenge is sent on the authorization request, and the verifier
  is supplied when the authorization code is exchanged for tokens.
- The **`offline_access`** scope is always requested so DialPad issues a refresh token. The user's access
  and refresh tokens are stored **encrypted on the user's account**, and outbound calls are placed with the
  connected user's access token. Tokens are refreshed automatically when they expire.
- The **environment** setting selects the endpoints. Production uses `https://dialpad.com/oauth2/authorize`,
  `/oauth2/token`, and `/oauth2/deauthorize`; sandbox uses the matching `https://sandbox.dialpad.com`
  endpoints.
- When a user **disconnects**, the provider calls DialPad's `deauthorize` endpoint to revoke every token
  DialPad issued on the user's behalf before the stored tokens are removed locally.

## Capabilities

The DialPad provider advertises support for dialing, hang up, hold, resume, mute, transfer, merge, sending DTMF digits, receiving inbound calls, and provider-directory lookup. The soft phone UI uses these capabilities to decide which controls to display. Multi-party conference requests are executed as sequential DialPad merge operations that merge every additional selected call into the primary call. Transfer directory lookup calls DialPad's paginated company-users endpoint, displays the user's name, and prefers the internal extension before falling back to the assigned phone number.

## How call control works

The soft phone sends a request to the `TelephonyHub`, which resolves the DialPad provider and calls
the DialPad REST API on the server. For example, a dial request issues an authenticated `POST` to the
`call` endpoint with the destination number, caller id, and user id; subsequent operations target the
`call/{id}/{action}` endpoints. Because all control happens server-side, the API key never reaches
the browser.

## Contact Center integration

Enable the **DialPad Contact Center Voice** feature to use DialPad as the phone provider for the
Contact Center. It implements the Contact Center voice provider boundary over DialPad, advertises the
`AgentDeviceNative` delivery model (DialPad rings the agent's own soft phone), and supports outbound
dialing and call transfer.

- **Outbound / dialer** — the Contact Center dialer and manual dialing route outbound calls through the
  Voice Contact Center Call Router to DialPad, which places the call and rings the agent's DialPad soft
  phone.
- **Inbound** — configure a DialPad webhook to `POST` call events to `/api/dialpad/webhook/call`. The webhook is authenticated by the **Webhook signing secret** configured on the DialPad settings screen (DialPad signs the payload as an HS256 JWT). New inbound calls create a CRM activity and a voice interaction, are queued through the matching entry point, and are offered to an available agent; later events (answered, held, muted, recording/conference changes, ended) update the interaction and call session. Missing signing secrets are rejected, and a configured secret that cannot be decrypted returns a service-unavailable response instead of downgrading to unsigned acceptance. Webhook request bodies are limited to 1 MiB, oversized deliveries return HTTP 413, and accepted state-changing processing is not canceled when the sending client disconnects.

Create the call-event webhook subscription in the DialPad administration portal and point it at the tenant's public HTTPS URL. Orchard validates and processes deliveries but does not currently create or health-check the DialPad subscription automatically, so operators should monitor subscription status and delivery failures in DialPad.

## Registering the provider in code

The provider is registered by the module's startup with a named HTTP client that uses the standard
ASP.NET Core resiliency pipeline, plus the tenant-aware provider options configuration:

```csharp
services.AddHttpClient(DialPadConstants.ProviderTechnicalName)
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);

        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.CircuitBreaker.FailureRatio = 0.1;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.MinimumThroughput = 100;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
    });

services.AddTelephonyProviderOptionsConfiguration<DialPadProviderOptionsConfigurations>();
services.AddSiteDisplayDriver<DialPadSettingsDisplayDriver>();
```

The `DialPadProviderOptionsConfigurations` implementation contributes the DialPad provider only when
the tenant settings enable it. The named HTTP client is resolved by the provider for REST API and OAuth
token calls, so transient DialPad failures go through the configured retry, timeout, circuit-breaker,
and attempt-limiter policies.
