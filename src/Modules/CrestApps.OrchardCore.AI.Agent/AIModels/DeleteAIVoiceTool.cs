using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Deletes a voice at the provider of a text-to-speech deployment, as the Voice library page does. It cannot be undone.
/// </summary>
public sealed class DeleteAIVoiceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIVoice";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "deploymentName": {
          "type": "string",
          "description": "The technical name of the text-to-speech deployment whose provider holds the voice."
        },
        "voiceId": {
          "type": "string",
          "description": "The id of the voice, from listAIVoices."
        }
      },
      "required": ["deploymentName", "voiceId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a voice at the provider. It cannot be undone, and profiles or personas that use the voice lose it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIDeployments))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("deploymentName", out var deploymentName);
        arguments.TryGetFirstString("voiceId", out var voiceId);

        if (string.IsNullOrWhiteSpace(deploymentName) || string.IsNullOrWhiteSpace(voiceId))
        {
            return "Nothing was deleted: give the deploymentName and the voiceId, from listAIVoices.";
        }

        var (deployment, library, error) = await VoiceLibraryLookup.FindAsync(services, deploymentName, cancellationToken);

        if (library is null)
        {
            return $"Nothing was deleted: {error}";
        }

        try
        {
            await library.DeleteAsync(voiceId.Trim(), cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return $"The provider did not delete the voice: {exception.Message}";
        }

        return AgentToolResults.Json(new
        {
            deleted = true,
            deploymentName = deployment.Name,
            voiceId = voiceId.Trim(),
            note = "The voice is gone at the provider. Profiles and personas that used it need another voice.",
        });
    }
}
