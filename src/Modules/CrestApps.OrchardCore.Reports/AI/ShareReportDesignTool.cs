using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Sets the users and roles a designed report is shared with, by publishing the stored report with the new lists, as the
/// designer's sharing settings do. It needs the permission that changes the report, with the report as the resource, and
/// the permission that shares reports publicly to share with anonymous visitors. Every user and role must exist, so a
/// misspelled name never grants access to whoever later registers with it.
/// </summary>
public sealed class ShareReportDesignTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "shareReportDesign";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the designed report, from listReports."
        },
        "users": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "The user names the report is shared with; they replace the current list. Leave it out to keep the current users; give an empty list to share with no user."
        },
        "roles": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "The role names the report is shared with; they replace the current list. Authenticated shares it with everyone signed in, Anonymous with every visitor. Leave it out to keep the current roles."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets the users and roles a designed report is shared with. The people it is shared with see the report's data with its owner's access.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Give the report's id in id. Call listReports to find it.";
        }

        var users = ReadStringList(arguments, "users");
        var roles = ReadStringList(arguments, "roles");

        if (users is null && roles is null)
        {
            return "Give the users, the roles, or both that the report is shared with.";
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

        var canSharePublicly = await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ShareReportsPublicly);
        var sharedUsers = design.SharedUserNames?.ToList() ?? [];
        var sharedRoles = design.SharedRoles?.ToList() ?? [];

        if (users is not null)
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var unknown = new List<string>();

            sharedUsers = [];

            foreach (var name in users.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (await userManager.FindByNameAsync(name) is { } found)
                {
                    sharedUsers.Add(found.UserName);
                }
                else
                {
                    unknown.Add(name);
                }
            }

            if (unknown.Count > 0)
            {
                return $"There is no user named {string.Join(", ", unknown.Select(name => $"'{name}'"))}. Nothing was changed.";
            }
        }

        if (roles is not null)
        {
            var roleNames = (await services.GetRequiredService<IRoleService>().GetRoleNamesAsync()).ToList();

            roleNames.Add(OrchardCoreConstants.Roles.Anonymous);
            roleNames.Add(OrchardCoreConstants.Roles.Authenticated);

            var unknown = roles.Where(role => !roleNames.Contains(role, StringComparer.OrdinalIgnoreCase)).ToArray();

            if (unknown.Length > 0)
            {
                return $"There is no role named {string.Join(", ", unknown.Select(name => $"'{name}'"))}. Nothing was changed.";
            }

            sharedRoles = roles
                .Select(role => roleNames.First(name => string.Equals(name, role, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var wasPublic = design.SharedRoles?.Contains(OrchardCoreConstants.Roles.Anonymous, StringComparer.OrdinalIgnoreCase) == true;

            if (!canSharePublicly && !wasPublic && sharedRoles.Contains(OrchardCoreConstants.Roles.Anonymous, StringComparer.OrdinalIgnoreCase))
            {
                return "You are not allowed to share reports with anonymous visitors.";
            }
        }

        var (revision, problem) = await ReportDesignPublishing.GetRevisionAsync(services, design);

        if (problem is not null)
        {
            return problem;
        }

        var incoming = design.Clone();

        incoming.SharedUserNames = sharedUsers;
        incoming.SharedRoles = sharedRoles;

        var published = await services.GetRequiredService<ReportDesignHistoryService>().PublishAsync(incoming, design, revision, force: false, user, canSharePublicly);
        var failure = await ReportDesignPublishing.DescribeFailureAsync(services, published);

        if (failure is not null)
        {
            return failure;
        }

        if (published.Save is not { Saved: true } saved)
        {
            return Json(new
            {
                shared = false,
                errors = published.Save?.Errors ?? [],
            });
        }

        changes.Record(saved.Id);

        return Json(new
        {
            shared = true,
            id = saved.Id,
            title = saved.Design?.DisplayText ?? design.DisplayText,
            users = saved.Design?.SharedUserNames ?? sharedUsers,
            roles = saved.Design?.SharedRoles ?? sharedRoles,
            versionNumber = published.VersionNumber,
            note = "The people the report is shared with see its data with its owner's access.",
        });
    }
}
