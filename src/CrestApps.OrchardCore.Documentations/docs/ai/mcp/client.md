---
sidebar_label: MCP Client Integration
sidebar_position: 2
title: MCP Client Integration
description: Connect to remote and local MCP servers using SSE or Stdio transports.
---

# MCP Client Integration

The MCP Client features allow your Orchard Core application to connect to external MCP servers, enabling AI models to leverage additional tools and resources provided by those servers.

Two transport types are supported:

| Transport | Feature ID | Description |
|---|---|---|
| **Server-Sent Events (SSE)** | `CrestApps.OrchardCore.AI.Mcp` | Connect to remote MCP servers over HTTP. |
| **Standard Input/Output (Stdio)** | `CrestApps.OrchardCore.AI.Mcp.Stdio` | Connect to local MCP servers (e.g., Docker containers). |

---

## SSE Transport (Remote Servers)

| | |
| --- | --- |
| **Feature Name** | Model Context Protocol (MCP) Client |
| **Feature ID** | `CrestApps.OrchardCore.AI.Mcp` |

The **MCP Client Feature** enables your application to connect to remote MCP servers using standard HTTP requests with **Server-Sent Events (SSE)** transport, which allows real-time data flow between LLMs and external services.

### Connect to a Remote MCP Server

1. Open your Orchard Core project.
2. Navigate to **Artificial Intelligence** → **MCP Connections**.
3. Click the **Add Connection** button.
4. Under the **Server Sent Events (SSE)** source, click **Add**.
5. Enter the following connection details:
   - **Display Text**: A descriptive name for the connection.
   - **Endpoint**: The MCP server endpoint URL (e.g., `https://localhost:1234/`).
   - **Authentication**: Select the authentication method (see below).
6. Save the connection.

### Authentication Types

When configuring an SSE connection, you can choose from the following authentication methods:

| Authentication Type | Description |
|---|---|
| **Anonymous** | No authentication required. Use for development or servers with no auth. |
| **API Key** | Send an API key in a configurable HTTP header with an optional prefix. |
| **Basic Authentication** | Standard HTTP Basic auth using username and password. |
| **OAuth 2.0 Client Credentials** | Obtain an access token via the OAuth 2.0 client credentials flow. |
| **OAuth 2.0 + Private Key JWT** | Authenticate using a signed JWT client assertion with the client credentials flow. |
| **OAuth 2.0 + Mutual TLS (mTLS)** | Authenticate using a client certificate for mutual TLS with the client credentials flow. |
| **Custom Headers** | Provide raw HTTP headers as JSON for advanced scenarios. |

#### API Key

Provide the API key value and optionally configure:
- **Header Name**: The HTTP header name (defaults to `Authorization`).
- **Key Prefix**: A prefix prepended to the key (e.g., `Bearer`, `ApiKey`).

#### Basic Authentication

Provide a **Username** and **Password**. The credentials are Base64-encoded and sent in the `Authorization` header.

#### OAuth 2.0 Client Credentials

Configure the following:
- **Token Endpoint**: The OAuth 2.0 token URL (e.g., `https://auth.example.com/oauth2/token`).
- **Client ID**: The application client identifier.
- **Client Secret**: The application secret.
- **Scopes**: Optional space-separated list of scopes.

The module automatically acquires and caches access tokens using the `client_credentials` grant type.

#### OAuth 2.0 + Private Key JWT

Configure the following:
- **Token Endpoint**: The OAuth 2.0 token URL.
- **Client ID**: The application client identifier.
- **Private Key (PEM)**: The RSA private key in PEM format used to sign the JWT client assertion.
- **Key ID (kid)**: Optional key identifier included in the JWT header (required by some identity providers).
- **Scopes**: Optional space-separated list of scopes.

The module creates a signed JWT assertion using the private key and sends it to the token endpoint using the `urn:ietf:params:oauth:client-assertion-type:jwt-bearer` assertion type.

#### OAuth 2.0 + Mutual TLS (mTLS)

Configure the following:
- **Token Endpoint**: The OAuth 2.0 token URL.
- **Client ID**: The application client identifier.
- **Client Certificate (Base64 PFX)**: The Base64-encoded PFX/PKCS#12 client certificate.
- **Certificate Password**: Optional password for the PFX certificate file.
- **Scopes**: Optional space-separated list of scopes.

The module authenticates to the token endpoint using the client certificate for mutual TLS authentication.

#### Custom Headers

For advanced scenarios, provide a JSON object of HTTP header key-value pairs. This is the legacy approach and is useful when none of the standard authentication types fit your needs.

:::note
All sensitive credentials (API keys, passwords, client secrets, private keys, client certificates) are **encrypted at rest** using ASP.NET Core Data Protection and are **never included** in deployment exports.
:::

### SSE Recipe-Based Setup

You can also configure the SSE connection programmatically using a recipe:

```json
{
  "steps": [
    {
      "name": "McpConnection",
      "connections": [
        {
          "DisplayText": "Example server",
          "Properties": {
            "SseMcpConnectionMetadata": {
              "Endpoint": "https://localhost:1234/",
              "AuthenticationType": "ApiKey",
              "ApiKeyHeaderName": "Authorization",
              "ApiKeyPrefix": "Bearer",
              "ApiKey": "your-api-key-here"
            }
          }
        }
      ]
    }
  ]
}
```

:::warning
Sensitive values (ApiKey, BasicPassword, OAuth2ClientSecret, OAuth2PrivateKey, OAuth2ClientCertificate, OAuth2ClientCertificatePassword) provided in recipes are encrypted upon import. Do not include already-encrypted values in recipe files.
:::

---

## Stdio Transport (Local Servers)

| | |
| --- | --- |
| **Feature Name** | Model Context Protocol (MCP) Local Client |
| **Feature ID** | `CrestApps.OrchardCore.AI.Mcp.Stdio` |

The **Local MCP Client Feature** allows your application to connect to MCP servers running locally, typically in containers. It uses **Standard Input/Output (Stdio)** for communication — ideal for offline tools or running local services.

### Example: Global Time Capabilities with `mcp/time`

Let's equip your AI model with time zone intelligence using the [`mcp/time`](https://hub.docker.com/r/mcp/time) Docker image.

#### Step 1: Install Docker Desktop

Download and install [Docker Desktop](https://www.docker.com/products/docker-desktop), then launch the app.

#### Step 2: Pull the MCP Docker Image

1. Open Docker Desktop.
2. Search for `mcp/time` in the **Docker Hub** tab.
3. Click on the image and hit **Pull**.

#### Step 3: Add the Connection via Orchard Core

1. Open your Orchard Core project.
2. Navigate to **Artificial Intelligence** → **MCP Connections**.
3. Click the **Add Connection** button.
4. Under the **Standard Input/Output (Stdio)** source, click **Add**.
5. Enter the following connection details:
   - **Display Text**: `Global Time Capabilities`
   - **Command**: `docker`
   - **Command Arguments**:
     ```json
     ["run", "-i", "--rm", "mcp/time"]
     ```

💡 These arguments are based on the official usage from the [`mcp/time` Docker Hub page](https://hub.docker.com/r/mcp/time).

### Stdio Recipe-Based Setup

Prefer configuration through code? Here's how to define the same connection using a recipe:

```json
{
  "steps": [
    {
      "name": "McpConnection",
      "connections": [
        {
          "DisplayText": "Global Time Capabilities",
          "Properties": {
            "StdioMcpConnectionMetadata": {
              "Command": "docker",
              "Arguments": [
                "run",
                "-i",
                "--rm",
                "mcp/time"
              ]
            }
          }
        }
      ]
    }
  ]
}
```

---

## Create an AI Profile

After adding an MCP connection (SSE or Stdio), create an AI profile that uses it:

👉 [Learn how to create an AI Profile](../overview#creating-ai-profiles)
