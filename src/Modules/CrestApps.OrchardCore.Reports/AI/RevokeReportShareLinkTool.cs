using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Revokes a share link of a designed report through the same service as the designer's Share links. It needs the
/// permission that changes the report, with the report as the resource, and the permission that shares reports
/// publicly.
/// </summary>
public sealed class RevokeReportShareLinkTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "revokeReportShareLink";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the designed report, from listReports."
        },
        "linkId": {
          "type": "string",
          "description": "The id of the link, from getReport with include shareLinks."
        }
      },
      "required": ["id", "linkId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Revokes one share link of a designed report so it stops working. Use getReport with share links to find the link id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");
        var linkId = ReadString(arguments, "linkId");

        if (id is null || linkId is null)
        {
            return "Give the report's id in id and the link's id in linkId. Call getReport with include shareLinks to find it.";
        }

        if (ReportToolScopeChanges.Get(services).WasChanged(id))
        {
            return ReportToolScopeChanges.ChangedMessage(id);
        }

        var design = await services.GetRequiredService<ReportDesignService>().FindAsync(id);

        if (design is null)
        {
            return ReportToolCatalog.NotFoundMessage(id);
        }

        if (!await ReportToolCatalog.CanManageAsync(services, user, design) ||
            !await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ShareReportsPublicly))
        {
            return NotAllowedMessage;
        }

        var link = await services.GetRequiredService<ReportShareLinkService>().RevokeAsync(design, linkId, user.Identity?.Name, cancellationToken);

        if (link is null)
        {
            return $"The report has no share link '{linkId}'. Call getReport with include shareLinks to find it.";
        }

        return Json(new
        {
            revoked = true,
            linkId = link.LinkId,
            report = design.DisplayText,
            recipients = link.RecipientNames,
            revokedAt = await ToLocalAsync(services, link.RevokedUtc),
        });
    }
}
