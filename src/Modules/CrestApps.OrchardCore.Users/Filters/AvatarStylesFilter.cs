using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Users.Filters;

/// <summary>
/// Represents the avatar styles filter.
/// </summary>
public sealed class AvatarStylesFilter : IAsyncResultFilter
{
    private readonly IResourceManager _resourceManager;
    private readonly UserAvatarOptions _avatarOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvatarStylesFilter"/> class.
    /// </summary>
    /// <param name="resourceManager">The resource manager.</param>
    /// <param name="options">The options.</param>
    public AvatarStylesFilter(
        IResourceManager resourceManager,
        IOptions<UserAvatarOptions> options)
    {
        _resourceManager = resourceManager;
        _avatarOptions = options.Value;
    }

    /// <summary>
    /// Asynchronously performs the on result execution operation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="next">The next.</param>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (_avatarOptions.UseDefaultStyle &&
            context.HttpContext.User.Identity.IsAuthenticated &&
                context.IsViewOrPageResult())
        {
            _resourceManager.RegisterResource("stylesheet", "user-profile-avatar").AtHead();
        }

        await next();
    }
}
