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

## Server certificates

Accepting any server certificate lets anyone on the network path read the credentials and the files, so the editor refuses it unless the host's operator allows it with `CrestApps_AI:FileTransfer:AllowAnyFtpCertificate: true` in the tenant's configuration. Connections saved with it before keep it until they are edited; the check at connection time is in CrestApps.Core.

## Documentation

See [MCP FTP/FTPS Resource Handler](../../CrestApps.Docs/docs/ai/mcp/ftp.md) in the technical manual.
