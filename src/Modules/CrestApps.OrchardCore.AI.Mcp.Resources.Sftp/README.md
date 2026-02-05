# MCP SFTP Resource Handler

The **SFTP Resource Handler** module provides SFTP (SSH File Transfer Protocol) resource support for the MCP Server, allowing remote files on SFTP servers to be exposed as MCP resources.

## Overview

This module implements the `IMcpResourceTypeHandler` interface to handle `sftp://` URIs. It uses the [SSH.NET](https://github.com/sshnet/SSH.NET) library to connect to SFTP servers and retrieve file content.

## Features

- **SFTP Support**: Connect to SFTP servers using SSH
- **Multiple Authentication**: Support for password and private key authentication
- **Credentials Protection**: Passwords and private keys are encrypted using `IDataProtector` when stored
- **Export Safety**: Sensitive credentials are automatically cleared during deployment export
- **MIME Type Detection**: Automatic MIME type detection based on file extension

## URI Pattern

```
sftp://{itemId}/{path}
```

- `{itemId}`: The unique identifier of the MCP resource
- `{path}`: The path to the file on the SFTP server

## Configuration

When creating an SFTP resource in the admin UI, you can configure:

| Field | Description |
|-------|-------------|
| **Host** | The SFTP server hostname or IP address |
| **Port** | The SSH port (default: 22) |
| **Username** | SSH authentication username |
| **Password** | SSH authentication password (encrypted in storage) |
| **Private Key** | SSH private key for key-based authentication (encrypted in storage) |

## Usage

### Creating an SFTP Resource via Admin UI

1. Navigate to **Artificial Intelligence** → **MCP Resources**
2. Click **Add Resource**
3. Select **SFTP** as the resource type
4. Fill in the connection details:
   - Display Text: A friendly name for the resource
   - URI: `sftp://auto-generated-id/path/to/file.txt`
   - Name: The MCP resource name
   - Host, Port, Username, Password or Private Key
5. Save the resource

### Creating an SFTP Resource via Recipe

```json
{
  "steps": [
    {
      "name": "McpResource",
      "Resources": [
        {
          "Source": "sftp",
          "DisplayText": "Remote Log File",
          "Resource": {
            "Uri": "sftp://resource-id/var/log/app.log",
            "Name": "remote-log",
            "Description": "Application log from SFTP server",
            "MimeType": "text/plain"
          },
          "Properties": {
            "SftpConnectionMetadata": {
              "Host": "ssh.example.com",
              "Port": 22,
              "Username": "user",
              "Password": "",
              "PrivateKey": ""
            }
          }
        }
      ]
    }
  ]
}
```

> **Note**: Passwords and private keys are not exported for security reasons. You must manually set them after importing.

## Authentication Methods

### Password Authentication

Provide a username and password to authenticate with the SFTP server.

### Private Key Authentication

Provide a username and the contents of your private key file. The private key can be:
- RSA key
- DSA key
- ECDSA key
- Ed25519 key

For keys with a passphrase, include the passphrase in the password field.

## Dependencies

This module requires:

- `CrestApps.OrchardCore.AI.Mcp` (MCP Server module)
- [SSH.NET](https://www.nuget.org/packages/SSH.NET/) NuGet package

## Security Considerations

- **Credential Encryption**: Passwords and private keys are encrypted at rest using ASP.NET Core Data Protection
- **Export Safety**: Sensitive credentials are automatically removed during deployment/recipe export
- **Key-Based Auth**: Prefer private key authentication over passwords when possible
- **Key Protection**: Keep private keys secure and consider using passphrase-protected keys
- **Firewall**: Ensure your server can reach the SFTP host on the configured port (typically 22)

## Extending

To create a custom SFTP resource handler, you can implement `IMcpResourceHandler` to handle additional events:

```csharp
public class CustomSftpMcpResourceHandler : IMcpResourceHandler
{
    public void Exporting(ExportingMcpResourceContext context)
    {
        // Handle export customization
    }
}
```

## Related Modules

- [MCP Server](../CrestApps.OrchardCore.AI.Mcp/README.md) - Core MCP Server functionality
- [MCP FTP Resource](../CrestApps.OrchardCore.AI.Mcp.Resources.Ftp/README.md) - FTP/FTPS support
