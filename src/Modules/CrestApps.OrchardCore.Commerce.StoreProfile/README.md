# Store Profile

The store's name, logo, tax ID, contact details and address in one place, under **Settings > Commerce > Store**.
Receipts, the address parcels ship from and the tax origin use it when their own settings are empty.

- Feature: `CrestApps.OrchardCore.Commerce.StoreProfile` (`CommerceConstants.Features.StoreProfile`).
- Settings: `StoreProfileSettings` (Commerce.Abstractions), exported by the **Store profile** deployment step.
- Places: `IStoreLocationService` (Commerce.Core) and `IStoreLocationSource` for features that add places.

See the [Commerce documentation](../../CrestApps.Docs/docs/modules/commerce.md#store-profile).
