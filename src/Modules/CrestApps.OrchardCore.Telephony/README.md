# CrestApps.OrchardCore.Telephony

Provides a **provider-agnostic** call-control layer, a tenant-aware SignalR hub, and an optional floating soft phone for Orchard Core. It defines the abstractions that telephony providers (such as Asterisk and Dialpad) implement, so application code never talks to a vendor API directly.

This module does **not** provide an SMS abstraction. Use the Orchard Core SMS or the CrestApps Omnichannel features for SMS.

## Features

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Telephony | `CrestApps.OrchardCore.Telephony` | Provider resolver, call services, SignalR hub, OAuth connect/callback routes, interaction persistence, and the **Settings → Communication → Telephony** provider settings screen. Depends on `OrchardCore.Users` and `CrestApps.OrchardCore.SignalR`. |
| Telephony Soft Phone | `CrestApps.OrchardCore.Telephony.SoftPhone` | Injects the floating soft phone into the admin dashboard, the front end, or both. Its country-aware keypad uses the `intl-tel-input` resource, so it depends on `CrestApps.OrchardCore.Resources`. |

## Installation

Install the package into the web/startup project and enable the features through the admin dashboard, the **Features** screen, or a recipe:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.Telephony.SoftPhone"
      ]
    }
  ]
}
```

The soft-phone feature is optional; omit it when only server-side call services are required.

## Configuration

Enable Telephony and a provider feature, then configure the provider under **Settings → Communication → Telephony**. Each provider contributes its own settings tab through a site display driver. The default provider is stored in `TelephonySettings.DefaultProviderName`; when a single configured provider is enabled it becomes the default automatically.

Provider credentials and per-user OAuth tokens are always kept server-side and are protected by the tenant's data-protection configuration.

## Usage

- Call `ITelephonyService` from application code; do not invoke a provider directly.
- The browser calls `TelephonyHub`, which resolves the selected `ITelephonyProvider` through `ITelephonyProviderResolver` and returns `TelephonyResult` values while pushing `CallStateChanged`, `IncomingCall`, and `ReceiveError` events.
- Register hubs through `HubRouteManager` so tenant URL prefixes and site base URLs are preserved.
- Call history is persisted through `ITelephonyInteractionStore` and survives provider removal.

To place the soft phone manually, register its resources and render the widget:

```cshtml
<style asp-name="telephony-soft-phone" at="Head"></style>
<script asp-name="telephony-soft-phone" at="Foot"></script>
<script asp-name="telephony-phone-field" at="Foot"></script>

@await DisplayAsync(await New.SoftPhoneWidget())
```

## Creating a provider

Reference `CrestApps.OrchardCore.Telephony.Abstractions`, implement `ITelephonyProvider`, and add only the capability contracts the provider supports (`ITelephonyCallControlProvider`, `ITelephonyHoldProvider`, `ITelephonyMuteProvider`, `ITelephonyTransferProvider`, `ITelephonyConferenceProvider`, `ITelephonyDtmfProvider`, `ITelephonyInboundCallProvider`, `ITelephonySoftPhoneCredentialsProvider`). Keep `TelephonyCapabilities` aligned with the implemented contracts — the service fails closed when either is absent.

## Dependencies

- `OrchardCore.Users`
- `CrestApps.OrchardCore.SignalR`

## Documentation

See the [Telephony documentation](https://orchardcore.crestapps.com/telephony/) for full configuration, provider authoring, and soft-phone guidance.
