---
sidebar_label: Phone Number Verifications - Veriphone
sidebar_position: 10
title: Veriphone Phone Number Verification
description: Configure the Veriphone provider for the Phone Number Verifications module.
---

| | |
| --- | --- |
| **Feature Name** | Veriphone Phone Number Verification |
| **Feature ID** | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Veriphone` |
| **Depends on** | `CrestApps.OrchardCore.PhoneNumbers.Verifications` |

## Purpose

The **Veriphone Phone Number Verification** feature verifies phone numbers using the [Veriphone phone number validation API](https://veriphone.io/docs). It is a provider for the [Phone Number Verifications](phone-number-verifications) framework: it calls Veriphone's `/v2/verify` endpoint and maps the response into the framework's provider-agnostic result model.

Enabling this feature automatically enables the core **Phone Number Verifications** feature and registers the Veriphone provider under the key `Veriphone`, making it selectable as the default provider.

## Configuration

Configure the provider under **Settings** -> **Phone Number Verifications** on the **Veriphone** tab. The tab only appears when this feature is enabled.

| Setting | Default | Purpose |
| --- | --- | --- |
| **Endpoint** | `https://api.veriphone.io/v2/verify` | The Veriphone phone number verification endpoint. |
| **API key** | _(empty)_ | The API key issued by Veriphone. Stored as a protected value. |

## Authentication

Veriphone authenticates requests with an API key. The provider stores the key using ASP.NET Core Data Protection and sends it with the recommended `Authorization: Bearer <key>` header. The key is never displayed again after it is saved; enter a new value to rotate it, or leave the field empty to keep the existing key.

## Site settings

The provider settings are stored in the `VeriphonePhoneNumberVerificationSettings` site settings object and can also be provisioned through the Recipes module's generic `settings` step:

```json
{
  "steps": [
    {
      "name": "settings",
      "VeriphonePhoneNumberVerificationSettings": {
        "Endpoint": "https://api.veriphone.io/v2/verify",
        "ProtectedApiKey": "encrypted-api-key"
      }
    }
  ]
}
```

Secret values (`ProtectedApiKey`) are stored encrypted. Provision them through the admin UI so they are encrypted with the tenant's data-protection keys.

## Verification capabilities

Veriphone returns the following information, which the provider maps into the common result model:

| Result field | Source |
| --- | --- |
| `IsValid` / `IsReachable` | `phone_valid` |
| `NormalizedPhoneNumber` | `e164` |
| `NationalFormat` | `local_number` |
| `LineType`, `IsMobile`, `IsLandline`, `IsVoip` | `phone_type` |
| `CountryCode` / `CountryName` / `CountryPrefix` | `country_code` / `country` / `country_prefix` |
| `Region` | `phone_region` |
| `Carrier` | `carrier` |
| `TimeZone` | Derived from the normalized number via `IPhoneNumberService`. |
| `RawProviderResponse` | The full JSON payload. |
| `Metadata["internationalNumber"]` | `international_number` |

## Sample configuration

1. Create an account at [veriphone.io](https://veriphone.io) and copy the API key.
2. Enable the **Veriphone Phone Number Verification** feature under **Configuration** -> **Features**.
3. Open **Settings** -> **Phone Number Verifications**, select the **Veriphone** tab, paste the key, and save.
4. On the **General** tab, select **Veriphone** as the default provider.

## Troubleshooting

| Symptom | Resolution |
| --- | --- |
| Verifications always return `Failed` | Confirm the API key is valid, the account has credits, and the endpoint is reachable. Check the application logs for the Veriphone status code. |
| The **Veriphone** tab is missing | Ensure the **Veriphone Phone Number Verification** feature is enabled. |
| Numbers are not normalized to E.164 | Provide numbers with a country context, or enable the **Phone Numbers Services** feature so `IPhoneNumberService` can normalize them. |
| Rotated key has no effect | Saving an empty API key keeps the previous key. Enter the new key explicitly to replace it. |
