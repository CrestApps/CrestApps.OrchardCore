# CrestApps.OrchardCore.DialPad

Integrates the [DialPad](https://www.dialpad.com/) telephony platform with the CrestApps Telephony and Contact Center layers. It provides the DialPad `ITelephonyProvider` and the Contact Center voice provider boundary, so the Voice Contact Center Call Router can place outbound calls through DialPad.

DialPad-specific concepts stay inside this module; the shared Telephony and Contact Center modules remain provider-agnostic.

## Features

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| DialPad | `CrestApps.OrchardCore.DialPad` | Provides the DialPad telephony provider and its settings. Depends on `CrestApps.OrchardCore.Telephony`. |
| DialPad Contact Center Voice | `CrestApps.OrchardCore.DialPad.ContactCenterVoice` | Enables the DialPad provider to place outbound contact center calls and handle their real-time call events. |

## Installation

Install the package into the web/startup project and enable the features you need:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.DialPad",
        "CrestApps.OrchardCore.DialPad.ContactCenterVoice"
      ]
    }
  ]
}
```

The **Telephony** feature (and, for Contact Center voice, the **Contact Center Voice** feature) must also be enabled.

## Configuration

Configure the DialPad connection under **Settings → Communication → Telephony → DialPad**: the target environment (Production or Sandbox), the authentication type and its credentials (an API token, or OAuth client id/secret and scopes), the outbound caller ID, the acting user id, and the webhook signing secret. Credentials and per-user tokens are stored server-side and protected by the tenant's data-protection configuration.

Select DialPad as the default telephony provider only after the DialPad feature is enabled and configured.

## Usage

- Application and Contact Center code interacts with DialPad only through the shared `ITelephonyService` / Contact Center voice abstractions; the provider is not called directly.
- The provider advertises exactly the `TelephonyCapabilities` it implements, so the soft phone and Contact Center flows enable only the supported operations.
- Host and shutdown cancellation propagate as `OperationCanceledException` rather than being reported as provider failures.

## Dependencies

- `CrestApps.OrchardCore.Telephony`
- `CrestApps.OrchardCore.ContactCenter` (Voice) — for the Contact Center voice feature

## Documentation

See the [DialPad provider documentation](https://orchardcore.crestapps.com/telephony/dialpad) for connection setup and troubleshooting.
