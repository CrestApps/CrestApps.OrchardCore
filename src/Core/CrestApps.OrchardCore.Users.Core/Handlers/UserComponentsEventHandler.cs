using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;

namespace CrestApps.OrchardCore.Users.Core.Handlers;

/// <summary>
/// Handles events for user components event.
/// </summary>
public class UserComponentsEventHandler : UserEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    private IUserCacheService _userCacheService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserComponentsEventHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public UserComponentsEventHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override Task CreatedAsync(UserCreateContext context)
        => RemoveTagAsync(context.User);

    public override Task DeletedAsync(UserDeleteContext context)
        => RemoveTagAsync(context.User);

    public override Task DisabledAsync(UserContext context)
        => RemoveTagAsync(context.User);

    public override Task EnabledAsync(UserContext context)
        => RemoveTagAsync(context.User);

    public override Task UpdatedAsync(UserUpdateContext context)
        => RemoveTagAsync(context.User);

    private Task RemoveTagAsync(IUser user)
    {
        _userCacheService ??= _serviceProvider.GetRequiredService<IUserCacheService>();

        return _userCacheService.RemoveAsync(user.UserName);
    }
}
