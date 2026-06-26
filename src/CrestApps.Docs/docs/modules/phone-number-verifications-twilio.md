---
sidebar_label: Phone Number Verifications - Twilio
sidebar_position: 11
title: Twilio Phone Number Verification
description: Configure the Twilio Lookup provider for the Phone Number Verifications module.
---

| | |
| --- | --- |
| **Feature Name** | Twilio Phone Number Verification |
| **Feature ID** | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio` |
| **Depends on** | `CrestApps.OrchardCore.PhoneNumbers.Verifications` |

## Purpose

The **Twilio Phone Number Verification** feature verifies phone numbers using the [Twilio Lookup v2 API](https://www.twilio.com/docs/lookup/v2-api). It is a provider for the [Phone Number Verifications](phone-number-verifications) framework: it calls Twilio Lookup and maps the response into the framework's provider-agnostic result model.

Enabling this feature automatically enables the core **Phone Number Verifications** feature and registers the Twilio provider under the key `Twilio`, making it selectable as the default provider.

## Configuration

Configure the provider under **Settings** -> **Phone Number Verifications** on the **Twilio** tab. The tab only appears when this feature is enabled.

| Setting | Default | Purpose |
| --- | --- | --- |
| **Endpoint** | `https://lookups.twilio.com/v2/PhoneNumbers/{PhoneNumber}` | The Twilio Lookup endpoint template. `{PhoneNumber}` is replaced with the submitted phone number. |
| **Authentication type** | API key SID and secret | The Twilio authentication strategy. |
| **API key SID** / **API key secret** | _(empty)_ | Recommended production credentials. The secret is stored as a protected value. |
| **Account SID** / **Auth Token** | _(empty)_ | Local testing credentials. The token is stored as a protected value. |
| **Country code** | _(empty)_ | Optional ISO 3166-1 alpha-2 country code sent as `CountryCode` when national-format phone numbers are submitted. |
| **Data packages** | _(empty)_ | Optional comma-separated Twilio Lookup data packages sent as `Fields`. Basic validation is included by default; additional packages may incur Twilio charges. |

## Authentication

Twilio Lookup uses HTTP Basic authentication. Twilio recommends [API keys](https://www.twilio.com/docs/iam/api-keys) for production applications because they can be scoped, rotated, and revoked independently. With API key authentication, the **API key SID** is the Basic authentication username and the **API key secret** is the password.

For local testing, Twilio also supports using the **Account SID** as the Basic authentication username and the **Auth Token** as the password. You can find the Account SID and Auth Token in the [Twilio Console](https://www.twilio.com/console). Create API keys in the [Twilio Console API keys page](https://www.twilio.com/console/project/api-keys) or through Twilio's API key resources.

Secret values are never displayed again after they are saved. Enter a new secret or token to rotate it, or leave the field empty to keep the existing protected value.

## Lookup data packages

The provider performs Twilio Basic Lookup by default, which formats and validates the phone number. To request paid data packages, set **Data packages** to a comma-separated list supported by Twilio's `Fields` parameter, such as:

```text
line_type_intelligence,line_status,sms_pumping_risk
```

Review Twilio's [Lookup data package documentation](https://www.twilio.com/docs/lookup/v2-api#data-packages) before enabling paid packages.

## Site settings

The provider settings are stored in the `TwilioPhoneNumberVerificationSettings` site settings object and can also be provisioned through the Recipes module's generic `settings` step:

```json
{
  "steps": [
    {
      "name": "settings",
      "TwilioPhoneNumberVerificationSettings": {
        "Endpoint": "https://lookups.twilio.com/v2/PhoneNumbers/{PhoneNumber}",
        "AuthenticationType": "ApiKey",
        "ApiKeySid": "SKxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
        "ProtectedApiKeySecret": "encrypted-api-key-secret",
        "CountryCode": "US",
        "Fields": "line_type_intelligence,line_status"
      }
    }
  ]
}
```

Secret values (`ProtectedApiKeySecret`, `ProtectedAuthToken`) are stored encrypted. Provision them through the admin UI so they are encrypted with the tenant's data-protection keys.

## Verification capabilities

Twilio returns the following information, which the provider maps into the common result model:

| Result field | Source |
| --- | --- |
| `IsValid` / `IsReachable` | `valid` |
| `NormalizedPhoneNumber` | `phone_number` |
| `NationalFormat` | `national_format` |
| `CountryCode` / `CountryPrefix` | `country_code` / `calling_country_code` |
| `LineType`, `IsMobile`, `IsLandline`, `IsVoip` | `line_type_intelligence.type` when the data package is requested. |
| `Carrier` | `line_type_intelligence.carrier_name` when the data package is requested. |
| `LineStatus` | `line_status.status` when the data package is requested. |
| `RiskScore` / `RiskLevel` / `IsAbuseDetected` | `sms_pumping_risk.sms_pumping_risk_score`, `sms_pumping_risk.carrier_risk_category`, and `sms_pumping_risk.number_blocked` when the data package is requested. |
| `TimeZone` | Derived from the normalized number via `IPhoneNumberService`. |
| `RawProviderResponse` | The full JSON payload. |
| `Metadata["validationErrors"]` | `validation_errors` |
| `Metadata["mobileCountryCode"]` / `Metadata["mobileNetworkCode"]` | `line_type_intelligence.mobile_country_code` / `line_type_intelligence.mobile_network_code` |

## Sample configuration

1. Create or select a Twilio account and enable access to [Twilio Lookup](https://www.twilio.com/docs/lookup).
2. Create an API key in the [Twilio Console API keys page](https://www.twilio.com/console/project/api-keys), or copy the Account SID and Auth Token from the [Twilio Console](https://www.twilio.com/console) for local testing.
3. Enable the **Twilio Phone Number Verification** feature under **Configuration** -> **Features**.
4. Open **Settings** -> **Phone Number Verifications**, select the **Twilio** tab, choose the authentication type, enter the matching credentials, and save.
5. On the **General** tab, select **Twilio** as the default provider.

## Troubleshooting

| Symptom | Resolution |
| --- | --- |
| Verifications always return `Failed` | Confirm the selected authentication type matches the credentials, the API key has access to Lookup, and the endpoint is reachable. Check the application logs for the Twilio status code. |
| The **Twilio** tab is missing | Ensure the **Twilio Phone Number Verification** feature is enabled. |
| Line type or carrier is empty | Add `line_type_intelligence` to **Data packages**. Twilio only returns this object when the data package is requested. |
| Rotated secret has no effect | Saving an empty secret keeps the previous protected value. Enter the new secret explicitly to replace it. |
