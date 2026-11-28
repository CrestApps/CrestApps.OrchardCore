using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Restores an earlier version of a designed report and publishes it, as restoring a version in the designer and then
/// publishing does. It needs the permission that changes the report, with the report as the resource. A version also
/// holds who the report was shared with then; the report keeps who it is shared with now, so restoring never changes
/// sharing, which only <c>shareReportDesign</c> does.
/// </summary>
public sealed class RestoreReportDesignVersionTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "restoreReportDesignVersion";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the designed report, from listReports."
        },
        "version": {
          "type": "integer",
          "minimum": 1,
          "description": "The number of the version to restore, from getReport with include versions."
        }
      },
      "required": ["id", "version"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Restores an earlier saved version of a designed report and publishes it; who the report is shared with does not change. Use getReport with versions to find the version number.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");
        var number = ReadInt(arguments, "version");

        if (id is null || number is not > 0)
        {
            return "Give the report's id in id and the version number in version. Call getReport with include versions to find it.";
        }

        var changes = ReportToolScopeChanges.Get(services);

        if (changes.WasChanged(id))
        {
            return ReportToolScopeChanges.ChangedMessage(id);
        }

        var design = await services.GetRequiredService<ReportDesignService>().FindAsync(id);

        if (design is null)
        {
            return ReportToolCatalog.NotFoundMessage(id);
        }

        if (!await ReportToolCatalog.CanManageAsync(services, user, design))
        {
            return NotAllowedMessage;
        }

        var (revision, problem) = await ReportDesignPublishing.GetRevisionAsync(services, design);

        if (problem is not null)
        {
            return problem;
        }

        var history = services.GetRequiredService<ReportDesignHistoryService>();
        var restored = await history.RestoreAsync(design, number.Value, revision, force: false, user);

        if (restored.Status == ReportHistoryStatus.NotFound)
        {
            return $"The report has no version {number}. Call getReport with include versions to find it.";
        }

        var failure = await ReportDesignPublishing.DescribeFailureAsync(services, restored);

        if (failure is not null)
        {
            return failure;
        }

        // The draft now holds the version; it is published with the report's current sharing.
        var workingCopy = await history.GetWorkingCopyAsync(design);
        var incoming = workingCopy.Design.Clone();

        incoming.SharedUserNames = design.SharedUserNames?.ToList() ?? [];
        incoming.SharedRoles = design.SharedRoles?.ToList() ?? [];

        var canSharePublicly = await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ShareReportsPublicly);
        var published = await history.PublishAsync(incoming, design, workingCopy.Revision, force: false, user, canSharePublicly);

        failure = await ReportDesignPublishing.DescribeFailureAsync(services, published);

        if (failure is not null)
        {
            return $"Version {number} was copied into the report's draft in the Report Builder but not published: {failure}";
        }

        if (published.Save is not { Saved: true } saved)
        {
            return Json(new
            {
                restored = false,
                error = $"Version {number} was copied into the report's draft in the Report Builder but could not be published. Fix or discard the draft in the builder.",
                errors = published.Save?.Errors ?? [],
            });
        }

        changes.Record(saved.Id);

        return Json(new
        {
            restored = true,
            id = saved.Id,
            title = saved.Design?.DisplayText ?? incoming.DisplayText,
            restoredVersion = number,
            versionNumber = published.VersionNumber,
            warnings = saved.Warnings.Count > 0 ? saved.Warnings : null,
            sharing = "Who the report is shared with did not change.",
        });
    }
}
