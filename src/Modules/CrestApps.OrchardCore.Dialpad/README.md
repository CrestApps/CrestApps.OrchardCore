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

Configure the Dialpad connection under **Settings → Communication → Telephony → Dialpad**: the target environment (Production or Sandbox), the authentication type and its credentials (an API token, or OAuth client id/secret and scopes), the outbound caller ID, the acting user id, and the webhook signing secret. Credentials and per-user tokens are stored server-side and protected by the tenant's data-protection configuration.

Select Dialpad as the default telephony provider only after the Dialpad feature is enabled and configured.

## Usage

- Application and Contact Center code interacts with Dialpad only through the shared `ITelephonyService` / Contact Center voice abstractions; the provider is not called directly.
- The provider advertises exactly the `TelephonyCapabilities` it implements, so the soft phone and Contact Center flows enable only the supported operations.
- Host and shutdown cancellation propagate as `OperationCanceledException` rather than being reported as provider failures.

## Dependencies

- `CrestApps.OrchardCore.Telephony`
- `CrestApps.OrchardCore.ContactCenter` (Voice) — for the Contact Center voice feature

## Documentation

See the [Dialpad provider documentation](https://orchardcore.crestapps.com/telephony/dialpad) for connection setup and troubleshooting.
