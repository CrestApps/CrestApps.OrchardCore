using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Dispatch.Api;
using CrestApps.OrchardCore.WorkOrders;
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
[RequireFeatures(AssetsConstants.Features.Assets)]
public sealed class AssetsApiStartup : StartupBase
{
    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAssetsApi();
    }
}

/// <summary>
/// Maps the service agreement reads of the HTTP API, when Service Agreements is on.
/// </summary>
[Feature(DispatchConstants.Features.Api)]
[RequireFeatures(WorkOrdersConstants.Features.Agreements)]
public sealed class AgreementsApiStartup : StartupBase
{
    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapServiceAgreementsApi();
    }
}
