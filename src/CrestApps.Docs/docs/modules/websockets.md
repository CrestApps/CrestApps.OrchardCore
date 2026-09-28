---
sidebar_label: WebSockets
sidebar_position: 12
title: WebSockets Feature
description: Enables ASP.NET Core WebSocket hosting per tenant and provides a swappable connection registry for features that host raw WebSocket endpoints.
---

| | |
| --- | --- |
| **Feature Name** | WebSockets |
| **Feature ID** | `CrestApps.OrchardCore.WebSockets` |
| **Enabled by dependency only** | Yes |

The **WebSockets** feature adds the ASP.NET Core WebSocket middleware to the tenant request pipeline and provides a shared, swappable registry for correlating provider-initiated WebSocket callbacks. It is infrastructure: it exists so any feature that needs to host a *raw* WebSocket endpoint can depend on it rather than each enabling WebSocket support on its own.

## Why it exists

The OrchardCore host does not enable the ASP.NET Core WebSocket middleware on its own, and SignalR does not require it (SignalR uses the connection-handler abstraction directly). A feature that accepts a **raw** WebSocket upgrade — `HttpContext.WebSockets.AcceptWebSocketAsync()` — needs `app.UseWebSockets()` to run before endpoint routing, or the upgrade is refused.

This feature is the single place that middleware is enabled, so multiple WebSocket-hosting features never register it more than once, and reusable WebSocket infrastructure has one home.

Because it is **enabled by dependency only**, it never appears as a standalone toggle in the features list. It is switched on automatically when a feature that depends on it is enabled — for example the Telnyx Contact Center [Voice Media](../contact-center/voice-routing.md) adapter, which hosts the WebSocket that Telnyx dials back to.

## What the feature provides

- **WebSocket middleware.** Adds `app.UseWebSockets(...)` to the tenant pipeline, configured from tenant configuration.
- **`IWebSocketConnectionRegistry`.** A rendezvous registry that correlates a provider-initiated WebSocket callback with the request that started it. The starter registers a `WebSocketRendezvous` under an unguessable key, embeds that key in the callback URL, and awaits the socket; the hosting endpoint claims the rendezvous by key when the socket arrives and hands it over. The default implementation is a **per-node in-memory** registry.

## Configuration

The feature binds the tenant configuration section `CrestApps:WebSockets` directly onto the framework `WebSocketOptions`. Every value is optional; when omitted, the ASP.NET Core defaults apply (a two-minute keep-alive interval and no origin restriction).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "WebSockets": {
        "KeepAliveInterval": "00:00:30",
        "KeepAliveTimeout": "00:00:10",
        "AllowedOrigins": [ "https://app.example.com" ]
      }
    }
  }
}
```

### Settings reference

| Setting | Description |
| --- | --- |
| `KeepAliveInterval` | Interval (as a `TimeSpan`, for example `"00:00:30"`) at which the server sends keep-alive ping frames on an idle socket. Defaults to two minutes. |
| `KeepAliveTimeout` | Timeout (as a `TimeSpan`) to wait for a keep-alive pong before aborting the connection. Omit to disable the timeout. |
| `AllowedOrigins` | Allowed `Origin` header values accepted during the WebSocket handshake. When empty, every origin is accepted. Only constrains browser-originated handshakes, which send an `Origin` header. |

## For developers

To host a raw WebSocket endpoint from your own feature:

1. Add a project reference to `CrestApps.OrchardCore.WebSockets` and depend on the `CrestApps.OrchardCore.WebSockets` feature in your module manifest. That guarantees the WebSocket middleware is in the pipeline whenever your feature is enabled.
2. Correlate the provider callback with the request that started it through `IWebSocketConnectionRegistry`:

```csharp
// On the request that asks a provider to dial back:
var key = /* an unguessable, single-use key */;
var rendezvous = await registry.RegisterAsync(key, cancellationToken);
// ... tell the provider to connect to your endpoint with this key in the URL ...
var socket = await rendezvous.ConnectedTask.WaitAsync(timeout, cancellationToken);

// On the endpoint the provider dials back to:
var rendezvous = await registry.TryClaimAsync(keyFromUrl, cancellationToken);
if (rendezvous is null) { /* unknown or already-claimed key: refuse */ }
var accepted = await httpContext.WebSockets.AcceptWebSocketAsync();
rendezvous.TryComplete(accepted);
await rendezvous.ReleasedTask; // keep the request (and socket) alive until the consumer is done
```

The contracts (`IWebSocketConnectionRegistry`, `WebSocketRendezvous`) live in `CrestApps.OrchardCore.WebSockets.Abstractions`, so a consumer depends only on the abstractions and never on the module's host wiring.

## Multi-node deployments

The default `IWebSocketConnectionRegistry` is a **per-node in-memory** registry: a key is only resolvable on the node that registered it. A live WebSocket is a connection terminated at a single node and cannot be moved between nodes, so a callback-agnostic load balancer that routes a provider's callback to a node that did not start the exchange will not find the key, and the open attempt fails.

Two deployment patterns work today:

- **Single node** — the default, correct out of the box.
- **Host affinity** — route each callback back to the node that started it (each node advertising its own public base URL). The socket then lands where the awaiting request is, and the in-memory registry resolves it.

Because `IWebSocketConnectionRegistry` is an abstraction, a distributed implementation (for example one gated on the `OrchardCore.Redis` feature) can replace the default without changing any consumer, for a callback-agnostic multi-node deployment.

## Related documentation

- [Standard Modules overview](index.md)
- [Voice Routing Architecture](../contact-center/voice-routing.md)
