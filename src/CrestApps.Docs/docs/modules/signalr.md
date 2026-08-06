---
sidebar_label: SignalR
sidebar_position: 2
title: SignalR Feature
description: Seamless SignalR integration within Orchard Core for real-time communication.
---

| | |
| --- | --- |
| **Feature Name** | SignalR |
| **Feature ID** | `CrestApps.OrchardCore.SignalR` |
| **Redis backplane feature ID** | `CrestApps.OrchardCore.SignalR.Redis` |
| **Azure backplane feature ID** | `CrestApps.OrchardCore.SignalR.Azure` |

Provides real-time messaging capabilities using SignalR.

## Creating a Hub

To create a SignalR hub in your module, first install the `Microsoft.AspNetCore.SignalR.Core` package using the NuGet Package Manager. Then, follow the official SignalR documentation to implement your hub.

To register the hub within your module, we recommend utilizing the `HubRouteManager` as shown below:

```csharp
public sealed class ChatStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var hubRouteManager = serviceProvider.GetRequiredService<HubRouteManager>();
        hubRouteManager.MapHub<AIChatHub>(routes);
    }
}
```

## Configuring Hub Options

You can configure options for a specific hub (for example to allow long-running operations
or tune keep-alive settings) by configuring `HubOptions<T>` in `ConfigureServices`.
For example:

```csharp
services.Configure<HubOptions<AIChatHub>>(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(10);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
```

## Generating the Hub URL

To obtain the SignalR hub URL dynamically within a client, inject `HubRouteManager` and generate the link as demonstrated below:

```csharp
@inject HubRouteManager HubRouteManager

var url = HubRouteManager.GetUriByHub<AIChatHub>(ViewContext.HttpContext);
```

Then, initialize the SignalR connection using JavaScript:

```html
<script type="text/javascript" at="Foot" depends-on="signalr">
    document.addEventListener("DOMContentLoaded", function () {
        var connection = new signalR.HubConnectionBuilder()
            .withUrl("@url")
            .build();

        connection.start()
            .then(function () {
                console.log('Connected to SignalR hub!');
            })
            .catch(function (error) {
                console.error('Connection failed:', error.message);
            });
    });
</script>
```

Note the dependency on the `signalr` script, which is automatically added to the page by the SignalR module.

This setup ensures your SignalR hub is properly registered and accessible within Orchard Core, allowing seamless real-time communication.

## Multi-tenant destinations

SignalR backplanes are shared infrastructure, while Orchard user identifiers and application group names are tenant-local. Do not send tenant data through an unqualified `Clients.User(userId)` or a globally named group.

Use `TenantSignalRGroupName.ForUser(shellSettings.Name, userId)` for user destinations and `TenantSignalRGroupName.ForGroup(shellSettings.Name, logicalGroupName)` for application groups. The hub must add only authorized connections to the corresponding tenant-qualified group, and publishers must target the same generated name. This keeps equal user, queue, or supervisor identifiers in different Orchard shells isolated on both single-node and backplane deployments.

## Redis backplane

Enable `CrestApps.OrchardCore.SignalR.Redis` on every tenant that must exchange SignalR messages across application nodes. This feature ships in its own module and NuGet package (`CrestApps.OrchardCore.SignalR.Redis`), so the base SignalR module carries no Redis dependency; installing the Redis backplane package brings in the base module automatically. The feature depends on `OrchardCore.Redis` and uses its `OrchardCore_Redis` connection settings, but creates a dedicated SignalR Redis connection so stopping a hub lifetime manager cannot dispose Orchard's shared cache, bus, or lock connection.

The backplane channel prefix includes both `InstancePrefix` and the immutable Orchard shell name. Two nodes serving the same tenant therefore share one channel namespace, while different tenants never share hub control channels even when they use the same Redis deployment. Application destinations must still use `TenantSignalRGroupName`; channel isolation is defense in depth rather than permission enforcement.

```json
{
  "OrchardCore": {
    "OrchardCore_Redis": {
      "Configuration": "localhost:6379,abortConnect=false",
      "InstancePrefix": "MyApplication:Production:EastUS:"
    }
  }
}
```

Use a deployment-unique `InstancePrefix` that identifies the application, environment, and region whenever Redis infrastructure is shared. Reusing an empty or generic prefix across deployments can merge the backplane channels of tenants with the same shell name.

For multi-node deployments, also enable `OrchardCore.Redis.Lock` when features rely on distributed critical sections, because they require the Redis lock implementation independently of the SignalR backplane.

## Azure SignalR Service backplane

Enable `CrestApps.OrchardCore.SignalR.Azure` to route SignalR traffic through the [Azure SignalR Service](https://learn.microsoft.com/azure/azure-signalr/signalr-overview) instead of hosting the backplane yourself. This offloads connection management and fan-out to Azure and is a convenient scale-out option when Redis infrastructure is not available. The feature depends only on the base SignalR feature.

Provide the Azure SignalR Service connection string under the `CrestApps:SignalR:Azure:ConnectionString` key:

```json
{
  "CrestApps": {
    "SignalR": {
      "Azure": {
        "ConnectionString": "Endpoint=https://<name>.service.signalr.net;AccessKey=<key>;Version=1.0;"
      }
    }
  }
}
```

Store the connection string as a secret (for example in environment variables, user secrets, or a key vault) rather than in a committed `appsettings.json`. When the feature is enabled but no connection string is configured, the backplane is not registered and a warning is written to the log, so the tenant keeps working with the default in-memory backplane instead of failing to start.

Azure SignalR routes each hub through its own service, so hubs mapped with `HubRouteManager` continue to carry the tenant request prefix. Continue to use `TenantSignalRGroupName` for user and application group destinations; it keeps equal identifiers isolated across shells regardless of which backplane is active.

Enable only one backplane per tenant. `CrestApps.OrchardCore.SignalR.Redis` and `CrestApps.OrchardCore.SignalR.Azure` are alternative scale-out providers and are not meant to run together.

## Authenticating with access tokens

Browser clients that are already signed in are authenticated through the regular authentication cookie, and nothing extra is required.

Headless clients, such as single page applications, mobile applications, and service-to-service callers, authenticate with an access token instead. Enable the **OpenID Token Validation** feature (`OrchardCore.OpenId.Validation`) and send the token when connecting. Requests that arrive at an opted-in hub without an authenticated user and with a bearer token are validated using the same `Api` authentication scheme used by the API endpoints, so the same identity works for both the API and the hubs.

Because browsers cannot set an `Authorization` header on a WebSocket handshake, SignalR clients send the token using the standard `access_token` query string parameter. Both forms are supported:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/Communication/Hub/AIChatHub", {
        accessTokenFactory: () => accessToken,
    })
    .build();
```

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://www.example.com/Communication/Hub/AIChatHub", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(accessToken);
    })
    .Build();
```

Requests that already carry an authentication cookie, or that use another authentication scheme, are left untouched. Token validation only runs for hubs that opted in, so the rest of the site, including hubs declared by Orchard Core or by your own application, is unaffected.

The token is only authenticated, not authorized. The identity behind the token still needs the permissions each hub requires, such as `QueryAnyAIProfile` and the [AI tool permissions](../ai/tools#tool-permissions) when the profile uses tools.

### Opting a hub in

Hubs are opt-in. Apply `AllowApiTokenAuthenticationAttribute` to the hub class to allow the `Api` scheme to run for its requests:

```csharp
using CrestApps.OrchardCore.SignalR;
using Microsoft.AspNetCore.SignalR;

[AllowApiTokenAuthentication]
public sealed class MyHub : Hub
{
}
```

The following hubs opt in out of the box:

| Hub | Route |
| --- | --- |
| `AIChatHub` | `/Communication/Hub/AIChatHub` |
| `ChatInteractionHub` | `/Communication/Hub/ChatInteractionHub` |
| `TelephonyHub` | `/Communication/Hub/TelephonyHub` |

The attribute never weakens a hub's authorization requirements. A hub decorated with `[Authorize]` still rejects callers that fail the policy, and a hub that allows anonymous connections still accepts them when no token is supplied. The attribute only allows an otherwise anonymous request that carries a valid bearer token to be associated with the token's user before authorization runs.
