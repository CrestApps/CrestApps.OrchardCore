# MCP FTP/FTPS Resource Handler

The **FTP/FTPS Resource Handler** module provides FTP and FTPS resource support for the MCP Server, allowing remote files on FTP servers to be exposed as MCP resources.

## Overview

This module implements the `IMcpResourceTypeHandler` interface to handle `ftp://` URIs. It uses the [FluentFTP](https://github.com/robinrodricks/FluentFTP) library to connect to FTP servers and retrieve file content.

## Features

- **FTP and FTPS Support**: Connect to both standard FTP and secure FTPS servers
- **Credentials Protection**: Passwords are encrypted using `IDataProtector` when stored
- **Export Safety**: Passwords are automatically cleared during deployment export
- **MIME Type Detection**: Automatic MIME type detection based on file extension

## URI Pattern

```
ftp://{itemId}/{path}
```

- `{itemId}`: The unique identifier of the MCP resource
- `{path}`: The path to the file on the FTP server

## Configuration

When creating an FTP resource in the admin UI, you can configure:

| Field | Description |
|-------|-------------|
| **Host** | The FTP server hostname or IP address |
| **Port** | The FTP port (default: 21) |
| **Username** | FTP authentication username |
| **Password** | FTP authentication password (encrypted in storage) |
| **Use SSL** | Enable FTPS (FTP over SSL/TLS) |

## Usage

### Creating an FTP Resource via Admin UI

1. Navigate to **Artificial Intelligence** → **MCP Resources**
2. Click **Add Resource**
3. Select **FTP/FTPS** as the resource type
4. Fill in the connection details:
   - Display Text: A friendly name for the resource
   - URI: `ftp://auto-generated-id/path/to/file.txt`
   - Name: The MCP resource name
   - Host, Port, Username, Password, Use SSL
5. Save the resource

### Creating an FTP Resource via Recipe

```json
{
  "steps": [
    {
      "name": "McpResource",
      "Resources": [
        {
          "Source": "ftp",
          "DisplayText": "Remote Config File",
          "Resource": {
            "Uri": "ftp://resource-id/config/settings.json",
            "Name": "remote-config",
            "Description": "Configuration file from FTP server",
            "MimeType": "application/json"
          },
          "Properties": {
            "FtpConnectionMetadata": {
              "Host": "ftp.example.com",
              "Port": 21,
              "Username": "user",
              "Password": "",
              "UseSsl": false
            }
          }
        }
      ]
    }
  ]
}
```

> **Note**: Passwords are not exported for security reasons. You must manually set the password after importing.

## Dependencies

This module requires:

- `CrestApps.OrchardCore.AI.Mcp` (MCP Server module)
- [FluentFTP](https://www.nuget.org/packages/FluentFTP/) NuGet package

## Security Considerations

- **Password Encryption**: Passwords are encrypted at rest using ASP.NET Core Data Protection
- **Export Safety**: Passwords are automatically removed during deployment/recipe export
- **FTPS**: Use FTPS (SSL/TLS) when possible for encrypted connections
- **Firewall**: Ensure your server can reach the FTP host on the configured port

## Extending

To create a custom FTP resource handler, you can implement `IMcpResourceHandler` to handle additional events:

```csharp
public class CustomFtpMcpResourceHandler : IMcpResourceHandler
{
    public void Exporting(ExportingMcpResourceContext context)
    {
        // Handle export customization
    }
}
```

## Related Modules

- [MCP Server](../CrestApps.OrchardCore.AI.Mcp/README.md) - Core MCP Server functionality
- [MCP SFTP Resource](../CrestApps.OrchardCore.AI.Mcp.Resources.Sftp/README.md) - SSH File Transfer Protocol support
