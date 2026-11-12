# AI File Sources - FTP

Adds an FTP/FTPS connector to File Sources, so a folder on an FTP server can be ingested into a File AI data source.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| AI File Sources - FTP | `CrestApps.OrchardCore.AI.FileSources.Ftp` | Adds an FTP/FTPS connector to File Sources, so a folder on an FTP server can be ingested into a File AI data source. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.AI.FileSources`](../CrestApps.OrchardCore.AI.FileSources/README.md)

## Server certificates

Accepting any server certificate lets anyone on the network path read the credentials and the files, so the editor refuses it unless the host's operator allows it with `CrestApps_AI:FileTransfer:AllowAnyFtpCertificate: true` in the tenant's configuration. The check is in the editor only: connections saved with it before keep it until they are edited, and the recipe and deployment steps store it as given. The check at connection time comes with CrestApps.Core #322 (`FtpOptions.AllowAnyCertificate`).

## Documentation

See [AI File Sources](../../CrestApps.Docs/docs/ai/file-sources.md) in the technical manual.
