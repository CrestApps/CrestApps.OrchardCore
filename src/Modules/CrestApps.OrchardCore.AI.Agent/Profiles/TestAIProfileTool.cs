using System.Text.Json;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Sends one message to an AI profile with its tools turned off and returns the reply, the way the A2A host answers a
/// message, so an edit can be checked. It makes a paid model call, and only for a profile the caller may chat with.
/// </summary>
public sealed class TestAIProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "testAIProfile";

    private const int MaxMessageLength = 4000;

    private const int MaxReplyLength = 8000;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "profileId": {
          "type": "string",
          "description": "The ID of the profile to test, from listAIProfiles."
        },
        "message": {
          "type": "string",
          "maxLength": 4000,
          "description": "The message to send, as a user of the profile would write it."
        }
      },
      "required": ["profileId", "message"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends one message to a profile and returns its reply, with the profile's tools turned off. Use it to check an edit. It makes a paid model call.";

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

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("profileId", out var profileId);
        arguments.TryGetFirstString("message", out var message);

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profile = string.IsNullOrWhiteSpace(profileId) ? null : await profileManager.FindByIdAsync(profileId.Trim(), cancellationToken);

        // The permission is checked on the profile, as the chat checks it, so a profile-specific grant counts.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.QueryAnyAIProfile, profile))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (profile is null)
        {
            return $"No AI profile has the ID '{profileId}'. Use listAIProfiles to find the ID.";
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return "Nothing was sent: give the message to send.";
        }

        if (message.Length > MaxMessageLength)
        {
            return $"Nothing was sent: the message is longer than {MaxMessageLength} characters.";
        }

        // Building the tested profile's context writes its data source into the running turn's context; the running
        // assistant keeps its own once the test is done.
        var invocation = AIInvocationScope.Current;
        var runningDataSourceId = invocation?.DataSourceId;

        try
        {
            var completionContextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
            var completionContext = await completionContextBuilder.BuildAsync(profile, cancellationToken: cancellationToken);
            completionContext.DisableTools = true;

            var deployment = await services.GetRequiredService<IAIDeploymentManager>().ResolveSlotAsync(
                AIDeploymentSlotNames.Chat,
                deploymentName: completionContext.ChatDeploymentName,
                cancellationToken: cancellationToken);

            if (deployment is null)
            {
                return $"Nothing was sent: the profile '{AIConfigurationGuard.GetTitle(profile)}' has no chat deployment and the site has no default one.";
            }

            var completion = await services.GetRequiredService<IAICompletionService>().CompleteAsync(
                deployment,
                [new ChatMessage(ChatRole.User, message.Trim())],
                completionContext,
                cancellationToken);

            var reply = completion?.Messages?.LastOrDefault(item => item.Role == ChatRole.Assistant)?.Text
                ?? completion?.Text;

            return AgentToolResults.Json(new
            {
                profileId = profile.ItemId,
                title = AIConfigurationGuard.GetTitle(profile),
                deployment = deployment.Name,
                toolsTurnedOff = true,
                reply = string.IsNullOrEmpty(reply) ? null : reply.Length > MaxReplyLength ? reply[..MaxReplyLength] + "…" : reply,
                note = string.IsNullOrEmpty(reply) ? "The model returned no text." : null,
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetService<ILogger<TestAIProfileTool>>()?.LogError(exception, "Testing the AI profile '{ProfileId}' failed.", profile.ItemId);

            return $"The test message could not be sent to the profile '{AIConfigurationGuard.GetTitle(profile)}': the model call failed. Check the profile's deployment on its page: {AIProfileToolSupport.GetProfileEditPath(services, profile.ItemId)}";
        }
        finally
        {
            if (invocation is not null)
            {
                invocation.DataSourceId = runningDataSourceId;
            }
        }
    }
}
