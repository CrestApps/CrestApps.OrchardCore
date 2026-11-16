# Model Context Protocol (MCP) FTP Resource

Provides FTP/FTPS resource support for the MCP Server, allowing remote files to be exposed as MCP resources.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Model Context Protocol (MCP) FTP Resource | `CrestApps.OrchardCore.AI.Mcp.Resources.Ftp` | Provides FTP/FTPS resource support for the MCP Server, allowing remote files to be exposed as MCP resources. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `CrestApps.OrchardCore.AI.Mcp.Server`

## Server certificates and TLS

Accepting any server certificate lets anyone on the network path read the credentials and the files, and so does sending a user name and password without TLS (an encryption mode other than Explicit or Implicit). The host's operator allows them in the tenant's configuration, with `CrestApps_AI:FileTransfer:AllowAnyFtpCertificate: true` and `CrestApps_AI:FileTransfer:AllowUnencryptedFtp: true`. The editor refuses either unless it is allowed, and the same settings are bound to CrestApps.Core's `FtpOptions` (`AllowAnyCertificate`, `AllowUnencrypted`), so the FTP client refuses such a connection when it connects, whatever the recipe or deployment step stored. **Upgrading:** a connection saved before with either one fails to connect until it is changed or the host allows it.
## Documentation

See [MCP FTP/FTPS Resource Handler](../../CrestApps.Docs/docs/ai/mcp/ftp.md) in the technical manual.
