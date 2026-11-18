using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Creates a share link of a designed report through the same service as the designer's Share links, and emails it to
/// its recipients. It needs the permission that changes the report, with the report as the resource, and the permission
/// that shares reports publicly. The link's address holds a secret token, so the tool never returns it: the link reaches
/// its recipients by email only, and opens for them once they sign in.
/// </summary>
public sealed class CreateReportShareLinkTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "createReportShareLink";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the designed report, from listReports."
        },
        "recipients": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "The user names or emails of the users of the site the link is for."
        },
        "expiresAfterDays": {
          "type": "integer",
          "minimum": 1,
          "description": "The number of days the link works, 7 by default."
        },
        "maxUses": {
          "type": "integer",
          "minimum": 0,
          "description": "The most times the link may be used, or 0 for no limit (the default). Opening the report, changing its filters and exporting it are each a use."
        },
        "allowExport": {
          "type": "boolean",
          "description": "Whether the link also allows exporting. False by default."
        },
        "note": {
          "type": "string",
          "description": "A note about what the link is for, sent in the email and shown with the link."
        }
      },
      "required": ["id", "recipients"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a link that opens a designed report for named recipients, with an expiry in days, an optional maximum number of uses, whether export is allowed and a note, and emails it to the recipients. The link itself is never returned: it holds a secret token and reaches the recipients by email only.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");
        var recipients = ReadStringList(arguments, "recipients");

        if (id is null)
        {
            return "Give the report's id in id. Call listReports to find it.";
        }

        if (recipients is not { Count: > 0 })
        {
            return "Give at least one recipient, a user name or email of a user of the site, in recipients.";
        }

        var expiresAfterDays = ReadInt(arguments, "expiresAfterDays") ?? 7;

        if (expiresAfterDays < 1)
        {
            return "Give expiresAfterDays as 1 day or more.";
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

        var outcome = await services.GetRequiredService<ReportShareLinkService>().CreateAsync(design, new ReportShareLinkRequest
        {
            Recipients = recipients,
            Note = ReadString(arguments, "note"),
            ExpiresAfterDays = expiresAfterDays,
            MaxUses = Math.Max(0, ReadInt(arguments, "maxUses") ?? 0),
            AllowExport = ReadBool(arguments, "allowExport") == true,

            // The address is never given to the model, so the email is the only way the link reaches its recipients.
            NotifyByEmail = true,
        }, user.Identity?.Name, cancellationToken);

        if (outcome.Result is null)
        {
            return $"The link was not created: {outcome.Error}";
        }

        var link = outcome.Result.Link;
        var notified = outcome.Result.Notified ?? [];
        var notNotified = link.RecipientNames.Where(name => !notified.Contains(name, StringComparer.OrdinalIgnoreCase)).ToArray();

        return Json(new
        {
            created = true,
            linkId = link.LinkId,
            report = design.DisplayText,
            recipients = link.RecipientNames,
            emailedTo = notified,
            notEmailedTo = notNotified.Length > 0 ? notNotified : null,
            expires = await ToLocalAsync(services, link.ExpiresUtc),
            maxUses = link.MaxUses == 0 ? (int?)null : link.MaxUses,
            allowExport = ReportSharedLinkHandler.AllowsExport(link),
            note = notNotified.Length > 0
                ? "Some recipients were not emailed, so they cannot open the link. Revoke it with revokeReportShareLink and create one again, or create it in the Report Builder, which shows the link once."
                : "The link was emailed to its recipients; it opens the report for them once they sign in. Its address is not shown here.",
        });
    }
}
