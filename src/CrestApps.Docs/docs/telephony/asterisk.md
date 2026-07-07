---
sidebar_label: Asterisk
sidebar_position: 2
title: Asterisk Telephony Provider
description: Integrate Asterisk as a telephony provider for the Orchard Core soft phone.
---

| | |
| --- | --- |
| **Feature Name** | Asterisk |
| **Feature ID** | `CrestApps.OrchardCore.Asterisk` |

The **Asterisk** module integrates the [Asterisk](https://www.asterisk.org/) platform as a provider for the [Telephony](./) soft phone. It uses the **Asterisk REST Interface (ARI)** over HTTP basic authentication and performs call control server-side, so the browser never needs direct access to Asterisk credentials.

## Two Asterisk providers

When the **Asterisk** feature is enabled, the telephony settings can expose up to two selectable providers:

| Provider | How it is configured | When it appears |
| --- | --- | --- |
| **Asterisk** | Site settings under **Settings → Communication → Telephony → Asterisk** | When an administrator enables it for the tenant |
| **Default Asterisk** | Shell configuration (`appsettings.json`, user secrets, or environment variables) | Automatically, when the required configuration exists |

This mirrors the Orchard Core default-provider pattern used by modules such as SMTP: the tenant-specific provider is managed in the UI, while the configuration-backed provider is automatically available across tenants whenever the host supplies its configuration.

## Capabilities

The Asterisk provider currently advertises support for:

- **Dial**
- **Hang up**
- **Hold / resume**
- **Mute / unmute**
- **Merge**
- **Send DTMF digits**

It does not currently advertise inbound-call, transfer, or soft-phone voicemail capabilities.

## Tenant-configured Asterisk settings

Configure the tenant-specific **Asterisk** provider on the **Asterisk** tab under **Settings → Communication → Telephony**. The Telephony settings UI creates that tab from the site-settings display driver, and the Asterisk editor itself only renders the provider fields inside the tab. You need the `Manage telephony settings` permission.

| Setting | Description |
| --- | --- |
| **Enable Asterisk provider** | Turns the tenant-configured provider on, makes it selectable as the default provider, and reveals the rest of the tenant-specific Asterisk fields in the settings UI. |
| **ARI base URL** | The base ARI endpoint, for example `http://localhost:8088/ari/`. If you omit `/ari`, it is added automatically. |
| **ARI user name** | The Asterisk ARI user name used for HTTP basic authentication. |
| **ARI password** | The ARI password. Stored encrypted with the data protection provider. |
| **Stasis application name** | The ARI application name that receives originated channels. |
| **Endpoint template** | Optional. Use `{number}` to convert the dialed destination into an Asterisk endpoint, for example `PJSIP/{number}@phones` or `Local/{number}@default`. The admin hint now renders that token literally, so the settings screen remains stable while showing the exact placeholder to enter. When empty, the dialed destination is sent to Asterisk as-is. |
| **Outbound caller id** | Optional caller identifier presented on outbound calls. |
| **Dial timeout (seconds)** | How long Asterisk keeps trying to originate the call before timing out. |

When you enable **Asterisk** and no default telephony provider is set yet, **Asterisk** becomes the default automatically. When you disable **Asterisk** while it is the default, the default provider is cleared and the soft phone is disabled until another provider is selected.

## Configuration-backed Default Asterisk provider

The **Default Asterisk** provider is not managed through site settings. Instead, the host configures it through shell configuration. When all required values are present, the provider appears automatically in the **Default telephony provider** selector for every tenant where the module is enabled.

### Required configuration

Use the `OrchardCore:CrestApps:Asterisk:Default` section:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Asterisk": {
        "Default": {
          "BaseUrl": "http://localhost:8088/ari/",
          "UserName": "crestapps",
          "Password": "crestapps-dev",
          "ApplicationName": "crestapps-telephony",
          "EndpointTemplate": "Local/{number}@default",
          "TimeoutSeconds": 30
        }
      }
    }
  }
}
```

Equivalent environment variables use the standard double-underscore path, for example:

```text
OrchardCore__CrestApps__Asterisk__Default__BaseUrl=http://localhost:8088/ari/
OrchardCore__CrestApps__Asterisk__Default__UserName=crestapps
OrchardCore__CrestApps__Asterisk__Default__Password=crestapps-dev
OrchardCore__CrestApps__Asterisk__Default__ApplicationName=crestapps-telephony
OrchardCore__CrestApps__Asterisk__Default__EndpointTemplate=Local/{number}@default
OrchardCore__CrestApps__Asterisk__Default__TimeoutSeconds=30
```

The provider becomes available only when `BaseUrl`, `UserName`, `Password`, and `ApplicationName` are all configured.

## How call control works

The provider uses ARI endpoints such as:

- `POST /channels` to originate a call
- `DELETE /channels/{id}` to hang up a call
- `POST` / `DELETE /channels/{id}/hold` to hold and resume
- `POST` / `DELETE /channels/{id}/mute?direction=both` to mute and unmute
- `POST /channels/{id}/dtmf` to send digits
- `POST /bridges` plus `POST /bridges/{id}/addChannel` to merge two calls

Because all requests are issued server-side, the ARI password never reaches the browser.

## Aspire local development

`src\Startup\CrestApps.Aspire.AppHost` now provisions an **Asterisk** container for local development using the `andrius/asterisk:latest` image, mounts minimal `http.conf`, `ari.conf`, and `extensions.conf` files, and injects the **Default Asterisk** environment variables into the Orchard Core web project automatically.

This makes the configuration-backed provider available immediately for local tenants as soon as:

1. The **Asterisk** module is enabled.
2. The tenant selects **Default Asterisk** as its default telephony provider.

The bundled local configuration is intended for development and connectivity testing. Production deployments should supply their own ARI credentials, dialplan, endpoints, and media/network configuration.
