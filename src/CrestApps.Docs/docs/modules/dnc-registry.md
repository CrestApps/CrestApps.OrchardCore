---
sidebar_label: DNC Registry
sidebar_position: 3
title: DNC Registry
description: Configure national do-not-call registry providers and global import enforcement for Omnichannel contact imports.
user_manual:
  - user-manual/administration/do-not-call-lists
---

| | |
| --- | --- |
| **Feature Name** | DNC Registry |
| **Feature ID** | `CrestApps.OrchardCore.DncRegistry` |

The **DNC Registry** module provides a shared compliance layer for Omnichannel contact imports. It lets site owners configure national do-not-call registry providers, enforce registry checks globally, and expose additional registry choices during bulk imports. The same registries screen outbound dialing in the Contact Center.

:::tip[Using the do-not-call screens]
The step-by-step instructions for administrators (uploading and managing local lists, preparing the CSV file,
enforcing checks on every import, and entering registry credentials) are in the User Manual:
[Do Not Call Lists and Registries](../user-manual/administration/do-not-call-lists.md). This page covers the
features, settings objects, storage, screening semantics and extension points.
:::

## Built-in registry integrations

The module ships with these provider features:

| Registry | Feature ID | Admin location |
| --- | --- | --- |
| USA FTC Do Not Call Registry | `CrestApps.OrchardCore.DncRegistry.UsaFtc` | **Settings** -> **DNC Registries** -> **USA FTC Registry** |
| Canada LNNTE-DNCL Registry | `CrestApps.OrchardCore.DncRegistry.CanadaDncl` | **Settings** -> **DNC Registries** -> **Canada LNNTE-DNCL Registry** |
| Local Do Not Call Registry | `CrestApps.OrchardCore.DncRegistry.Local` | **Interaction Center** -> **Local DNC Registry** |
| DNC Registry - Azure Blob Storage | `CrestApps.OrchardCore.DncRegistry.Azure` | Configuration only -- see [below](#store-local-registry-files-in-azure-blob-storage) |

Enable the core **DNC Registry** feature first, then enable the provider features you want to use. The **USA FTC Registry**, **Canada LNNTE-DNCL Registry**, and **Local DNC Registry** admin pages are only added to the admin menu when their matching provider feature is enabled.

## Where settings live

The module splits settings by responsibility:

| Location | Purpose |
| --- | --- |
| **Settings** -> **Content Import** | Enforce do-not-call checks globally for imports and choose registries that must always run |
| **Settings** -> **DNC Registries** -> **USA FTC Registry** | Configure USA FTC API access |
| **Settings** -> **DNC Registries** -> **Canada LNNTE-DNCL Registry** | Configure Canada DNCL API access |
| **Interaction Center** -> **Local DNC Registry** | Manage locally uploaded DNC lists |

The settings pages and the Local DNC Registry screen require the **Manage DNC registry settings** (`ManageDncRegistrySettings`) permission.

When global enforcement is enabled, Omnichannel imports always perform DNC checks even if the importer does not opt in on the import form.

The same settings can be provisioned through the Recipes module's generic `Settings` step by using the `DncRegistrySettings`, `UsaFtcDncRegistrySettings`, and `CanadaDnclRegistrySettings` objects when the matching features are enabled. `EnforcedRegistryKeys` takes the registry keys: `usa-ftc-dnc` (USA FTC), `canada-lnnte-dncl` (Canada LNNTE-DNCL), and `local-dnc` (Local Do Not Call Registry).

```json
{
  "steps": [
    {
      "name": "settings",
      "DncRegistrySettings": {
        "EnforceGlobally": true,
        "EnforcedRegistryKeys": ["usa-ftc-dnc", "canada-lnnte-dncl"]
      },
      "UsaFtcDncRegistrySettings": {
        "OrganizationId": "example-org",
        "BaseUrl": "https://telemarketing.donotcall.gov/api/",
        "ProtectedApiKey": "encrypted-api-key"
      }
    }
  ]
}
```

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
- skip rows whose phone numbers are found on one or more selected registries, or import them marked Do not call instead
- merge importer-selected registries with any registries enforced globally by site settings

Registry checks run in parallel across the selected providers so a single import can compare the same row against multiple external compliance services. Rows skipped because of duplicate phone numbers or DNC matches are added to the import error export together with the skip reason.

## Local Do Not Call Registry

The **Local DNC Registry** feature (`CrestApps.OrchardCore.DncRegistry.Local`) allows administrators to maintain do-not-call lists directly within the application without relying on external API services.

### Key features

- **CSV upload**: Import phone numbers from CSV files with automatic E.164 normalization
- **Background processing**: Uploads return quickly and continue processing after the request ends
- **Progress reporting**: Each uploaded list shows total rows, processed rows, successes, and errors while the import runs
- **Country-based organization**: Each list is associated with a specific country (ISO 3166-1 alpha-2 code)
- **Filtering by country**: When checking numbers, you can filter against a specific country's list or check all countries
- **List replacement**: Delete old lists and upload replacements (e.g., monthly DNC updates)
- **Phone number normalization**: Uploaded phone numbers are normalized to [E.164](https://en.wikipedia.org/wiki/E.164) format (`+<country code><subscriber number>`, e.g., `+17024993350`) using [libphonenumber](https://github.com/twcclegg/libphonenumber-csharp). This globally unique format eliminates ambiguity between countries and provides a consistent comparison key.

### Local lists and background processing

Lists are managed under **Interaction Center** -> **Local DNC Registry** (requires `ManageDncRegistrySettings`). An
upload saves a `LocalDncList` with the **Pending** status and returns immediately; the rows are imported in the
background. Each list tracks total, processed, successful and rejected rows, and the rejected rows (with a reason
each) can be downloaded as CSV. The operator actions (**Process now**, **Pause import**, **Delete**, **Download
errors**) are described in the [User Manual](../user-manual/administration/do-not-call-lists.md#upload-a-local-list).

A background task (**Local DNC Import Processor**) runs every 10 minutes and keeps lists from getting stuck:

- It starts **Pending** imports.
- It resumes a **Processing** import or a **Deleting** list that has saved no progress for 10 minutes, for example after the site restarted. The work continues where it stopped rather than starting over.
- It retries a **Failed** import up to 5 times, waiting 10 minutes longer after each failure. After that, the list stays failed until someone uses **Process now**, which also gives it 5 more automatic retries. An import whose uploaded file is missing is not retried.

A list's stored status is **Pending**, **Processing**, **Completed**, **Failed**, **Paused**, or **Deleting**. The admin list shows a completed list that has rejected rows as **Completed with errors**; it is still stored as **Completed**.

Only **Completed** lists are used for screening; a list that is still importing, paused, or failed is not consulted. Uploaded files are stored under `App_Data/Sites/{tenant}/DncRegistry` unless [Azure Blob Storage](#store-local-registry-files-in-azure-blob-storage) is enabled.

The import and deletion of lists are paced by the shared `CrestApps:BackgroundWork:Pacing` settings described in [Content Transfer](content-transfer.md#limiting-how-much-of-the-database-an-import-uses).

### CSV file format

The CSV file should contain a single column with one phone number per row. If the file comes from a spreadsheet, keep the data on a single worksheet and export that worksheet as CSV.

The importer automatically:
- skips blank rows
- skips a header row
- skips duplicate phone numbers within the same file
- skips invalid phone numbers
- tracks row-level errors and success counts

Rows with multiple populated columns are rejected because the file must contain phone numbers only in a single column. When a row omits the leading `+`, the importer uses the selected country as the region context to parse and convert the number to E.164 format using `IPhoneNumberService`. Numbers that cannot be parsed into valid E.164 format are rejected.

Example CSV:
```csv
555-123-4567
(555) 234-5678
5559876543
```

### Numbers are canonical before a registry sees them

`INationalDoNotCallRegistry` takes and returns `PhoneNumber` values, which are in E.164 form by construction. A registry is therefore never asked about a number in a shape it has to interpret, and it never has to normalize a number a second time.

That is deliberate. A caller that hands a registry a raw string forces the registry to guess what was meant, and two pieces of code guessing separately will eventually guess differently: the caller screens one number while the registry looks up another, the lookup matches nothing, and the caller reads the empty result as "not listed". Requiring the canonical type at the boundary removes the guess. Canonicalization happens once, in the caller, through `IPhoneNumberService.TryParse`.

### Screening fails closed

A registry that cannot complete a lookup raises `DoNotCallScreeningException` instead of returning an empty result. The distinction is the whole point: a registry that answers "not listed" has screened the number, while a registry that is unreachable, misconfigured, or rejecting requests has said nothing at all. Returning an empty set for the second case makes silence indistinguishable from a clean answer, and every caller acting on it proceeds as though the number had been cleared.

Callers honour that signal rather than ignoring it. The outbound dialer suppresses the attempt with `ComplianceScreeningUnavailable`, which is not terminal, so the activity stays available for a later cycle. The contact import skips the row and reports why, so an unscreened number is never imported as one the registries cleared.

A registry with no credentials configured is a different situation again: it is not participating in screening, returns an empty result, and does not raise. Configure only the registries whose answers you intend to rely on.

### Country filtering with NumberSearchContext

The `INationalDoNotCallRegistry` interface supports a `NumberSearchContext` parameter that allows filtering by country:

```csharp
var context = new NumberSearchContext
{
    CountryCode = "US",
};

var registered = await registry.GetRegisteredNumbersAsync(phoneNumbers, context);
```

When no `CountryCode` is specified (or the context is `null`), the local registry checks against all uploaded lists regardless of country.

## Provider-specific configuration

Each national registry is configured on its own page under **Settings** -> **DNC Registries**. The field-by-field
reference is in the [User Manual](../user-manual/administration/do-not-call-lists.md#connect-a-national-registry).

| Registry | Settings object | Properties | Default `BaseUrl` |
| --- | --- | --- | --- |
| USA FTC Do Not Call Registry | `UsaFtcDncRegistrySettings` | `OrganizationId`, `BaseUrl`, `ProtectedApiKey` | `https://telemarketing.donotcall.gov/api/` |
| Canada LNNTE-DNCL Registry | `CanadaDnclRegistrySettings` | `AccountNumber`, `BaseUrl`, `ProtectedApiKey` | `https://www.lnnte-dncl.gc.ca/api/` |

`BaseUrl` is required when the settings are saved from the admin screen. A registry with no credentials does not
take part in screening (see [Screening fails closed](#screening-fails-closed)).

To obtain access, follow the official programs: the FTC National Do Not Call Registry at
[telemarketing.donotcall.gov](https://telemarketing.donotcall.gov/), and the Canada DNCL API onboarding at
[www.lnnte-dncl.gc.ca/en/Organization/DNCL_API](https://www.lnnte-dncl.gc.ca/en/Organization/DNCL_API).

## Store local registry files in Azure Blob Storage

| | |
| --- | --- |
| **Feature Name** | DNC Registry - Azure Blob Storage |
| **Feature ID** | `CrestApps.OrchardCore.DncRegistry.Azure` |

By default the **Local Do Not Call Registry** keeps uploaded list files on the local file system. Enable
**DNC Registry - Azure Blob Storage** to keep them in an Azure Blob container instead, which is what a
multi-instance deployment needs so every instance reads the same uploads.

The feature depends on **Local Do Not Call Registry** and has no admin screen of its own. It is configured
entirely through the `CrestApps:DncRegistry:AzureBlobStorage` shell configuration section, which sits under
the `OrchardCore` key in the application's root `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "DncRegistry": {
        "AzureBlobStorage": {
          "ConnectionString": "",
          "ContainerName": "dnc-registry",
          "BasePath": "some/base/path",
          "CreateContainer": true
        }
      }
    }
  }
}
```

| Setting | Description |
| --- | --- |
| `ConnectionString` | Azure Storage account connection string. **Required.** |
| `ContainerName` | Azure Blob container name. **Required.** Supports Liquid, so `{{ ShellSettings.Name }}-dnc-registry` gives each tenant its own container. See [Separating tenants](#separating-tenants). |
| `BasePath` | Optional subdirectory inside the container where registry files are stored. Supports Liquid, so `{{ ShellSettings.Name }}` separates tenants that share a container. |
| `CreateContainer` | When `true`, the container is created automatically if it does not already exist. |
| `RemoveContainer` | When `true`, the container is removed when the tenant is deleted. |
| `RemoveFilesFromBasePath` | Removes only the configured `BasePath` contents when the tenant is deleted. Use this instead of `RemoveContainer` when the container is shared. |

:::warning
`ConnectionString` and `ContainerName` are both required. When either is missing, the feature stays enabled
but **does not take over storage** — an error is written to the log and the local file system keeps serving
the registry files.
:::

### Separating tenants

`ContainerName` and `BasePath` both accept Liquid, with the tenant's `ShellSettings` available, so one
configuration in the root `appsettings.json` serves every tenant. There are two ways to keep tenants apart:

- **A container per tenant.** Set `ContainerName` to a template such as
  `{{ ShellSettings.Name }}-dnc-registry` and leave `BasePath` empty. Each tenant's registry files are in a
  container of their own, created automatically when `CreateContainer` is `true`, and access policies or
  lifecycle rules can be applied to one tenant at a time. Set `RemoveContainer` to `true` to delete the
  tenant's container when the tenant is deleted.
- **One shared container.** Keep a fixed `ContainerName` and set `BasePath` to `{{ ShellSettings.Name }}`.
  Every tenant's registry files are in the same container, separated by a folder prefix. Use
  `RemoveFilesFromBasePath`, not `RemoveContainer`, so deleting one tenant does not delete the other tenants'
  files.

```json
{
  "ContainerName": "{{ ShellSettings.Name }}-dnc-registry",
  "CreateContainer": true
}
```

The resolved container name is lowercased and must be a valid Azure container name: 3 to 63 characters,
lowercase letters, digits and single hyphens, starting and ending with a letter or digit. A tenant name that
breaks these rules, for example one with an underscore, resolves to an invalid name, and an error is logged
when the tenant starts. Use the shared container for such tenants.

:::note
A tenant that overrides these values in its own `App_Data/Sites/{tenant}/appsettings.json` writes the same
keys **without** the `OrchardCore` wrapper, starting at `CrestApps`, because that file is already scoped to
the tenant.
:::

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

Each implementation receives the canonical numbers selected during import and returns the subset that the registry reports as listed.

### Supporting country-based filtering

Implementations can override the `GetRegisteredNumbersAsync` overload that accepts a `NumberSearchContext` to support country-based filtering:

```csharp
public Task<HashSet<PhoneNumber>> GetRegisteredNumbersAsync(
    IEnumerable<PhoneNumber> phoneNumbers,
    NumberSearchContext context,
    CancellationToken cancellationToken = default)
{
    // Filter by context.CountryCode if provided
}
```

A registry that cannot express a number in the form its upstream API expects — for example a non-NANP number sent to a North American registry — must skip that number rather than send an altered one. Asking a registry about a number other than the one being dialed produces an answer about a different person.

The default interface implementation delegates to the parameterless overload, so existing registries continue to work without modification.
