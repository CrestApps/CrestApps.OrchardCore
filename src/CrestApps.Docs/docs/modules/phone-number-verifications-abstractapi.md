---
sidebar_label: Phone Number Verifications - AbstractAPI
sidebar_position: 9
title: AbstractAPI Phone Number Verification
description: Configure the AbstractAPI provider for the Phone Number Verifications module.
---

| | |
| --- | --- |
| **Feature Name** | AbstractAPI Phone Number Verification |
| **Feature ID** | `CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi` |
| **Depends on** | `CrestApps.OrchardCore.PhoneNumbers.Verifications` |

## Purpose

The **AbstractAPI Phone Number Verification** feature verifies phone numbers using the [AbstractAPI Phone Validation](https://www.abstractapi.com/api/phone-validation-api) service. It is a provider for the [Phone Number Verifications](phone-number-verifications) framework: it calls the AbstractAPI endpoint and maps the response into the framework's provider-agnostic result model.

Enabling this feature automatically enables the core **Phone Number Verifications** feature and registers the AbstractAPI provider under the key `AbstractApi`, making it selectable as the default provider.

## Configuration

Configure the provider under **Settings** -> **Phone Number Verifications** on the **AbstractAPI** tab. The tab only appears when this feature is enabled.

| Setting | Default | Purpose |
| --- | --- | --- |
| **Endpoint** | `https://phonevalidation.abstractapi.com/v1/` | The AbstractAPI Phone Validation endpoint. |
| **Authentication type** | API key | The authentication strategy. AbstractAPI uses API key authentication. |
| **API key** | _(empty)_ | The API key issued by AbstractAPI. Stored as a protected value. |
| **Username** / **Password** | _(empty)_ | Used when basic authentication is selected. |
| **Client ID** / **Client secret** | _(empty)_ | Used when client credentials authentication is selected. |

![AbstractAPI provider settings tab](/img/docs/phone-number-verifications-provider-settings.png)

> Screenshot placeholder: the AbstractAPI settings tab.

## Authentication

AbstractAPI authenticates requests with an API key supplied as the `api_key` query string parameter. Select **API key** as the authentication type and provide the key issued from your AbstractAPI dashboard. The key is stored using ASP.NET Core Data Protection and is never displayed again after it is saved — enter a new value to rotate it, or leave the field empty to keep the existing key.

The **Basic** and **Client credentials** authentication types are provided for providers and proxies that front AbstractAPI with additional authentication. When **Basic** is selected and a username is provided, the request includes an HTTP `Authorization: Basic` header alongside the API key.

## Site settings

The provider settings are stored in the `AbstractApiPhoneNumberVerificationSettings` site settings object and can also be provisioned through the Recipes module's generic `settings` step:

```json
{
  "steps": [
    {
      "name": "settings",
      "AbstractApiPhoneNumberVerificationSettings": {
        "Endpoint": "https://phonevalidation.abstractapi.com/v1/",
        "AuthenticationType": "ApiKey",
        "ProtectedApiKey": "encrypted-api-key"
      }
    }
  ]
}
```

Secret values (`ProtectedApiKey`, `ProtectedPassword`, `ProtectedClientSecret`) are stored encrypted. Provision them through the admin UI so they are encrypted with the tenant's data-protection keys.

## Verification capabilities

AbstractAPI returns the following information, which the provider maps into the common result model:

| Result field | Source |
| --- | --- |
| `IsValid` / `IsReachable` | `valid` |
| `NormalizedPhoneNumber` | `international_format` |
| `NationalFormat` | `local_format` |
| `LineType`, `IsMobile`, `IsLandline`, `IsVoip` | `type` |
| `CountryCode` / `CountryName` | `country.code` / `country.name` |
| `Carrier` | `carrier` |
| `TimeZone` | Derived from the normalized number via `IPhoneNumberService`. |
| `RawProviderResponse` | The full JSON payload. |
| `Metadata["location"]` | `location` |

## Sample configuration

1. Create an account at [abstractapi.com](https://www.abstractapi.com/api/phone-validation-api) and copy the Phone Validation API key.
2. Enable the **AbstractAPI Phone Number Verification** feature under **Configuration** -> **Features**.
3. Open **Settings** -> **Phone Number Verifications**, select the **AbstractAPI** tab, set **Authentication type** to **API key**, paste the key, and save.
4. On the **General** tab, select **AbstractAPI** as the default provider.

## Troubleshooting

| Symptom | Resolution |
| --- | --- |
| Verifications always return `Failed` | Confirm the API key is valid and that the endpoint is reachable. Check the application logs for the AbstractAPI status code. |
| The **AbstractAPI** tab is missing | Ensure the **AbstractAPI Phone Number Verification** feature is enabled. |
| Numbers are not normalized to E.164 | Provide numbers with a country context, or enable the **Phone Numbers Services** feature so `IPhoneNumberService` can normalize them. |
| Rotated key has no effect | Saving an empty API key keeps the previous key. Enter the new key explicitly to replace it. |
