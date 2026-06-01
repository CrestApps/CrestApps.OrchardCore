---
sidebar_label: DNC Registry
sidebar_position: 3
title: DNC Registry
description: Configure national do-not-call registry providers and global import enforcement for Omnichannel contact imports.
---

| | |
| --- | --- |
| **Feature Name** | DNC Registry |
| **Feature ID** | `CrestApps.OrchardCore.DncRegistry` |

The **DNC Registry** module provides a shared compliance layer for Omnichannel contact imports. It lets site owners configure national do-not-call registry providers, enforce registry checks globally, and expose additional registry choices during bulk imports.

## Built-in registry integrations

The module currently ships with these provider features:

| Registry | Feature ID | Settings page |
| --- | --- | --- |
| USA FTC Do Not Call Registry | `CrestApps.OrchardCore.DncRegistry.UsaFtc` | **Settings** -> **DNC Registries** -> **USA FTC Registry** |
| Canada LNNTE-DNCL Registry | `CrestApps.OrchardCore.DncRegistry.CanadaDncl` | **Settings** -> **DNC Registries** -> **Canada LNNTE-DNCL Registry** |

Enable the core **DNC Registry** feature first, then enable the provider features you want to use. The **USA FTC Registry** and **Canada LNNTE-DNCL Registry** settings pages are only added to the admin menu when their matching provider feature is enabled.

## Where settings live

The module splits settings by responsibility:

| Location | Purpose |
| --- | --- |
| **Settings** -> **Import Content Settings** | Enforce do-not-call checks globally for imports and choose registries that must always run |
| **Settings** -> **DNC Registries** -> **USA FTC Registry** | Configure USA FTC API access |
| **Settings** -> **DNC Registries** -> **Canada LNNTE-DNCL Registry** | Configure Canada DNCL API access |

When global enforcement is enabled, Omnichannel imports always perform DNC checks even if the importer does not opt in on the import form.

## Provider credentials and protected API keys

Each provider stores its API key as a protected value. After a key is saved:

- the UI does not display the existing key again
- the password field shows a replacement placeholder
- entering a new value replaces the stored key

This keeps the saved credential hidden while still allowing administrators to rotate it.

## Omnichannel import behavior

When **Omnichannel Management** and **Content Transfer** are enabled, Omnichannel contact imports can:

- ignore duplicate rows by phone number
- check duplicate phone numbers against both the current import batch and the contact records that already exist in Orchard before the batch saves
- skip rows whose phone numbers are found on one or more selected registries
- merge importer-selected registries with any registries enforced globally by site settings

Registry checks run in parallel across the selected providers so a single import can compare the same row against multiple external compliance services. Rows skipped because of duplicate phone numbers or DNC matches are added to the import error export together with the skip reason.

## Provider-specific configuration

### USA FTC Do Not Call Registry

Configure the USA FTC provider under **Settings** -> **DNC Registries** -> **USA FTC Registry**.

Current settings:

| Setting | Purpose |
| --- | --- |
| **Organization ID** | The FTC organization identifier used for API requests |
| **Base URL** | The FTC API base address |
| **API key** | The protected credential used to authenticate requests |

To obtain access, follow the FTC registration and API guidance provided by the official National Do Not Call Registry program at [telemarketing.donotcall.gov](https://telemarketing.donotcall.gov/).

### Canada LNNTE-DNCL Registry

Configure the Canada provider under **Settings** -> **DNC Registries** -> **Canada LNNTE-DNCL Registry**.

Current settings:

| Setting | Purpose |
| --- | --- |
| **Account number** | The Canada DNCL account identifier used for lookups |
| **Base URL** | The DNCL API base address |
| **API key** | The protected credential sent in the request header |

To obtain access, follow the official API onboarding guidance at [www.lnnte-dncl.gc.ca/en/Organization/DNCL_API](https://www.lnnte-dncl.gc.ca/en/Organization/DNCL_API).

## Adding a new registry

To integrate another national do-not-call registry:

1. Add a feature or module that references `CrestApps.OrchardCore.DncRegistry.Abstractions`.
2. Implement `INationalDoNotCallRegistry`.
3. Provide a stable `Key`, plus localized `DisplayName` and `Description`.
4. Register the implementation in `Startup` with `services.AddScoped<INationalDoNotCallRegistry, TRegistry>();`.
5. If the provider needs configuration, add its own site settings model, display driver, and admin menu entry under **Settings** -> **DNC Registries**.

```csharp
services.AddScoped<INationalDoNotCallRegistry, MyRegistry>();
```

Each implementation receives the numbers selected during import and returns the subset that the registry reports as listed.
