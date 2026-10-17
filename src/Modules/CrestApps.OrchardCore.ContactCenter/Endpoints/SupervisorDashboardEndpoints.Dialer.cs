using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static partial class SupervisorDashboardEndpoints
{
    internal static async Task<IResult> HandleDialerAsync(
        IAuthorizationService authorizationService,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
        {
            return TypedResults.Forbid();
        }

        var banner = await BuildDialerBannerAsync(supervisorQueueAuthorizationService, httpContext);

        // Without Paced Dialing there is no dialer to describe, and the page shows no banner.
        if (banner is null)
        {
            return TypedResults.NoContent();
        }

        return TypedResults.Ok(banner);
    }

    /// <summary>
    /// Builds the dialer banner over the paced campaigns the signed-in supervisor may see, or returns
    /// <see langword="null"/> when the Paced Dialing feature is off.
    /// </summary>
    /// <param name="supervisorQueueAuthorizationService">The service that decides which queues a supervisor may see.</param>
    /// <param name="httpContext">The request.</param>
    /// <returns>The banner, or <see langword="null"/>.</returns>
    internal static async Task<DialerPacingBanner> BuildDialerBannerAsync(
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        HttpContext httpContext)
    {
        var bannerService = httpContext.RequestServices.GetService<IDialerPacingBannerService>();
        var supervisorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (bannerService is null || string.IsNullOrEmpty(supervisorId))
        {
            return null;
        }

        var cancellationToken = httpContext.RequestAborted;
        var authorizationCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        return await bannerService.GetAsync(
            queueId => IsQueueAuthorizedAsync(
                supervisorQueueAuthorizationService,
                authorizationCache,
                httpContext.User,
                supervisorId,
                queueId,
                cancellationToken),
            cancellationToken);
    }
}
