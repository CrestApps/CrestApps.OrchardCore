# WebSockets

Enables ASP.NET Core WebSocket hosting for the tenant and provides a home for reusable WebSocket infrastructure.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| WebSockets | `CrestApps.OrchardCore.WebSockets` | Adds the ASP.NET Core WebSocket middleware to the tenant pipeline so features can host raw WebSocket endpoints. Enabled automatically as a dependency of features that need it. Enabled by the features that depend on it. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Extension points

- `IWebSocketConnectionRegistry` (CrestApps.OrchardCore.WebSockets.Abstractions): Correlates a provider-initiated WebSocket callback with the request that started it.

## Documentation

See [WebSockets Feature](../../CrestApps.Docs/docs/modules/websockets.md) in the technical manual.
