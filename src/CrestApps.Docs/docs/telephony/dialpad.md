---
sidebar_label: Dialpad
sidebar_position: 1
title: Dialpad Telephony Provider
description: Integrate the Dialpad platform as a telephony provider for the Orchard Core soft phone.
---

| | |
| --- | --- |
| **Feature Name** | Dialpad |
| **Feature ID** | `CrestApps.OrchardCore.Dialpad` |

The **Dialpad** module integrates the [Dialpad](https://www.dialpad.com/) platform as a provider for
the [Telephony](./) soft phone. It implements the provider-agnostic `ITelephonyProvider` contract
and performs all call control server-side through the Dialpad REST API, so the browser never needs a
Dialpad SDK or token.

## Dependencies

Enabling **Dialpad** automatically enables the **Telephony** feature it depends on. The Dialpad module compiles only against the Telephony and Contact Center abstraction packages, keeping it decoupled from their implementation assemblies, the soft phone, and the hub.

The base Dialpad feature does not require Contact Center. Install the Contact Center module package before enabling `CrestApps.OrchardCore.Dialpad.ContactCenterVoice`; its manifest dependency then enables Contact Center Voice for that tenant.

## Configuration

Configure Dialpad on the **Dialpad** tab under **Settings → Communication → Telephony**. You need the
`Manage telephony settings` permission. Dialpad supports two authentication modes. API key
authentication is the simplest integration path because one Dialpad account places calls for the
tenant. OAuth 2.0 is recommended for production multiuser integrations where each soft phone user
connects their own Dialpad account.

Dialpad exposes two separate environments — **Production** (`dialpad.com`) and **Sandbox**
(`sandbox.dialpad.com`). The settings editor lets you configure each environment independently in its
own card, so a tenant can hold production and sandbox credentials side by side. The **Active
environment** selector decides which credential set the provider actually connects with, letting you
validate an integration against the sandbox and switch to production without re-entering credentials.
Switching the active environment preserves the credentials of the environment that is not active.

| Setting | Description |
| --- | --- |
| **Enable Dialpad provider** | Turns the provider on and makes it selectable as the default provider. |
| **Active environment** | Select **Production** (`dialpad.com`) or **Sandbox** (`sandbox.dialpad.com`). This chooses which environment's credentials the provider uses to connect and place calls, and applies to both the REST API and the OAuth 2.0 endpoints. |

Each environment card (**Production** and **Sandbox**) exposes its own copy of the following credentials:

| Setting | Description |
| --- | --- |
| **Authentication type** | Select **API key** or **OAuth 2.0**. The default **Select authentication type** option keeps Dialpad disabled until an authentication mode is chosen for the active environment. |
| **API key** | The Dialpad API key used when **API key** authentication is selected. Stored encrypted with the data protection provider. |
| **User id** | The Dialpad user id that places outbound calls when **API key** authentication is selected. |
| **Outbound caller id** | The phone number presented to recipients on outbound calls. Include a country code, for example `+1`. |
| **OAuth client id** | The OAuth client id issued by Dialpad. Required when **OAuth 2.0** authentication is selected. |
| **OAuth client secret** | The OAuth client secret issued by Dialpad. Stored encrypted with the data protection provider. Required when **OAuth 2.0** authentication is selected. |
| **OAuth scopes** | Optional. The space-separated OAuth scopes requested during authorization. The `offline_access` scope is always added automatically so access tokens can be refreshed. |
| **Webhook signing secret** | Required when Dialpad Contact Center Voice is enabled. The secret Dialpad uses to sign inbound call-event webhooks (HS256 JWT). Stored encrypted with the data protection provider. Used to validate webhooks posted to `/api/dialpad/webhook/call` for the Contact Center inbound flow. See [Where to obtain the webhook signing secret](#where-to-obtain-the-webhook-signing-secret). |

Dialpad API calls use the active environment's fixed REST endpoint (`https://dialpad.com/api/v2/` for production or
`https://sandbox.dialpad.com/api/v2/` for sandbox), so there is no tenant-level API base URL field to configure.

When you enable Dialpad and no default provider is set yet, Dialpad becomes the default
automatically. When you disable Dialpad while it is the default provider, the default is cleared and
the soft phone is disabled until another provider is selected.

Secrets (the API key and the OAuth client secret) are encrypted before they are persisted. When a
secret has already been saved the field is left empty; enter a new value only when you want to
replace the stored secret.

The settings editor validates the **active** environment before saving. API key authentication
requires both the API key and the Dialpad user id. OAuth 2.0 requires the client id and client
secret. Missing values are reported next to the matching fields so administrators know exactly what
must be provided. The non-active environment is saved as entered without blocking validation.

### Authenticating with an API key

Select **API key** when one Dialpad account should place calls for the tenant. Enter the Dialpad API
key, the Dialpad user id that places outbound calls, and optionally an outbound caller id. This mode
does not require each soft phone user to connect their own Dialpad account; all provider calls use
the account that owns the API key.

### Authenticating users with OAuth 2.0

Select **OAuth 2.0** when each soft phone user should connect their own Dialpad account. Visit
[developers.dialpad.com](https://developers.dialpad.com/) for Dialpad's current instructions on
obtaining OAuth 2.0 access, creating an OAuth application, and configuring the required credentials.

To configure OAuth 2.0:

1. Register an OAuth application in the Dialpad admin portal to obtain a client id and client secret.
2. Add `{scheme}://{host}/Telephony/Connect/Callback` (with your tenant URL prefix when one is
   configured) as an allowed redirect URI on the Dialpad OAuth application.
3. Enter the client id, client secret, and any scopes in the matching environment card (**Production**
   or **Sandbox**) of the Dialpad settings, and set that environment as the **Active environment**.

Each user then sees a **Connect to provider** button in the soft phone and connects their own Dialpad
account. Dialpad implements the "three-legged" OAuth 2.0 authorization code flow (RFC 6749 §4.1), and the
provider follows Dialpad's documented requirements:

- **PKCE** ([RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636)) is always used. A per-request
  code verifier is generated, its `S256` challenge is sent on the authorization request, and the verifier
  is supplied when the authorization code is exchanged for tokens.
- The **`offline_access`** scope is always requested so Dialpad issues a refresh token. The user's access
  and refresh tokens are stored **encrypted on the user's account**, and outbound calls are placed with the
  connected user's access token. Tokens are refreshed automatically when they expire.
- The **active environment** setting selects the endpoints. Production uses `https://dialpad.com/oauth2/authorize`,
  `/oauth2/token`, and `/oauth2/deauthorize`; sandbox uses the matching `https://sandbox.dialpad.com`
  endpoints.
- When a user **disconnects**, the local tokens are always removed first — and that deletion is durably
  committed before the provider is contacted, so a concurrent request cannot observe stale credentials — so
  the interactive credentials are cleared immediately, then the provider calls Dialpad's `deauthorize`
  endpoint to revoke the token
  Dialpad issued on the user's behalf. Revocation is attempted whenever a stored access token exists, even
  if the tenant has since switched to API-key authentication, so a leftover OAuth grant is never silently
  abandoned. Revocation reports a typed result: a confirmed success, a definitive rejection, or an
  indeterminate outcome. A timeout, throttling (`429`), or server error (`5xx`) is treated as indeterminate
  because the unsafe deauthorize `POST` may still have committed. When Dialpad does not confirm the
  revocation, the disconnect still succeeds locally but is logged as an incomplete remote revocation and the
  disconnect response reports `remoteRevocationConfirmed: false` so operators know the grant may still be
  active at the provider.

## Capabilities

The Dialpad provider advertises support for dialing, hang up, hold, resume, mute, transfer, merge, sending DTMF digits, receiving inbound calls, and provider-directory lookup. The soft phone UI uses these capabilities to decide which controls to display. Multi-party conference requests are executed as sequential Dialpad merge operations that merge every additional selected call into the primary call. Transfer directory lookup calls Dialpad's paginated company-users endpoint, displays the user's name, and prefers the internal extension before falling back to the assigned phone number.

## How call control works

The soft phone sends a request to the `TelephonyHub`, which resolves the Dialpad provider and calls
the Dialpad REST API on the server. For example, a dial request issues an authenticated `POST` to the
`call` endpoint with the destination number, caller id, and user id; subsequent operations target the
`call/{id}/{action}` endpoints. Because all control happens server-side, the API key never reaches
the browser.

## Contact Center integration

Enable the **Dialpad Contact Center Voice** feature to use Dialpad as the phone provider for the
Contact Center. It implements the Contact Center voice provider boundary over Dialpad, advertises the
`AgentDeviceNative` delivery model (Dialpad rings the agent's own soft phone), and supports outbound
dialing and call transfer.

- **Outbound / dialer** — the Contact Center dialer and manual dialing route outbound calls through the
  Voice Contact Center Call Router to Dialpad, which places the call and rings the agent's Dialpad soft
  phone.
- **Inbound** — configure a Dialpad webhook to `POST` call events to `/api/dialpad/webhook/call`. The webhook is authenticated by the **Webhook signing secret** configured on the Dialpad settings screen (Dialpad signs the payload as an HS256 JWT). New inbound calls create a CRM activity and a voice interaction, are queued through the matching entry point, and are offered to an available agent; later events (answered, held, muted, recording/conference changes, ended) update the interaction and call session. Missing signing secrets are rejected, and a configured secret that cannot be decrypted returns a service-unavailable response instead of downgrading to unsigned acceptance. Webhook request bodies are limited to 1 MiB, oversized deliveries return HTTP 413, and accepted state-changing processing is not canceled when the sending client disconnects.

Create the call-event webhook subscription in the Dialpad administration portal and point it at the tenant's public HTTPS URL. Orchard validates and processes deliveries but does not currently create or health-check the Dialpad subscription automatically, so operators should monitor subscription status and delivery failures in Dialpad.

### Where to obtain the webhook signing secret

The **Webhook signing secret** is a value **you choose and register with Dialpad** when you create the
call-event webhook — it is not issued by Dialpad. Dialpad then uses it to sign every webhook payload it
delivers (as an HS256 JWT), and this module validates inbound deliveries to `/api/dialpad/webhook/call`
against the same value.

Create the webhook and set its secret using either method, then paste the same secret into the active
environment's **Webhook signing secret** field:

- **Dialpad developer portal** — in [developers.dialpad.com](https://developers.dialpad.com/), create a
  webhook subscription for call events and set its **secret** to a strong random value that you generate.
- **Dialpad API** — `POST https://dialpad.com/api/v2/webhooks` (or the sandbox host) with a JSON body that
  includes the `hook_url` (your tenant's `https://<host>/api/dialpad/webhook/call` endpoint) and a `secret`
  field set to the value you generate, then subscribe that webhook to the call events.

Use a high-entropy random string (for example a 32-byte value) as the secret, keep production and sandbox
secrets distinct, and rotate them if they are ever exposed. Leaving the field blank after saving keeps the
previously stored secret unchanged.

The provider is registered by the module's startup with a named HTTP client that uses the standard
ASP.NET Core resiliency pipeline, plus the tenant-aware provider options configuration:

```csharp
services.AddHttpClient(DialpadConstants.ProviderTechnicalName)
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

services.AddTelephonyProviderOptionsConfiguration<DialpadProviderOptionsConfigurations>();
services.AddSiteDisplayDriver<DialpadSettingsDisplayDriver>();
```

The `DialpadProviderOptionsConfigurations` implementation contributes the Dialpad provider only when
the tenant settings enable it. The named HTTP client is resolved by the provider for REST API and OAuth
token calls, so transient Dialpad failures go through the configured retry, timeout, circuit-breaker,
and attempt-limiter policies.

## Webhook contract tests

Dialpad does not publish a machine-readable schema, so its contract cannot be bound to a vendored specification the way [Asterisk](asterisk.md) is. It is bound to recorded deliveries instead, and the manifest at `tests/CrestApps.OrchardCore.Tests/Telephony/Cassettes/Dialpad/manifest.json` declares that weaker guarantee explicitly rather than implying a protocol proof it cannot make.

The rigor comes from coverage floors that are derived from the production code rather than restated by hand. `DialpadWebhookContractTests` scans the normalizer's own token switches and requires `states.json` to name exactly the call-state, recording-state, and answer-classification tokens the production code interprets, so a newly interpreted token fails the build until a recorded expectation is added for it. Tokens the normalizer deliberately ignores are recorded as such and asserted to stay ignored.

Each recorded scenario is then replayed through the whole ingress path rather than through the normalizer alone: the signed JWT webhook endpoint, the production deserializer, and the production normalizer. A delivery signed with the wrong secret is rejected, and every payload field the recordings use must bind to the property names the production model declares, so renaming a serialized field breaks the build.
