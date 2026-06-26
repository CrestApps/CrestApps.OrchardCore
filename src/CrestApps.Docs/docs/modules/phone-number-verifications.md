---
sidebar_label: Phone Number Verifications
sidebar_position: 8
title: Phone Number Verifications
description: A provider-agnostic framework for verifying contact phone numbers, with pluggable providers, background revalidation, just-in-time verification, and reporting.
---

| | |
| --- | --- |
| **Feature Name** | Phone Number Verifications |
| **Feature ID** | `CrestApps.OrchardCore.PhoneNumberVerifications` |

The **Phone Number Verifications** module provides a provider-agnostic framework for verifying phone numbers and storing the results on contact content items. It manages verification providers, verification workflows, verification records, reporting, and a background revalidation process.

The core feature does not depend on any external verification provider. Providers ship as separate features (for example, **AbstractAPI Phone Number Verification**) and are discovered dynamically, so adding a provider never requires changes to the core feature.

The core feature is enabled on demand. It is activated automatically when a dependent feature (such as a provider feature) is enabled, or you can enable it directly under **Configuration** -> **Features**.

## Architecture

The framework is built from a small set of cooperating abstractions:

| Abstraction | Responsibility |
| --- | --- |
| `IPhoneNumberVerificationProvider` | Calls an external verification service and maps the response into the common result model. Registered against a unique provider key. |
| `IPhoneNumberVerificationManager` | Selects the active provider, resolves providers by key, executes verification, applies caching rules, and performs just-in-time verification. |
| `IPhoneNumberVerificationStore` | Reads and updates the verification results stored on content items, and determines whether a number is already verified or requires revalidation. |
| `IContentPhoneNumberResolver` | Resolves the phone number to verify from a content item. The default implementation scans content fields whose name contains "phone". |
| `IPhoneNumberVerificationHandler` | Receives `Verifying` and `Verified` lifecycle events. |
| `PhoneNumberVerificationResult` | The provider-agnostic result model. Providers map their native responses into this shape and may extend it through the `Metadata` bag. |

Providers are resolved by a registered **provider key**; a provider never selects itself. Provider keys, display names, and descriptions are registered as metadata and discovered dynamically by the provider selection setting.

## Features

The module ships with the following features:

| Feature | Feature ID | Description |
| --- | --- | --- |
| Phone Number Verifications | `CrestApps.OrchardCore.PhoneNumberVerifications` | The core framework, settings, content part, SQL index, reporting, and background revalidation. |
| AbstractAPI Phone Number Verification | `CrestApps.OrchardCore.PhoneNumberVerifications.AbstractApi` | Verifies phone numbers using the [AbstractAPI Phone Validation](https://www.abstractapi.com/api/phone-validation-api) service. |

Enable a provider feature to activate the core feature and make the provider available for selection.

## Verification result model

Providers return a provider-agnostic `PhoneNumberVerificationResult`:

| Field | Description |
| --- | --- |
| `PhoneNumber` | The phone number that was submitted. |
| `NormalizedPhoneNumber` | The number in E.164 format. |
| `IsValid` / `IsReachable` | Whether the number is valid and reachable. |
| `IsMobile` / `IsLandline` / `IsVoip` | Line-type flags. |
| `CountryCode` / `CountryName` | Country information. |
| `Carrier` | The carrier name. |
| `TimeZone` | The IANA time zone identifier. |
| `LineType` | The normalized line type. |
| `RiskScore` | An optional provider risk score. |
| `VerificationProvider` | The provider key that produced the result. |
| `ProviderReferenceId` | The provider-specific reference identifier. |
| `VerificationDateUtc` | When the verification was performed. |
| `RawProviderResponse` | The raw, unmodified provider response. |
| `Status` | The normalized status (`Unverified`, `Verified`, `Invalid`, `Failed`). |
| `Metadata` | A provider-extensible bag for additional values. |

The entire normalized response is stored, so future providers can expose additional information without schema changes.

## Content item integration

Verification data is stored on contact content items through the **Phone Number Verification** content part (`PhoneNumberVerificationPart`). Attach the part to any content type that represents a contact under **Content** -> **Content Definition** -> **Content Types**.

The part stores:

| Field | Description |
| --- | --- |
| `LastVerifiedUtc` | The UTC timestamp of the most recent successful verification. |
| `LastVerifiedByUserId` | The identifier of the user who last triggered a verification. User identifiers are stored instead of usernames because usernames may change over time. |
| `VerificationProvider` | The provider key that produced the stored result. |
| `VerificationStatus` | The normalized verification status. |
| `VerificationResultJson` | The full normalized provider response. |
| `VerificationAttemptCount` | The number of verification attempts. |
| `NextVerificationDueUtc` | When the next verification becomes due. |

## SQL index

The module maintains a `PhoneNumberVerificationPartIndex` SQL index over the commonly queried fields (content item id, normalized phone number, verification status, provider, last-verified and next-due timestamps, country code, carrier, and line-type flags). The index powers reporting, dashboard widgets, revalidation jobs, and administrative searches. Provider-specific metadata stays in the stored JSON payload and is not indexed.

## Verification workflow

Imports and contact creation never verify numbers automatically, so imports stay fast and inexpensive. Verification happens through one of three paths:

1. **Background revalidation** — a scheduled job verifies contacts that are due.
2. **Explicit requests** — a verification is triggered for a specific contact.
3. **Just-in-time verification** — a verification is performed on demand before a trusted operation when enabled.

## Just-in-time verification

When **Enable just-in-time verification** is on, trusted operations that require a reliable phone number — such as outbound SMS, outbound calling, campaign execution, and third-party integrations — verify the number on demand if its verification has expired (`LastVerifiedUtc` + revalidation interval is in the past). When disabled, these operations never perform synchronous verification and always use the stored result.

## Background revalidation

A daily background task finds contacts that have never been verified or whose verification has expired, verifies them in resilient batches, and updates the stored results, the SQL index, and reporting data. The task:

- processes work in batches to scale to large data sets
- tolerates provider failures without stopping the run
- uses distributed locking so it is safe to run across multiple instances

## Caching and cost optimization

External verification APIs are paid services, so the framework minimizes provider calls. Verification records act as the authoritative cache. A number is only (re)verified when it has never been verified, its verification has expired, or a user explicitly requests revalidation. Cached results are always used first.

## Reporting

A report dashboard is available under **Configuration** -> **Phone Number Verifications** -> **Report**. It surfaces operational metrics such as total contacts, verified and unverified numbers, invalid numbers, mobile/landline/VoIP counts, numbers pending verification, numbers requiring revalidation, verification success rate, verification failures, and provider usage counts. The reporting infrastructure is built on the SQL index and is extensible for future dashboard widgets.

![Phone number verifications report dashboard](/img/docs/phone-number-verifications-report.png)

> Screenshot placeholder: the report dashboard.

## Configuration

Configure the module under **Settings** -> **Phone Number Verifications**.

| Setting | Default | Purpose |
| --- | --- | --- |
| **Enable just-in-time verification** | `false` | Verify a number on demand before trusted operations when its verification has expired. |
| **Revalidation interval (days)** | `365` | The number of days after which a verified number must be revalidated. |
| **Default provider** | First available | The provider used by default. The selector lists every enabled provider. |

![Phone number verifications core settings](/img/docs/phone-number-verifications-settings.png)

> Screenshot placeholder: the core settings page.

Each provider feature contributes its own tab to the same settings page, following the Orchard Core SMS module pattern. Provider tabs only appear when the provider feature is enabled.

![Provider settings tab](/img/docs/phone-number-verifications-provider-settings.png)

> Screenshot placeholder: a provider settings tab.

## Extensibility

Adding a provider never requires changes to the core feature. A provider only needs to:

1. Create a new feature.
2. Implement `IPhoneNumberVerificationProvider`.
3. Register provider-specific settings (optional).
4. Register the implementation and its dependencies.
5. Register the provider key and metadata.

### Creating a custom provider

Implement `IPhoneNumberVerificationProvider` and map the external response into `PhoneNumberVerificationResult`:

```csharp
public sealed class MyPhoneNumberVerificationProvider : IPhoneNumberVerificationProvider
{
    public async Task<PhoneNumberVerificationResult> VerifyAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        // Call the external service and map the response.
        return new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = phoneNumber,
            IsValid = true,
            Status = PhoneNumberVerificationStatus.Verified,
            VerificationProvider = "MyProvider",
        };
    }
}
```

Register the provider in a feature `Startup` using the provider key and metadata:

```csharp
[Feature("MyCompany.MyModule.MyProvider")]
public sealed class MyProviderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(MyPhoneNumberVerificationProvider));

        services.AddPhoneNumberVerificationProvider<MyPhoneNumberVerificationProvider>(
            "MyProvider",
            "My Provider",
            "Verifies phone numbers using My Provider.");

        // Optional: register a settings display driver for the provider tab.
        services.AddSiteDisplayDriver<MyProviderSettingsDisplayDriver>();
    }
}
```

`AddPhoneNumberVerificationProvider` registers the implementation as a keyed service under the provider key and adds its descriptor so the provider selection setting discovers it automatically.

### Customizing phone number resolution

To control which phone number is verified for a content item, implement `IContentPhoneNumberResolver` and register it. The manager uses the first resolver that returns a value, so a custom resolver registered after the default takes precedence for the content it understands.
