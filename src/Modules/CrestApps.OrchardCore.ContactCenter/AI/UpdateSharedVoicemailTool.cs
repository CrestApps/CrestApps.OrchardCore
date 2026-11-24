using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Claims, releases or resolves a shared voicemail, as the Claim, Release and Mark as done buttons of the Shared
/// voicemail page do. <see cref="ISharedVoicemailService"/> authorizes each action, including taking over another
/// person's claim, which needs the right to manage shared voicemail.
/// </summary>
internal sealed class UpdateSharedVoicemailTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "updateSharedVoicemail";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The voicemail id, from listSharedVoicemails."
        },
        "action": {
          "type": "string",
          "enum": ["claim", "release", "resolve"],
          "description": "claim: the caller handles it; release: return it to the queue, unclaimed; resolve: mark it as done."
        },
        "note": {
          "type": "string",
          "description": "With resolve, how the message was dealt with."
        }
      },
      "required": ["id", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Claims a voicemail for the caller, releases it back to the queue, or marks it done with a note.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.AccessSharedVoicemail;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = new ContactCenterToolArguments(arguments);
        var id = reader.GetString("id");
        var action = reader.GetString("action")?.ToLowerInvariant();

        if (id is null)
        {
            return "Nothing was done: give the id of the voicemail.";
        }

        var voicemails = services.GetRequiredService<ISharedVoicemailService>();

        var (result, done) = action switch
        {
            "claim" => (await voicemails.ClaimAsync(user, id, cancellationToken), "You are now handling this voicemail."),
            "release" => (await voicemails.ReleaseAsync(user, id, cancellationToken), "The voicemail was returned to the queue."),
            "resolve" => (await voicemails.ResolveAsync(user, id, reader.GetString("note"), cancellationToken), "The voicemail was marked as done."),
            _ => (null, null),
        };

        if (result is null)
        {
            return "Nothing was done: action must be one of: claim, release, resolve.";
        }

        if (!result.Succeeded)
        {
            return ListSharedVoicemailsTool.DescribeFailure(result);
        }

        return Json(new
        {
            id = result.Voicemail?.ItemId ?? id,
            status = result.Voicemail?.Status.ToString(),
            message = done,
        });
    }
}
