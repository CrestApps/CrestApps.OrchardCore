# Protected Files - Azure Blob Storage

Stores protected files in Azure Blob Storage instead of the local file system, so every instance of a scaled-out site reads the same files. Protected files are the files no public link opens: learning packages (SCORM and cmi5), coursework submission files, xAPI statement attachments, learning evidence, QTI feedback images and digital downloads.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Protected Files - Azure Blob Storage | `CrestApps.OrchardCore.ProtectedFiles.Azure` | Stores protected files in Azure Blob Storage instead of the local file system. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Configuration

The feature reads the `CrestApps:ProtectedFiles:AzureBlobStorage` shell configuration section. `ConnectionString` and `ContainerName` are required; each area is kept in a folder of its own below `BasePath`.

## Documentation

See [Protected files](../../CrestApps.Docs/docs/modules/protected-files.md) in the technical manual.
