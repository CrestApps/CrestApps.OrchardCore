using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Calls back the person who left a shared voicemail from the caller's own soft phone, as the Call back button of the
/// Shared voicemail page does: <see cref="ISharedVoicemailService"/> authorizes and records the call-back, then the
/// caller's soft phone is asked to dial.
/// </summary>
internal sealed class CallBackSharedVoicemailTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "callBackSharedVoicemail";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The voicemail id, from listSharedVoicemails."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Asks the caller's own soft phone to dial the person who left the voicemail. Needs the user's soft phone to be open; the voicemail stays open until it is resolved.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.AccessSharedVoicemail;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = ContactCenterOperationsSupport.GetUserId(user);
        var id = new ContactCenterToolArguments(arguments).GetString("id");

        if (userId is null)
        {
            return NotAllowedMessage;
        }

        if (id is null)
        {
            return "Nothing was done: give the id of the voicemail.";
        }

        var softPhones = services.GetService<ISoftPhoneClientNotifier>();

        if (softPhones is null)
        {
            return "Nothing was done: calling back is not available because no soft phone is set up on this site.";
        }

        var result = await services.GetRequiredService<ISharedVoicemailService>().RequestCallbackAsync(user, id, cancellationToken);

        if (!result.Succeeded)
        {
            return ListSharedVoicemailsTool.DescribeFailure(result);
        }

        // The same push the call button beside a phone number sends: the caller's soft phone dials the number.
        await softPhones.DialRequestedAsync(userId, new TelephonyDialRequest
        {
            Number = result.Voicemail.CallerNumber,
        });

        return Json(new
        {
            id = result.Voicemail.ItemId,
            requested = true,
            message = "Your soft phone was asked to dial the caller. It does not mean the call connected: if your soft phone does not start the call, open it and try again. Mark the voicemail as done (updateSharedVoicemail with resolve) once you have spoken to the caller.",
        });
    }
}
