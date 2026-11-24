using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Creates or revokes a share link of a designed report through the same service as the designer's Share links. Both
/// need the permission that changes the report, with the report as the resource, and the permission that shares reports
/// publicly. A created link's address holds a secret token, so the tool never returns it: the link reaches its
/// recipients by email only, and opens for them once they sign in.
/// </summary>
internal sealed class SetReportShareLinkTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setReportShareLink";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "action": {
          "type": "string",
          "enum": ["create", "revoke"],
          "description": "create makes a new link and emails it to the recipients; revoke stops an existing link from working."
        },
        "id": {
          "type": "string",
          "description": "The id of the designed report, from listReports."
        },
        "linkId": {
          "type": "string",
          "description": "For revoke: the id of the link, from getReport with include shareLinks."
        },
        "recipients": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "For create: the user names or emails of the users of the site the link is for."
        },
        "expiresAfterDays": {
          "type": "integer",
          "minimum": 1,
          "description": "For create: the number of days the link works, 7 by default."
        },
        "maxUses": {
          "type": "integer",
          "minimum": 0,
          "description": "For create: the most times the link may be used, or 0 for no limit (the default). Opening the report, changing its filters and exporting it are each a use."
        },
        "allowExport": {
          "type": "boolean",
          "description": "For create: whether the link also allows exporting. False by default."
        },
        "note": {
          "type": "string",
          "description": "For create: a note about what the link is for, sent in the email and shown with the link."
        }
      },
      "required": ["action", "id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or revokes a link that opens a designed report for named recipients. create takes the recipients, an expiry in days, an optional maximum number of uses, whether export is allowed and a note, and emails the link to the recipients; the link itself is never returned, since it holds a secret token. revoke takes the link id from getReport with share links and stops the link from working.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var action = ReadString(arguments, "action")?.ToLowerInvariant();
        var id = ReadString(arguments, "id");

        if (action is not ("create" or "revoke"))
        {
            return "Nothing was changed: give action as create or revoke.";
        }

        if (id is null)
        {
            return "Nothing was changed: give the report's id in id. Call listReports to find it.";
        }

        var linkId = ReadString(arguments, "linkId");
        var recipients = ReadStringList(arguments, "recipients");
        var expiresAfterDays = ReadInt(arguments, "expiresAfterDays") ?? 7;

        if (action == "create")
        {
            if (recipients is not { Count: > 0 })
            {
                return "No link was created: give at least one recipient, a user name or email of a user of the site, in recipients.";
            }

            if (expiresAfterDays < 1)
            {
                return "No link was created: give expiresAfterDays as 1 day or more.";
            }
        }
        else if (linkId is null)
        {
            return "Nothing was revoked: give the link's id in linkId. Call getReport with include shareLinks to find it.";
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

        var shareLinks = services.GetRequiredService<ReportShareLinkService>();

        if (action == "revoke")
        {
            var revoked = await shareLinks.RevokeAsync(design, linkId, user.Identity?.Name, cancellationToken);

            if (revoked is null)
            {
                return $"Nothing was revoked: the report has no share link '{linkId}'. Call getReport with include shareLinks to find it.";
            }

            return Json(new
            {
                action = "revoke",
                revoked = true,
                linkId = revoked.LinkId,
                report = design.DisplayText,
                recipients = revoked.RecipientNames,
                revokedAt = await ToLocalAsync(services, revoked.RevokedUtc),
            });
        }

        var outcome = await shareLinks.CreateAsync(design, new ReportShareLinkRequest
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
            action = "create",
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
                ? "Some recipients were not emailed, so they cannot open the link. Revoke it with setReportShareLink (action revoke) and create one again, or create it in the Report Builder, which shows the link once."
                : "The link was emailed to its recipients; it opens the report for them once they sign in. Its address is not shown here.",
        });
    }
}
