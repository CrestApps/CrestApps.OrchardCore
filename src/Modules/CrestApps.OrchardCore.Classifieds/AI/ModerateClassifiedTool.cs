using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Classifieds.AI;

/// <summary>
/// Approves or rejects a posted listing, as Approve and Reject on Content › Posted listings do: the latest version of a
/// content item with <see cref="ClassifiedPart"/>, through <see cref="ClassifiedService.ApproveAsync"/> (publishes it and
/// starts its term from now) or <see cref="ClassifiedService.RejectAsync"/> (takes it down and emails the poster the
/// reason when the site's email is set up). Never edit the part's status with the content tools instead: that skips the
/// term, the publishing and the email.
/// </summary>
internal sealed class ModerateClassifiedTool : ClassifiedsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "moderateClassified";

    private const int MaxReasonLength = 1000;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "listingId": {
          "type": "string",
          "description": "The listing's content item id, from listClassifieds."
        },
        "action": {
          "type": "string",
          "enum": ["approve", "reject"],
          "description": "approve publishes the listing and starts its term from now; reject takes it down and emails the poster."
        },
        "reason": {
          "type": "string",
          "description": "For reject only: why the listing was not approved. It is emailed to the poster word for word, so show it to the user first."
        }
      },
      "required": ["listingId", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Approves a posted listing (publishes it and starts its term from now) or rejects it with an optional reason (takes it down and emails the poster the reason). The poster of a rejected listing can edit it and post it again.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ClassifiedsPermissions.ModerateClassifieds;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "listingId") is not { } listingId)
        {
            return "Nothing was changed: give the listing's id. Use listClassifieds to find it.";
        }

        var action = ReadString(arguments, "action");
        var approve = string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase);

        if (!approve && !string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was changed: action must be approve or reject.";
        }

        var reason = ReadString(arguments, "reason");

        if (approve && reason is not null)
        {
            return "Nothing was changed: a reason is only sent when a listing is rejected. Leave it out to approve.";
        }

        if (reason?.Length > MaxReasonLength)
        {
            return $"Nothing was changed: the reason can be up to {MaxReasonLength} characters.";
        }

        var item = await services.GetRequiredService<IContentManager>().GetAsync(listingId, VersionOptions.Latest);

        if (item is null || !item.Has<ClassifiedPart>())
        {
            return $"Nothing was changed: no posted listing has the id '{listingId}'. Use listClassifieds to find it.";
        }

        var before = item.GetOrCreate<ClassifiedPart>().Status;
        var classifieds = services.GetRequiredService<ClassifiedService>();

        if (approve)
        {
            await classifieds.ApproveAsync(item);
        }
        else
        {
            await classifieds.RejectAsync(item, reason);
        }

        if (await CommitAsync(services, cancellationToken) is { } error)
        {
            return error;
        }

        var listing = await ListClassifiedsTool.DescribeAsync(services, item, services.GetService<IUserService>(), new Dictionary<string, string>(StringComparer.Ordinal));

        return Json(new
        {
            listing,
            previousStatus = ToCamel(before.ToString()),
            message = approve
                ? $"'{item.DisplayText}' is up and runs its term from now."
                : $"'{item.DisplayText}' was rejected and taken down. Its poster is emailed the reason when the site's email is set up and they have an email address.",
        });
    }
}
