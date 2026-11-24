using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Core.Cohorts;

/// <summary>
/// Takes a deleted user out of every cohort, so what builds on cohorts (enrollment by cohort) hears that they left.
/// </summary>
public sealed class CohortUserEventHandler : UserEventHandlerBase
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="CohortUserEventHandler"/> class.
    /// </summary>
    /// <param name="services">The services; the cohort service is resolved when a user is deleted, since it needs the
    /// user manager, which needs the user event handlers.</param>
    public CohortUserEventHandler(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    public override async Task DeletedAsync(UserDeleteContext context)
    {
        if (context?.User is User { UserId: { Length: > 0 } userId })
        {
            await _services.GetRequiredService<ICohortService>().RemoveFromAllAsync(userId, "UserDeleted");
        }
    }
}
