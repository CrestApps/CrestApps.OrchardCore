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

## Authenticating with access tokens

Browser clients that are already signed in are authenticated through the regular authentication cookie, and nothing extra is required.

Headless clients, such as single page applications, mobile applications, and service-to-service callers, authenticate with an access token instead. Enable the **OpenID Token Validation** feature (`OrchardCore.OpenId.Validation`) and send the token when connecting. Hub requests that arrive without an authenticated user and with a bearer token are validated using the same `Api` authentication scheme used by the API endpoints, so the same identity works for both the API and the hubs.

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

Requests that already carry an authentication cookie, or that use another authentication scheme, are left untouched. Token validation only runs for hub endpoints, so the rest of the site is unaffected.

The token is only authenticated, not authorized. The identity behind the token still needs the permissions each hub requires, such as `QueryAnyAIProfile` and the [AI tool permissions](../ai/tools#tool-permissions) when the profile uses tools.
