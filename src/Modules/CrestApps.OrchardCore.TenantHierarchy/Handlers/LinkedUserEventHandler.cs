using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Handlers;

/// <summary>
/// Removes any password hash from a linked user when it is saved, so a linked user never has a password.
/// </summary>
public sealed class LinkedUserEventHandler : UserEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedUserEventHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider, used to resolve the linked user service lazily.</param>
    public LinkedUserEventHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public override async Task UpdatingAsync(UserUpdateContext context)
    {
        if (context.User is not User user || string.IsNullOrEmpty(user.PasswordHash))
        {
            return;
        }

        if (await _serviceProvider.GetRequiredService<LinkedUserService>().IsLinkedUserAsync(user.UserId))
        {
            user.PasswordHash = null;
        }
    }
}
