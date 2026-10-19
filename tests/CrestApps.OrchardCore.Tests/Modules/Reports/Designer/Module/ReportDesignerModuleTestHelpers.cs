using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Tests.Core.Services.Catalogs.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.DataPipelines.SharedLinks;
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

/// <summary>
/// Keeps shared links in memory, to tell which links of a report a service deletes.
/// </summary>
internal sealed class InMemorySharedLinkManager : ISharedLinkManager
{
    public List<SharedLink> Links { get; } = [];

    public Task<SharedLinkResult> CreateAsync(SharedLinkRequest request, CancellationToken cancellationToken = default)
    {
        var link = new SharedLink
        {
            LinkId = Guid.NewGuid().ToString("n")[..26],
            Kind = request.Kind,
            ResourceId = request.ResourceId,
            Title = request.Title,
            Properties = new Dictionary<string, string>(request.Properties, StringComparer.Ordinal),
            RecipientNames = [.. request.Recipients],
        };

        Links.Add(link);

        return Task.FromResult(new SharedLinkResult { Link = link, Url = "/SharedLinks/" + link.LinkId + "?token=secret" });
    }

    public Task<SharedLink> GetAsync(string linkId, CancellationToken cancellationToken = default)
        => Task.FromResult(Links.FirstOrDefault(link => link.LinkId == linkId));

    public Task<(SharedLinkAccessResult Result, SharedLink Link)> UseAsync(string linkId, string token, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task RevokeAsync(SharedLink link, CancellationToken cancellationToken = default)
    {
        link.RevokedUtc ??= DateTime.UtcNow;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(SharedLink link, CancellationToken cancellationToken = default)
    {
        Links.Remove(link);

        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<SharedLink> Links, int Count)> ListAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<(IReadOnlyList<SharedLink>, int)>((Links.Skip(skip).Take(take).ToList(), Links.Count));

    public Task<IReadOnlyList<SharedLink>> ListAsync(string kind, string resourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SharedLink>>(Links.Where(link => link.Kind == kind && link.ResourceId == resourceId).Reverse().ToList());
}
