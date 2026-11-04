using CrestApps.OrchardCore.AI.WebMcp.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.WebMcp.Migrations;

/// <summary>
/// Defines the attachable WebMCP part.
/// </summary>
internal sealed class WebMcpPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpPartMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public WebMcpPartMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Defines the part.
    /// </summary>
    /// <returns>The version of the migration.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(WebMcpPart), part => part
            .Attachable()
            .WithDisplayName("WebMCP tools")
            .WithDescription("Exposes selected AI tools through WebMCP on the content item's page, to browser agents and the AI chat of signed-in users who may use them.")
        );

        return 1;
    }
}
