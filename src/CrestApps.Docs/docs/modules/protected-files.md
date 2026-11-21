---
sidebar_label: Protected Files
title: Protected Files
description: Where private uploads such as SCORM packages, coursework submissions and digital downloads are kept, and how to move them to Azure Blob Storage.
---

| | |
| --- | --- |
| **Feature Name** | Protected Files - Azure Blob Storage |
| **Feature ID** | `CrestApps.OrchardCore.ProtectedFiles.Azure` |
| **Category** | Content Management |
| **Depends on** | Nothing |

Protected files are uploads that no public link ever opens. Unlike media library files, a module serves them
itself, after it checks who is asking: for a single launch, or through a short-lived signed link.

## The protected file areas

Each module keeps its protected files in an area of its own:

| Area folder | Files | Module |
| --- | --- | --- |
| `LearningPackages` | SCORM 1.2, SCORM 2004 and cmi5 packages, kept as one cleaned `.zip` file each | [Learning](learning.md) packages |
| `LearningCoursework` | Files learners attach to coursework submissions | Learning coursework |
| `LearningXapiAttachments` | Attachments of xAPI statements | Learning xAPI |
| `LearningEvidence` | Evidence uploaded for required learning | Learning programs |
| `ExternalLearningEvidence` | Evidence uploaded for external learning | Learning certifications |
| `LearningQtiFeedbackImages` | Images of imported QTI feedback | Learning assessments |
| `DigitalFiles` | Files sold as [digital downloads](downloads.md) | Digital Downloads |

By default every area is a folder of the tenant's application data: `App_Data/Sites/{tenant}/{area folder}`.
Each file is in a subfolder named after its file id.

## Running on more than one instance

A single instance with a persistent disk needs nothing here. When the site runs on more than one instance (a
scaled-out App Service plan, several containers behind a load balancer), each instance only sees the files
uploaded to it, and a container whose `App_Data` folder is not on persistent storage loses them when it is
redeployed. Enable **Protected Files - Azure Blob Storage** to keep every area in one blob container that every
instance reads.

### Store protected files in Azure Blob Storage

The feature has no admin screen. It is configured through the `CrestApps:ProtectedFiles:AzureBlobStorage` shell
configuration section, which sits under the `OrchardCore` key in the application's root `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ProtectedFiles": {
        "AzureBlobStorage": {
          "ConnectionString": "",
          "ContainerName": "protected-files",
          "BasePath": "{{ ShellSettings.Name }}",
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
| `ContainerName` | Azure Blob container name. **Required.** Supports Liquid, so `protected-files-{{ ShellSettings.Name }}` gives each tenant its own container. |
| `BasePath` | Optional folder inside the container. Each area is kept in a folder of its own below it, for example `Contoso/LearningPackages`. Supports Liquid, so `{{ ShellSettings.Name }}` separates tenants that share a container. |
| `CreateContainer` | When `true` (the default), the container is created when the tenant starts if it does not exist. It is created without public access. |
| `RemoveContainer` | When `true`, the container is removed when the tenant is deleted. |
| `RemoveFilesFromBasePath` | Removes only the configured `BasePath` contents when the tenant is deleted. Use this instead of `RemoveContainer` when the container is shared. |

:::warning
`ConnectionString` and `ContainerName` are both required. When either is missing, the feature stays enabled but
**does not take over storage**: an error is written to the log and the files stay in `App_Data`.
:::

Files already in `App_Data` are not moved. Before you enable the feature on a site that has protected files,
copy each area folder to the container, keeping the same layout: `App_Data/Sites/Contoso/LearningPackages/{id}/{file}`
becomes `{BasePath}/LearningPackages/{id}/{file}`. AzCopy does this in one command per area.

The container should not allow public access: protected files are only served by the site, after its own checks.

### Learning packages on Azure Blob Storage

A SCORM or cmi5 launch reads each file the package asks for straight out of its `.zip` file, and Azure Blob
Storage can't read part of a `.zip` file in place. The first launch of a package on an instance therefore
downloads the package once into `App_Data/Sites/{tenant}/LearningPackagesCache`, and every later file of every
launch on that instance is read from that copy. Replacing a package stores a new file, so a copy never goes out
of date; copies nobody opened for 14 days are removed. Allow for the disk space of the packages learners use.

## Adding an area

A module registers an area with the folder it is kept in, and resolves `IProtectedFileStore<TArea>` to save, open
and delete files:

```csharp
public sealed class CourseworkFiles
{
}

services.AddProtectedFileArea<CourseworkFiles>("LearningCoursework");
```

The area follows the tenant's storage on its own: when **Protected Files - Azure Blob Storage** is enabled, it is
kept in the container like every other area. To keep protected files somewhere else, replace
`IProtectedFileAreaStoreFactory`, which returns the `IFileStore` of an area from its folder name.
