using CrestApps.OrchardCore.Dispatch.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// Maps the field service HTTP API.
/// </summary>
[Feature(DispatchConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapFieldServiceApi();
    }
}

/// <summary>
/// Maps the assets HTTP API, when Assets is on.
/// </summary>
[Feature(DispatchConstants.Features.Api)]
[RequireFeatures("CrestApps.OrchardCore.Assets")]
public sealed class AssetsApiStartup : StartupBase
{
    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAssetsApi();
    }
}
