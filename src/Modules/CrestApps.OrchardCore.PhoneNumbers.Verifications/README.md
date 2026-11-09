# Phone Number Verifications

Provides a provider-agnostic framework for verifying phone numbers, storing results on contact content items, and background revalidation.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Phone Number Verifications | `CrestApps.OrchardCore.PhoneNumbers.Verifications` | Provides a provider-agnostic framework for verifying phone numbers, storing results on contact content items, and background revalidation. Enabled by the features that depend on it. |
| AbstractAPI Phone Number Verification | `CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi` | Verifies phone numbers using the AbstractAPI Phone Validation service. |
| Veriphone Phone Number Verification | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Veriphone` | Verifies phone numbers using the Veriphone phone number validation service. |
| Twilio Phone Number Verification | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio` | Verifies phone numbers using the Twilio Lookup service. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `OrchardCore.Contents`
- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)
- [`CrestApps.OrchardCore.PhoneNumbers`](../CrestApps.OrchardCore.PhoneNumbers/README.md)

## Documentation

See [Phone Number Verifications](../../CrestApps.Docs/docs/modules/phone-number-verifications.md) in the technical manual.
