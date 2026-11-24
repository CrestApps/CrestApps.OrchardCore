using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Erases a recording with a stated reason, as the Erase button of a call recording's page does: only a recording the
/// caller may hear, through recording governance, which refuses one under legal hold and audits the erasure.
/// </summary>
internal sealed class EraseCallRecordingTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "eraseCallRecording";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The recording id, from listCallRecordings."
        },
        "reason": {
          "type": "string",
          "description": "Why the recording is erased, kept on the audit trail, for example the customer's privacy request."
        }
      },
      "required": ["id", "reason"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Permanently erases a recording and its media with a stated reason. Refused when the interaction is under legal hold. Cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.DeleteCallRecordings;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var access = await services.GetRequiredService<CallRecordingAccessEvaluator>().GetAccessAsync(user);

        if (!access.CanListOwn || string.IsNullOrEmpty(access.UserId))
        {
            return NotAllowedMessage;
        }

        var reader = new ContactCenterToolArguments(arguments);
        var id = reader.GetString("id");
        var reason = reader.GetString("reason");

        if (id is null)
        {
            return "Nothing was erased: give the id of the recording.";
        }

        if (reason is null)
        {
            return "Nothing was erased: give the reason the recording is erased.";
        }

        var recordings = services.GetRequiredService<CallRecordingReader>();
        var recording = await recordings.FindPlayableAsync(id, access, cancellationToken);

        if (recording is null)
        {
            return $"Nothing was erased: no recording you may hear has the id '{id}'.";
        }

        return await recordings.EraseAsync(recording, access, reason) switch
        {
            CallRecordingErasureOutcome.Erased => Json(new
            {
                erased = true,
                id = recording.ItemId,
                interactionId = recording.InteractionId,
                message = "The recording was erased.",
            }),
            CallRecordingErasureOutcome.LegalHold => "Nothing was erased: the recording is under legal hold and cannot be erased.",
            _ => "Nothing was erased: the recording could not be erased. Try again in a moment.",
        };
    }
}
