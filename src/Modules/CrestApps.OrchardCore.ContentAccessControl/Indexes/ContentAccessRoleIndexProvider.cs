using CrestApps.OrchardCore.ContentAccessControl.Services;
using CrestApps.OrchardCore.Roles.Core.Indexes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContentAccessControl.Indexes;

/// <summary>
/// Maps each published item of a restricted content type to <see cref="ContentAccessRoleIndex"/>: a row per picked
/// role, or <c>Anonymous</c> and <c>Authenticated</c> when none is picked.
/// </summary>
internal sealed class ContentAccessRoleIndexProvider : IndexProvider<ContentItem>, IScopedIndexProvider
{
    private static readonly string[] _everyone = [OrchardCoreConstants.Roles.Anonymous, OrchardCoreConstants.Roles.Authenticated];

    private readonly IServiceProvider _serviceProvider;
    private readonly HashSet<string> _openTypes = [];

    private IContentDefinitionManager _contentDefinitionManager;

    public ContentAccessRoleIndexProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<ContentAccessRoleIndex>()
            .Map(async contentItem =>
            {
                if (!contentItem.Published || _openTypes.Contains(contentItem.ContentType))
                {
                    return null;
                }

                // Resolved on first use, as Orchard Core's own index providers do.
                _contentDefinitionManager ??= _serviceProvider.GetRequiredService<IContentDefinitionManager>();

                var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

                if (!ContentAccessRestriction.IsRestricted(definition))
                {
                    _openTypes.Add(contentItem.ContentType);

                    return null;
                }

                var roleNames = ContentAccessRestriction.GetRoleNames(definition, contentItem);

                return (roleNames.Count == 0 ? _everyone : roleNames.Where(name => name.Length <= ContentAccessRoleIndex.MaxRoleNameLength))
                    .Select(roleName => new ContentAccessRoleIndex
                    {
                        ContentItemId = contentItem.ContentItemId,
                        ContentType = contentItem.ContentType,
                        RoleName = roleName,
                    });
            });
}
