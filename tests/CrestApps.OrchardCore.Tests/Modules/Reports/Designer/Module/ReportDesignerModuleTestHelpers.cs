using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

/// <summary>
/// An authorization service that grants the permissions a test lists and runs the handlers under test for resources.
/// </summary>
internal sealed class FakeAuthorizationService : IAuthorizationService
{
    private readonly HashSet<string> _granted;
    private readonly List<IAuthorizationHandler> _handlers = [];

    public FakeAuthorizationService(params string[] granted)
    {
        _granted = new HashSet<string>(granted, StringComparer.Ordinal);
    }

    public FakeAuthorizationService With(IAuthorizationHandler handler)
    {
        _handlers.Add(handler);

        return this;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        var list = requirements.ToList();
        var context = new AuthorizationHandlerContext(list, user, resource);

        foreach (var requirement in list.OfType<PermissionRequirement>())
        {
            if (_granted.Contains(requirement.Permission.Name))
            {
                context.Succeed(requirement);
            }
        }

        foreach (var handler in _handlers)
        {
            await handler.HandleAsync(context);
        }

        return context.HasSucceeded ? AuthorizationResult.Success() : AuthorizationResult.Failed();
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
    {
        return Task.FromResult(AuthorizationResult.Failed());
    }
}

internal static class ReportDesignerPrincipals
{
    public static ClaimsPrincipal User(string userId, string userName, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    public static ClaimsPrincipal Anonymous()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    public static Catalog<T> Catalog<T>(params T[] records)
        where T : CatalogItem
    {
        return new Catalog<T>(new FakeDocumentManager<T>(records));
    }

    public static string Design => ReportDesignerPermissions.ManageOwnReportDesigns.Name;

    // A snapshot store for tests whose views are all live, so it is never read.
    public static ReportViewSnapshotStore NoSnapshots()
    {
        return new ReportViewSnapshotStore(Mock.Of<ISession>());
    }
}
