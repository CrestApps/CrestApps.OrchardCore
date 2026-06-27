---
sidebar_label: Phone Number Verifications
sidebar_position: 8
title: Phone Number Verifications
description: A provider-agnostic framework for verifying contact phone numbers, with pluggable providers, content-part storage, SQL indexing, background revalidation, and reporting.
---

| | |
| --- | --- |
| **Feature Name** | Phone Number Verifications |
| **Feature ID** | `CrestApps.OrchardCore.PhoneNumbers.Verifications` |

The **Phone Number Verifications** module provides a provider-agnostic framework for verifying phone numbers and storing the results directly on content items through a content part. It manages verification providers, content-part storage helpers, SQL indexing, reporting, and a background revalidation process.

The core feature does not depend on any external verification provider. Providers ship as separate features (for example, **AbstractAPI Phone Number Verification**, **Veriphone Phone Number Verification**, and **Twilio Phone Number Verification**) and are discovered dynamically, so adding a provider never requires changes to the core feature.

The core feature is enabled on demand. It is activated automatically when a dependent feature (such as a provider feature) is enabled, or you can enable it directly under **Configuration** -> **Features**.

## Architecture

The framework is built from a small set of cooperating abstractions:

| Abstraction | Responsibility |
| --- | --- |
| `IPhoneNumberVerificationProvider` | Calls an external verification service and maps the response into the common result model. Registered against a unique provider key. |
| `IPhoneNumberVerificationManager` | Selects the active provider, resolves providers by key, executes verification, and raises verification lifecycle handlers. |
| `PhoneNumberVerificationPartExtensions` | Reads and updates verification data stored on content items with `contentItem.Alter<PhoneNumberVerificationPart>(...)` and `contentItem.TryGet<PhoneNumberVerificationPart>(...)`. |
| `IPhoneNumberVerificationHandler` | Receives `Verifying` and `Verified` lifecycle events. |
| `PhoneNumberVerificationResult` | The provider-agnostic result model. Providers map their native responses into this shape and may extend it through the `Metadata` bag. |

Providers are resolved by a registered **provider key**; a provider never selects itself. Provider keys, display names, and descriptions are registered as metadata and discovered dynamically by the provider selection setting.

## Features

The module ships with the following features:

| Feature | Feature ID | Description |
| --- | --- | --- |
| Phone Number Verifications | `CrestApps.OrchardCore.PhoneNumbers.Verifications` | The core framework, settings, content part, SQL index, automatic contact verification, reporting, and background revalidation. |
| AbstractAPI Phone Number Verification | `CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi` | Verifies phone numbers using the [AbstractAPI Phone Validation](https://www.abstractapi.com/api/phone-validation-api) service. |
| Veriphone Phone Number Verification | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Veriphone` | Verifies phone numbers using the [Veriphone phone number validation API](https://veriphone.io/docs). |
| Twilio Phone Number Verification | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio` | Verifies phone numbers using the [Twilio Lookup API](https://www.twilio.com/docs/lookup/v2-api). |

Enable a provider feature to activate the core feature and make the provider available for selection.

## Verification result model

Providers return a provider-agnostic `PhoneNumberVerificationResult`:

| Field | Description |
| --- | --- |
| `PhoneNumber` | The phone number that was submitted. |
| `NormalizedPhoneNumber` | The number in E.164 format. |
| `IsValid` / `IsReachable` | Whether the number is valid and reachable. |
| `IsMobile` / `IsLandline` / `IsVoip` | Line-type flags. |
| `NationalFormat` | The provider-supplied national display format, when available. |
| `CountryCode` / `CountryName` / `CountryPrefix` | Country information. |
| `Region` / `City` | Provider-reported location details. |
| `Carrier` | The carrier name. |
| `TimeZone` | The IANA time zone identifier. |
| `LineType` | The normalized line type. |
| `LineStatus` | The provider-specific line status, when available. |
| `RiskScore` / `RiskLevel` | Optional provider risk information. |
| `IsDisposable` / `IsAbuseDetected` | Optional provider risk flags. |
| `VerificationProvider` | The provider key that produced the result. |
| `ProviderReferenceId` | The provider-specific reference identifier. |
| `VerificationDateUtc` | When the verification was performed. |
| `RawProviderResponse` | The raw, unmodified provider response. |
| `Status` | The normalized status (`Unverified`, `Verified`, `Invalid`, `Failed`). |
| `Metadata` | A provider-extensible bag for additional values. |

The entire normalized response is stored, so future providers can expose additional information without schema changes. Rich responses such as Dialpad Professional phone intelligence can map common fields (format, carrier, location, validation, and risk) into the shared model while retaining plan-specific details such as messaging, registration, and breach data in `Metadata` and `RawProviderResponse`.

## Content item integration

Verification data is stored on contact content items through the **Phone Number Verification** content part (`PhoneNumberVerificationPart`). Attach the part to any content type that represents a contact under **Content** -> **Content Definition** -> **Content Types**.

The part stores:

| Field | Description |
| --- | --- |
| `PhoneNumber` | The phone number submitted for verification. |
| `NormalizedPhoneNumber` | The normalized phone number in E.164 format when available. |
| `LastVerifiedUtc` | The UTC timestamp of the most recent completed validity verification. |
| `LastVerifiedByUserId` | The identifier of the user who last triggered a verification. User identifiers are stored instead of usernames because usernames may change over time. |
| `VerificationProvider` | The provider key that produced the stored result. |
| `VerificationStatus` | The normalized verification status. |
| `VerificationResultJson` | The full normalized provider response. |
| `VerificationAttemptCount` | The number of verification attempts. |
| `FailedAttemptCount` | The number of consecutive failed verification requests (provider or transport errors). Reset to zero when a verification request completes successfully or the record is re-queued. |
| `LastError` | The error message from the most recent failed verification request, when any. |
| `LastAttemptUtc` | The UTC timestamp of the most recent verification attempt, whether it completed or failed. |
| `NextVerificationDueUtc` | When the next verification becomes due. |

## SQL index

The module maintains a `PhoneNumberVerificationPartIndex` SQL index over the commonly queried fields (content item id, raw and normalized phone numbers, verification status, provider, last-verified and next-due timestamps, country code, carrier, line-type flags, and line status). The index powers reporting, dashboard widgets, revalidation jobs, and administrative searches. Provider-specific metadata stays in the stored JSON payload and is not indexed.

## Verification workflow

Verification happens through one of three paths:

1. **Automatic contact changes** — when the Omnichannel Management feature is enabled, a content handler watches omnichannel contact content items. On create or update, it extracts the preferred phone number from the `ContactMethods` bag (`Cell`, then `Home`, `Office`, `Work`, `Other`) and verifies it when it is new or different from the number stored in `PhoneNumberVerificationPart`.
2. **Background revalidation** — a scheduled job verifies contacts that already have a stored phone number and are due.
3. **Explicit requests** — a verification is triggered for a specific phone number.

Automatic verification runs as deferred work after the content item is saved, so the content lifecycle does not call the external provider inline. If no provider is enabled or a provider call fails, the handler stores the phone number as `Unverified`; the background revalidation task can pick it up later when a provider is available.

When a phone field is rendered for display or editing, the UI shows a status icon next to the number when verification data is available on the same content item. Verified numbers show a green check mark, invalid numbers show a red error icon, failed verifications show a warning icon, and unverified numbers show a muted unknown icon. Each icon includes a tooltip that explains the status and includes the last verification timestamp when one is available.

## Resilience and retries

The framework distinguishes a **completed** verification (the provider returned a definitive `Verified` or `Invalid` answer) from a **failed request** (a provider rate limit, HTTP error, transport failure, or unparseable response). A failed request never marks a number as `Invalid`, because the number's validity was never actually determined.

When a verification request fails:

- `FailedAttemptCount` is incremented and the provider error message is stored in `LastError`.
- A record that has **never** completed verification is surfaced with the `Failed` status and is kept due so the background task retries it.
- A record that was **previously verified** is left untouched (status, `LastVerifiedUtc`, and `NextVerificationDueUtc` are preserved), so a transient provider outage never downgrades a known-good number. Only `FailedAttemptCount` and `LastError` are updated.

Failed records are retried automatically by the background task until `FailedAttemptCount` reaches the configured **Maximum verification attempts** (default `3`). Once a record reaches that cap it stops auto-retrying and is flagged as **Needs attention** in the records queue, where an administrator can inspect the error and manually re-queue it. When a verification request finally completes, `FailedAttemptCount` and `LastError` are reset to zero/null.

## Verification records queue

A **Phone Number Verifications** dashboard is available under **Tools** for users who have the `RunPhoneNumberVerificationsReport` permission. It lists every content item carrying verification data and lets administrators:

- see clickable status tiles (All, Verified, Invalid, Failed, Pending, and Needs attention) that show per-status counts and filter the list when selected,
- search records by raw or normalized phone number,
- sort records by most or least recently attempted, or newest or oldest created,
- review each record's phone number, provider, result, total and failed attempt counts, the last attempt timestamp, and the most recent provider error,
- manually re-queue a record with **Retry now**, which clears the failure counters and due date so the background task picks it up again (requires the `VerifyPhoneNumbers` permission).


Explicit callers are responsible for providing the phone number to verify. After a provider returns a `PhoneNumberVerificationResult`, store it on the content item with `contentItem.AlterPhoneNumberVerificationResult(result, verifiedByUserId, revalidationIntervalDays)`. Consumers can check for existing data with `contentItem.TryGet<PhoneNumberVerificationPart>(out var part)` or read the stored result with `contentItem.TryGetPhoneNumberVerificationResult(out var result)`.

```csharp
var result = await verificationManager.VerifyAsync(phoneNumber, cancellationToken: cancellationToken);

contentItem.AlterPhoneNumberVerificationResult(
    result,
    verifiedByUserId: userId,
    revalidationIntervalDays: settings.RevalidationIntervalDays);
```

## Background revalidation

A daily background task finds content items that already carry a stored phone number and whose verification is due, verifies them in resilient batches, and updates the stored results, the SQL index, and reporting data. The task:

- processes work in batches to scale to large data sets
- tolerates provider failures without stopping the run
- uses distributed locking so it is safe to run across multiple instances

## Caching and cost optimization

External verification APIs are paid services, so the framework minimizes provider calls. Verification data stored on `PhoneNumberVerificationPart` acts as the authoritative cache. A number is only (re)verified when its stored verification has expired or a caller explicitly requests revalidation. Cached results are always read from the content item first.

## Reporting

A report dashboard is available under **Reports** -> **Phone Number Verifications** for users who have the `RunPhoneNumberVerificationsReport` permission. It surfaces operational metrics such as total contacts, verified and unverified numbers, invalid numbers, mobile/landline/VoIP counts, numbers pending verification, numbers requiring revalidation, verification success rate, verification failures, and provider usage counts. The reporting infrastructure is built on the SQL index and is extensible for future dashboard widgets.

![Phone number verifications report dashboard](/img/docs/phone-number-verifications-report.png)

> Screenshot placeholder: the report dashboard.

## Configuration

Configure the module under **Settings** -> **Phone Number Verifications**.

| Setting | Default | Purpose |
| --- | --- | --- |
| **Default provider** | First available | The provider used by default. The selector lists only **enabled** providers. If no provider matches the selection (or none is chosen), the first enabled provider is used. |
| **Revalidation interval (days)** | `365` | The number of days after which a verified number must be revalidated. |
| **Maximum verification attempts** | `3` | The maximum number of consecutive failed verification requests before a record stops auto-retrying and is flagged as **Needs attention** in the records queue. |

![Phone number verifications core settings](/img/docs/phone-number-verifications-settings.png)

> Screenshot placeholder: the core settings page.

Each provider feature contributes its own tab to the same settings page, following the Orchard Core SMS module pattern. Provider tabs only appear when the provider feature is enabled.

Each provider tab includes an **Enable this provider** switch. A provider is only used for verification and only appears in the **Default provider** selector when this switch is on. Turning the switch on reveals the provider's connection and authentication fields, which are then validated when the settings are saved; turning it off hides those fields and skips their validation. If you disable the provider that is currently selected as the default, the default selection is cleared and the framework falls back to the first enabled provider.

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

Reference `CrestApps.OrchardCore.PhoneNumbers.Abstractions`, then implement `IPhoneNumberVerificationProvider` from the `CrestApps.OrchardCore.PhoneNumbers` namespace and map the external response into `PhoneNumberVerificationResult`:

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

Register the provider in a feature `Startup` using the provider key and localized metadata:

```csharp
[Feature("MyCompany.MyModule.MyProvider")]
public sealed class MyProviderStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public MyProviderStartup(IStringLocalizer<MyProviderStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(MyPhoneNumberVerificationProvider));

        services.AddPhoneNumberVerificationProvider<MyPhoneNumberVerificationProvider>(
            "MyProvider",
            options =>
            {
                options.DisplayName = S["My Provider"];
                options.Description = S["Verifies phone numbers using My Provider."];
            });

        // Optional: register a settings display driver for the provider tab.
        services.AddSiteDisplayDriver<MyProviderSettingsDisplayDriver>();
    }
}
```

`AddPhoneNumberVerificationProvider` registers the implementation as a keyed service under the provider key and adds its descriptor so the provider selection setting discovers it automatically. The registration action configures localized provider metadata without allowing the provider key to drift from the keyed service registration.
