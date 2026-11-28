using CrestApps.OrchardCore.Surveys.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Maps the Surveys API.
/// </summary>
[Feature(SurveysConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapSurveysApi();
}
