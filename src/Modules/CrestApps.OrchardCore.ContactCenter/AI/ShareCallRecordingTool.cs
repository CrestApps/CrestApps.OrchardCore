using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Makes the signed link the Share button of a call recording's page makes: anyone holding it can play the recording
/// for 24 hours without signing in. It needs the same permissions as the button, and making it is audited the same way.
/// </summary>
internal sealed class ShareCallRecordingTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "shareCallRecording";

    private const string AuditPurpose = "call-recordings-share";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The recording id, from listCallRecordings."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a link that lets anyone who has it play the recording until it expires (24 hours), and returns the link and expiry.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ListenToAllCallRecordings;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var access = await services.GetRequiredService<CallRecordingAccessEvaluator>().GetAccessAsync(user);

        if (!access.CanListOwn || string.IsNullOrEmpty(access.UserId))
        {
            return NotAllowedMessage;
        }

        var id = new ContactCenterToolArguments(arguments).GetString("id");

        if (id is null)
        {
            return "No link was made: give the id of the recording.";
        }

        var recordings = services.GetRequiredService<CallRecordingReader>();
        var recording = await recordings.FindPlayableAsync(id, access, cancellationToken);

        if (recording is null)
        {
            return $"No link was made: no recording you may hear has the id '{id}'.";
        }

        // The link is the site's own address, which only the request knows.
        var request = services.GetService<IHttpContextAccessor>()?.HttpContext?.Request;

        if (request is null || !request.Host.HasValue)
        {
            return "No link was made: the site's address is not known here. Use the Share button on the recording's page.";
        }

        var (token, expiresUtc) = services.GetRequiredService<CallRecordingLinks>().Create(recording.ItemId);
        await recordings.AuditAccessAsync(recording, access, AuditPurpose, cancellationToken);

        return Json(new
        {
            id = recording.ItemId,
            url = CallRecordingLinkEndpoints.BuildUrl(request, token),
            expires = await ToLocalAsync(services.GetRequiredService<ILocalClock>(), expiresUtc),
            note = "Anyone with this link can play the recording until it expires.",
        });
    }
}
