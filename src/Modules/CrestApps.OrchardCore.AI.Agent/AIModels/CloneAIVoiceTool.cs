using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Speech;
using CrestApps.OrchardCore.AI.Agent.Media;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Clones a voice at the provider of a text-to-speech deployment, as the Voice library page does, from a recording in
/// the media library instead of an upload. The provider may charge for it.
/// </summary>
public sealed class CloneAIVoiceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cloneAIVoice";

    /// <summary>
    /// The largest recording the Voice library page takes, 10 MB.
    /// </summary>
    public const long MaxSampleBytes = 10 * 1024 * 1024;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "deploymentName": {
          "type": "string",
          "description": "The technical name of the text-to-speech deployment whose provider clones the voice."
        },
        "name": {
          "type": "string",
          "description": "The name of the new voice."
        },
        "language": {
          "type": "string",
          "description": "The language the voice speaks, for example en."
        },
        "mediaPath": {
          "type": "string",
          "description": "The path of the recording in the media library, for example voices/ceo.mp3. At most 10 MB."
        }
      },
      "required": ["deploymentName", "name", "mediaPath"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a cloned voice at the provider from an audio file in the media library (up to 10 MB) and returns its id. Only for providers that support cloning; the provider may charge for it.";

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
        arguments.TryGetFirstString("name", out var name);
        arguments.TryGetFirstString("language", out var language);
        arguments.TryGetFirstString("mediaPath", out var mediaPath);

        if (string.IsNullOrWhiteSpace(deploymentName) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(mediaPath))
        {
            return "Nothing was cloned: give the deploymentName, a name for the voice and the mediaPath of the recording.";
        }

        var mediaFileStore = services.GetService<IMediaFileStore>();

        if (mediaFileStore is null)
        {
            return "Nothing was cloned: the media library is not available on this site, so there is no recording to read.";
        }

        if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, mediaPath, out var path))
        {
            return $"Nothing was cloned: {MediaToolSupport.DescribeInvalidPath(mediaPath)}";
        }

        if (!await MediaToolSupport.CanManagePathAsync(services, path))
        {
            return $"Nothing was cloned: you are not allowed to use the media file '{path}'.";
        }

        var (deployment, library, error) = await VoiceLibraryLookup.FindAsync(services, deploymentName, cancellationToken);

        if (library is null)
        {
            return $"Nothing was cloned: {error}";
        }

        if (!library.SupportsCloning)
        {
            return $"Nothing was cloned: the provider of '{deployment.Name}' does not clone voices.";
        }

        var file = await mediaFileStore.GetFileInfoAsync(path);

        if (file is null || file.IsDirectory)
        {
            return $"Nothing was cloned: there is no media file at '{path}'. Use listMediaFiles to find the recording.";
        }

        if (file.Length <= 0 || file.Length > MaxSampleBytes)
        {
            return $"Nothing was cloned: the recording must be between 1 byte and 10 MB, and '{path}' is {file.Length:N0} bytes.";
        }

        byte[] content;

        await using (var stream = await mediaFileStore.GetFileStreamAsync(path))
        using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory, cancellationToken);
            content = memory.ToArray();
        }

        if (!new FileExtensionContentTypeProvider().TryGetContentType(file.Name, out var contentType))
        {
            contentType = MediaToolSupport.DefaultMimeType;
        }

        try
        {
            var voice = await library.CloneAsync(new VoiceCloneRequest
            {
                Name = name.Trim(),
                Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim(),
                Samples =
                [
                    new VoiceCloneSample
                    {
                        FileName = file.Name,
                        ContentType = contentType,
                        Content = content,
                    },
                ],
            }, cancellationToken);

            return AgentToolResults.Json(new
            {
                cloned = true,
                deploymentName = deployment.Name,
                voiceId = voice?.Id,
                name = voice?.Name ?? name.Trim(),
                language = voice?.Language,
                note = "Choose the voice id as a profile's or persona's voice.",
            });
        }
        catch (HttpRequestException exception)
        {
            return $"The provider did not clone the voice: {exception.Message}";
        }
    }
}
