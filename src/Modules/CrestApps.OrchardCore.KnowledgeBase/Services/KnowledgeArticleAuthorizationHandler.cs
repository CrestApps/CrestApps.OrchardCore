using CrestApps.OrchardCore.KnowledgeBase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.KnowledgeBase.Services;

/// <summary>
/// Keeps an article to its audience wherever it is shown: viewing an article meant for signed-in people or for staff
/// fails for anyone else, whatever content permissions they have.
/// </summary>
internal sealed class KnowledgeArticleAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _services;

    public KnowledgeArticleAuthorizationHandler(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.Resource is not ContentItem item
            || !item.TryGet<KnowledgeArticlePart>(out var part)
            || part.Audience == KnowledgeArticleAudience.Public
            || (requirement.Permission != CommonPermissions.ViewContent && requirement.Permission != CommonPermissions.PreviewContent))
        {
            return;
        }

        var audiences = await _services.GetRequiredService<KnowledgeArticleAccess>().GetAudiencesAsync(context.User);

        if (!audiences.Contains(part.Audience))
        {
            context.Fail();
        }
    }
}
