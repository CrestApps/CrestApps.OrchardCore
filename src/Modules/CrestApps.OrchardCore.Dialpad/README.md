# CrestApps.OrchardCore.Dialpad

Integrates the [Dialpad](https://www.dialpad.com/) telephony platform with the CrestApps Telephony and Contact Center layers. It provides the Dialpad `ITelephonyProvider` and the Contact Center voice provider boundary, so the Voice Contact Center Call Router can place outbound calls through Dialpad.

Dialpad-specific concepts stay inside this module; the shared Telephony and Contact Center modules remain provider-agnostic.

## Features

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Dialpad | `CrestApps.OrchardCore.Dialpad` | Provides the Dialpad telephony provider and its settings. Depends on `CrestApps.OrchardCore.Telephony`. |
| Dialpad Contact Center Voice | `CrestApps.OrchardCore.Dialpad.ContactCenterVoice` | Enables the Dialpad provider to place outbound contact center calls and handle their real-time call events. |

## Installation

Install the package into the web/startup project and enable the features you need:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Dialpad",
        "CrestApps.OrchardCore.Dialpad.ContactCenterVoice"
      ]
    }
  ]
}
```

The **Telephony** feature (and, for Contact Center voice, the **Contact Center Voice** feature) must also be enabled.

## Configuration

Configure the Dialpad connection under **Settings → Communication → Telephony → Dialpad**: the target environment (Production or Sandbox), the authentication type and its credentials (an API token, or OAuth client id/secret and scopes), the outbound caller ID, the acting user id, and the required webhook signing secret. Credentials and per-user tokens are stored server-side and protected by the tenant's data-protection configuration.

Choose **Admin account** or **Admin API key** as the webhook registration method, save the settings, then use **Register webhook** to create the company-level call-event webhook and subscription automatically. If **Admin account** is selected, the same button starts the Dialpad sign-in flow so an administrator can sign in; when sign-in returns, the server completes the registration and the settings page shows a progress status while it finishes. The server generates the signing secret and commits it (encrypted) first, then creates the Dialpad webhook (`POST /api/v2/webhooks`) and subscription (`POST /api/v2/subscriptions/call`) from a deferred task that runs after the secret is committed, so Dialpad can read the committed secret when it verifies the webhook. The secret is never shown in the browser. The active environment shows a danger alert until webhook registration is complete, a success alert when it is registered, and a confirmed **Disconnect webhook** action that deletes the saved Dialpad subscription/webhook before clearing the local signing secret. The base **Dialpad** feature now maps `/api/dialpad/webhook/call` directly, so registered call-event deliveries no longer depend on the Contact Center voice feature being enabled. Dialpad event delivery is required for synchronized soft-phone call state and inbound routing; REST polling is only a fallback when provider events are delayed or unavailable.

Select Dialpad as the default telephony provider only after the Dialpad feature is enabled and configured.

## Usage

- Application and Contact Center code interacts with Dialpad only through the shared `ITelephonyService` / Contact Center voice abstractions; the provider is not called directly.
- The provider advertises exactly the `TelephonyCapabilities` it implements, so the soft phone and Contact Center flows enable only the supported operations.
- Outbound dial requests use Dialpad's initiate-via-ring flow. Dialpad first rings the acting user's active Dialpad devices, and the destination is useful only after the user answers in Dialpad. The Orchard Core soft phone controls the call but does not carry Dialpad audio in the browser.
- Host and shutdown cancellation propagate as `OperationCanceledException` rather than being reported as provider failures.

## Dependencies

- `CrestApps.OrchardCore.Telephony`
- `CrestApps.OrchardCore.ContactCenter` (Voice) — for the Contact Center voice feature

## Documentation

See the [Dialpad provider documentation](https://orchardcore.crestapps.com/telephony/dialpad) for connection setup and troubleshooting.
