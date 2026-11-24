using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Core.Indexes;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Migrations;

/// <summary>
/// Creates the External Tools tables, the External tool part and course item type (added to the course outlines), and
/// registers the LTI service scopes as OpenID scopes. The type is defined in code: a recipe run inside a migration would
/// wait forever on SQLite for the write lock the migration holds.
/// </summary>
internal sealed class LtiMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;
    private readonly IOpenIdScopeManager _scopeManager;

    public LtiMigrations(
        IContentDefinitionManager contentDefinitionManager,
        CourseListSynchronizer courseListSynchronizer,
        IOpenIdScopeManager scopeManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
        _scopeManager = scopeManager;
    }

    public async Task<int> CreateAsync()
    {
        await LtiIndexSchema.CreateAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(ExternalToolItemPart), part => part
            .Attachable(false)
            .WithDisplayName("External tool")
            .WithDescription("Makes a course item an LTI 1.3 resource link: the registered external tool it launches, its target link and custom parameters, and its line item."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(LtiConstants.ContentTypes.ExternalToolItem, type => type
            .WithDisplayName("External tool")
            .WithDescription("An activity in a registered LTI 1.3 tool that learners launch from the course player.")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(HtmlBodyPart), part => part.WithPosition("2").WithEditor("Wysiwyg").WithDisplayName("Introduction"))
            .WithPart(nameof(ExternalToolItemPart), part => part.WithPosition("3"))
            .WithPart(nameof(CourseItemPart), part => part
                .WithPosition("10")
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = LtiConstants.ExternalToolKind,
                    DefaultCompletionMode = CompletionModes.Graded,
                })));

        await _courseListSynchronizer.SynchronizeAsync();
        await RegisterScopesAsync();

        return 1;
    }

    private async Task RegisterScopesAsync()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LtiConstants.Scopes.LineItem] = "LTI: manage line items",
            [LtiConstants.Scopes.LineItemReadOnly] = "LTI: read line items",
            [LtiConstants.Scopes.ResultReadOnly] = "LTI: read results",
            [LtiConstants.Scopes.Score] = "LTI: post scores",
            [LtiConstants.Scopes.ContextMembershipReadOnly] = "LTI: read course memberships",
        };

        foreach (var (scope, displayName) in names)
        {
            if (await _scopeManager.FindByNameAsync(scope) is not null)
            {
                continue;
            }

            await _scopeManager.CreateAsync(new OpenIdScopeDescriptor
            {
                Name = scope,
                DisplayName = displayName,
                Description = "A scope of the LTI Advantage services of Learning External Tools. Only external tools are given it.",
            });
        }
    }
}
