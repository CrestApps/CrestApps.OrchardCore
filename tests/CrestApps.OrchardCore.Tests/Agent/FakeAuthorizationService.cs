using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Stands in for the authorization service in a tool test: it answers each permission check with the test's rule and
/// records what was asked, so a test can pin the permission and the resource a tool checks.
/// </summary>
internal sealed class FakeAuthorizationService : IAuthorizationService
{
    private readonly Func<Permission, object, bool> _authorize;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeAuthorizationService"/> class.
    /// </summary>
    /// <param name="authorize">The rule that grants or refuses a permission on a resource.</param>
    public FakeAuthorizationService(Func<Permission, object, bool> authorize)
    {
        _authorize = authorize;
    }

    /// <summary>
    /// Gets the permission checks the tool made, in order.
    /// </summary>
    public List<(string Permission, object Resource)> Checks { get; } = [];

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        foreach (var requirement in requirements.OfType<PermissionRequirement>())
        {
            Checks.Add((requirement.Permission.Name, resource));

            if (!_authorize(requirement.Permission, resource))
            {
                return Task.FromResult(AuthorizationResult.Failed());
            }
        }

        return Task.FromResult(AuthorizationResult.Success());
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
        => Task.FromResult(AuthorizationResult.Failed());
}
