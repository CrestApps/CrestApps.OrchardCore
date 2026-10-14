using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.Admin;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// Grants the Access admin panel permission to a signed-in user, but only while they open a page marked with
/// <see cref="CustomerAdminPageAttribute"/>. Customers who buy through the checkout get no admin access, yet their own
/// plans, sites and transactions are admin pages; without this, those pages answer 403.
/// </summary>
public sealed class CustomerAdminPageAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerAdminPageAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor for the current request, whose endpoint is checked.</param>
    public CustomerAdminPageAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded ||
            !string.Equals(requirement.Permission?.Name, AdminPermissions.AccessAdminPanel.Name, StringComparison.Ordinal) ||
            context.User?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Only the page being opened counts: the dashboard and every other admin page keep requiring the permission.
        var endpoint = _httpContextAccessor.HttpContext?.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<CustomerAdminPageAttribute>() is not null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
