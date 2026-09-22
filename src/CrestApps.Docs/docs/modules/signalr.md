---
sidebar_label: SignalR
sidebar_position: 2
title: SignalR Feature
description: Orchard Core SignalR migration guidance for CrestApps modules.
---

| | |
| --- | --- |
| **Feature Name** | SignalR |
| **Feature ID** | `OrchardCore.SignalR` |
| **Deprecated compatibility feature ID** | `CrestApps.OrchardCore.SignalR` |
| **Redis backplane feature ID** | `OrchardCore.SignalR.Redis` |
| **Azure backplane feature ID** | `OrchardCore.SignalR.Azure` |

The SignalR module has been migrated into the Orchard Core framework. Use `OrchardCore.SignalR`
and the framework `signalr` script resource for new work. The deprecated CrestApps feature only
exists as a compatibility feature for sites that still need migration.

## CrestApps hub helpers

`SignalRHubRoutes` and `Html.SignalRHubUrl<T>()` are **not** part of the Orchard Core SignalR module. They
ship in `CrestApps.OrchardCore.Core` and are the routing convention the CrestApps hubs follow, so a module
that references that package gets them whichever SignalR feature is enabled.

`SignalRHubRoutes.GetHubPath<T>()` returns `/Communication/Hub/{HubTypeName}`. Views that need the client
URL use `Html.SignalRHubUrl<T>()`, which adds the current request path base so hub links work for tenants
served under a URL prefix.

```csharp
public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
{
    routes.MapHub<MyHub>(SignalRHubRoutes.GetHubPath<MyHub>());
}
```

```cshtml
@{
    var hubUrl = Html.SignalRHubUrl<MyHub>();
}

<script asp-name="my-script" depends-on="signalr" at="Foot"></script>
```

## Multi-tenant destinations

A SignalR backplane is shared infrastructure, while Orchard user identifiers and application group names are
tenant-local. Do not send tenant data through an unqualified `Clients.User(userId)` or a globally named
group.

`TenantSignalRGroupName` (in `CrestApps.OrchardCore.SignalR.Core`) qualifies both:
`TenantSignalRGroupName.ForUser(shellName, userId)` for user destinations and
`TenantSignalRGroupName.ForGroup(shellName, logicalGroupName)` for application groups. The hub adds only
authorized connections to the qualified group, and publishers target the same generated name, so equal user
or group identifiers in different shells stay isolated on single-node and backplane deployments alike.

Learn more in the [Orchard Core SignalR documentation](https://docs.orchardcore.net/en/latest/reference/modules/SignalR/).
