using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Deletes a shared voicemail and erases its recording, as the Delete button of the Shared voicemail page does.
/// <see cref="ISharedVoicemailService"/> authorizes the delete and refuses a recording under legal hold.
/// </summary>
internal sealed class DeleteSharedVoicemailTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteSharedVoicemail";

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
    public override string Description => "Deletes a shared voicemail and its recording. Refused under legal hold. Cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ManageSharedVoicemail;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = new ContactCenterToolArguments(arguments).GetString("id");

        if (id is null)
        {
            return "Nothing was deleted: give the id of the voicemail.";
        }

        var result = await services.GetRequiredService<ISharedVoicemailService>().DeleteAsync(user, id, cancellationToken);

        if (!result.Succeeded)
        {
            return ListSharedVoicemailsTool.DescribeFailure(result);
        }

        return Json(new
        {
            deleted = true,
            id = result.Voicemail?.ItemId ?? id,
            message = "The voicemail was deleted and its recording erased.",
        });
    }
}
