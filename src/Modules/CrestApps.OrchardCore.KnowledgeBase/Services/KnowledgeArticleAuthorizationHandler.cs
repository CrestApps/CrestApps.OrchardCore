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
            || !IsReading(requirement.Permission?.Name))
        {
            return;
        }

        var audiences = await _services.GetRequiredService<KnowledgeArticleAccess>().GetAudiencesAsync(context.User);

        if (!audiences.Contains(part.Audience))
        {
            context.Fail();
        }
    }

    /// <summary>
    /// Whether a permission reads content: viewing, previewing or listing it, whether one's own or anyone's, in general
    /// or for a type (<c>ViewContent_HelpArticle</c>), compared by name so a type's own permission meets the audience too.
    /// </summary>
    /// <param name="name">The permission's name.</param>
    /// <returns>Whether it reads content.</returns>
    internal static bool IsReading(string name)
        => !string.IsNullOrEmpty(name) && _reading.Contains(name.Split('_', 2)[0]);

    private static readonly HashSet<string> _reading = new(StringComparer.Ordinal)
    {
        CommonPermissions.ViewContent.Name,
        CommonPermissions.ViewOwnContent.Name,
        CommonPermissions.PreviewContent.Name,
        CommonPermissions.PreviewOwnContent.Name,
        CommonPermissions.ListContent.Name,
    };
}
