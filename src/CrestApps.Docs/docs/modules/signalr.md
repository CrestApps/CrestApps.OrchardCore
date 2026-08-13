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

Views that need a hub URL can use `Html.SignalRHubUrl<T>()`. The helper adds the current request
path base, so hub links work for tenants that use a URL prefix.

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

Learn more in the [Orchard Core SignalR documentation](https://docs.orchardcore.net/en/latest/reference/modules/SignalR/).
