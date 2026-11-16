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

## Server certificates and TLS

Accepting any server certificate lets anyone on the network path read the credentials and the files, and so does sending a user name and password without TLS (an encryption mode other than Explicit or Implicit). The host's operator allows them in the tenant's configuration, with `CrestApps_AI:FileTransfer:AllowAnyFtpCertificate: true` and `CrestApps_AI:FileTransfer:AllowUnencryptedFtp: true`. The editor refuses either unless it is allowed, and the same settings are bound to CrestApps.Core's `FtpOptions` (`AllowAnyCertificate`, `AllowUnencrypted`), so the FTP client refuses such a connection when it connects, whatever the recipe or deployment step stored. **Upgrading:** a connection saved before with either one fails to connect until it is changed or the host allows it.
## Documentation

See [AI File Sources](../../CrestApps.Docs/docs/ai/file-sources.md) in the technical manual.
