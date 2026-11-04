# Phone Numbers Services

Provides phone number parsing, validation, and E.164 formatting services.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Phone Numbers Services | `CrestApps.OrchardCore.PhoneNumbers` | Provides phone number parsing, validation, and E.164 formatting services. Enabled by the features that depend on it. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Extension points

- `IPhoneNumberService` (CrestApps.OrchardCore.PhoneNumbers.Abstractions): Provides phone number parsing, validation, and formatting operations using the E.164 international standard.
- `IPhoneNumberVerificationProvider` (CrestApps.OrchardCore.PhoneNumbers.Abstractions): Represents a single phone number verification provider implementation.
- `IPhoneNumberVerificationProviderSettings` (CrestApps.OrchardCore.PhoneNumbers.Core): Common contract implemented by provider-specific phone number verification settings that support being enabled or disabled from the admin settings UI.
- `IPhoneNumberVerificationHandler` (CrestApps.OrchardCore.PhoneNumbers.Core): Handles events raised during the phone number verification lifecycle.
- `IPhoneNumberVerificationManager` (CrestApps.OrchardCore.PhoneNumbers.Core): Resolves registered phone number verification providers and executes verification requests.
- and 1 more contracts in the same projects.

## Documentation

See [Content Fields](../../CrestApps.Docs/docs/modules/content-fields.md) in the technical manual.
