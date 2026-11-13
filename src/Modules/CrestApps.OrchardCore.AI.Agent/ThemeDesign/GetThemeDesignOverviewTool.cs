using System.Text.Json;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore;
using CrestApps.OrchardCore.SiteResources;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell.Descriptor.Models;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Tells the AI which themes are active, what the user may change, and what their theme draft holds.
/// </summary>
public sealed class GetThemeDesignOverviewTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "getThemeDesignOverview";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {

      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Call this first when designing the theme. Returns the active front-end and admin themes, whether admin templates are on, what the user may change, the changes in their unpublished theme draft, the address of the page where they review it, and a guide to designing an Orchard Core theme with these tools. Read the guide before you change anything.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var draftService = GetDraftService(services);
        var draft = await draftService.GetDraftAsync();
        var themes = await services.GetRequiredService<IThemeShapeExplorer>().GetActiveThemesAsync();
        var shellDescriptor = services.GetRequiredService<ShellDescriptor>();
        var httpContext = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var adminPrefix = services.GetRequiredService<IOptions<AdminOptions>>().Value.AdminUrlPrefix;

        return Serialize(new
        {
            themes,
            adminTemplatesEnabled = shellDescriptor.Features.Any(feature => feature.Id == OrchardCoreFeatureIds.AdminTemplates),
            canManageSiteTemplates = await CanAsync(services, GetTemplatesPermission(ThemeDesignScope.Site)),
            canManageAdminTemplates = await CanAsync(services, GetTemplatesPermission(ThemeDesignScope.Admin)),
            canManageSiteResources = await CanAsync(services, SiteResourcesPermissions.ManageSiteResources),
            draft = draft is null ? null : new
            {
                draft.Preview,
                draft.ChangeCount,
                draft.CreatedUtc,
                draft.ModifiedUtc,
                changes = await draftService.GetChangesAsync(),
            },
            reviewUrl = $"{httpContext?.Request.PathBase}/{adminPrefix}/theme-designer",
            guide = await services.GetRequiredService<ITemplateService>().RenderAsync(ThemeDesignTemplateIds.Guide, cancellationToken: cancellationToken),
        });
    }
}
