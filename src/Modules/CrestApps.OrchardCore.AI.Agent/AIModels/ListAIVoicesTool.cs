using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Speech;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Searches the voices of a text-to-speech deployment, as the Voice library page does, one page at a time with the
/// provider's cursor.
/// </summary>
public sealed class ListAIVoicesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIVoices";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "deploymentName": {
          "type": "string",
          "description": "The technical name of the text-to-speech deployment whose voices to search. Defaults to the first one."
        },
        "query": {
          "type": "string",
          "description": "Words to search the voices for, for example calm or narrator."
        },
        "language": {
          "type": "string",
          "description": "Only voices of this language, for example en or en-GB."
        },
        "gender": {
          "type": "string",
          "enum": ["Male", "Female", "Neutral"],
          "description": "Only voices of this gender, where the provider knows it."
        },
        "cursor": {
          "type": "string",
          "description": "The nextCursor of the previous result, to get the next page."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many voices per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Searches the voices of a text-to-speech deployment by text and language and returns their ids and names, with a cursor for the next page. Use it to find a voice id for a profile or persona.";

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
        arguments.TryGetFirstString("query", out var query);
        arguments.TryGetFirstString("language", out var language);
        arguments.TryGetFirstString("gender", out var genderText);
        arguments.TryGetFirstString("cursor", out var cursor);
        var pageSize = Math.Clamp(arguments.GetFirstValueOrDefault("pageSize", AgentToolResults.DefaultPageSize), 1, AgentToolResults.MaxPageSize);

        SpeechVoiceGender? gender = null;

        if (!string.IsNullOrWhiteSpace(genderText))
        {
            if (!Enum.TryParse<SpeechVoiceGender>(genderText.Trim(), ignoreCase: true, out var parsed) || parsed == SpeechVoiceGender.Unknown)
            {
                return "gender must be Male, Female or Neutral.";
            }

            gender = parsed;
        }

        var (deployment, library, error) = await VoiceLibraryLookup.FindAsync(services, deploymentName, cancellationToken);

        if (library is null)
        {
            return error;
        }

        VoiceSearchResult result;

        try
        {
            result = await library.SearchAsync(new VoiceSearchRequest
            {
                Query = string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
                Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim(),
                Gender = gender,
                Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim(),
                PageSize = pageSize,
            }, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return $"The provider did not list its voices: {exception.Message}";
        }

        var voices = (result?.Voices ?? []).Take(pageSize).ToList();

        return AgentToolResults.Json(new
        {
            deploymentName = deployment.Name,
            supportsCloning = library.SupportsCloning,
            items = voices.Select(voice => new
            {
                id = voice.Id,
                name = voice.Name,
                language = voice.Language,
                gender = voice.Gender == SpeechVoiceGender.Unknown ? null : voice.Gender.ToString(),
            }).ToList(),
            pageSize,
            nextCursor = result?.NextCursor,
            truncated = !string.IsNullOrEmpty(result?.NextCursor),
            libraryPath = AIModelToolSupport.GetVoiceLibraryPath(services, deployment.Name),
        });
    }
}
