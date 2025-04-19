using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Tools;

internal sealed class ProfileAwareAIToolSource : IAIToolSource
{
    public const string ToolSource = "ProfileAware";

    private readonly ILogger<ProfileAwareAIToolSource> _logger;
    private readonly IAICompletionService _completionService;
    private readonly INamedModelStore<AIProfile> _profileStore;

    public ProfileAwareAIToolSource(
        ILogger<ProfileAwareAIToolSource> logger,
        IAICompletionService completionService,
        INamedModelStore<AIProfile> profileStore,
        IStringLocalizer<ProfileAwareAIToolSource> S)
    {
        _logger = logger;
        _completionService = completionService;
        _profileStore = profileStore;
        DisplayName = S["Profile Invoker"];
        Description = S["Provides a function that calls other profile."];
    }

    public string Name
        => ToolSource;

    public AIToolSourceType Type
        => AIToolSourceType.Function;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }

    public async Task<AITool> CreateAsync(AIToolInstance instance)
    {
        if (!instance.TryGet<AIProfileFunctionMetadata>(out var metadata) || string.IsNullOrEmpty(metadata.ProfileId))
        {
            return new ProfileInvoker(_completionService, instance, profile: null, _logger);
        }

        var profile = await _profileStore.FindByIdAsync(metadata.ProfileId);

        return new ProfileInvoker(_completionService, instance, profile, _logger);
    }

    private sealed class ProfileInvoker : AIFunction
    {
        private const string PromptProperty = "Prompt";

        private readonly IAICompletionService _completionService;
        private readonly ILogger _logger;
        private readonly AIProfile _profile;

        public ProfileInvoker(
            IAICompletionService completionService,
            AIToolInstance instance,
            AIProfile profile,
            ILogger logger)
        {
            _completionService = completionService;
            _profile = profile;
            _logger = logger;

            var funcMetadata = instance.As<InvokableToolMetadata>();

            Name = instance.Id;
            Description = string.IsNullOrEmpty(funcMetadata.Description)
                ? "Provides a way to call another model."
                : funcMetadata.Description;

            JsonSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "prompt": {
                      "type": "string",
                      "description": "The user's prompt."
                    }
                  },
                  "required": ["prompt"],
                  "return_type": {
                    "type": "string",
                    "description": "The response to the user's prompt as a string."
                  }
                }
                """, JsonSerializerOptions);
        }

        public override string Name { get; }

        public override string Description { get; }

        public override JsonElement JsonSchema { get; }

        protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            if (_profile is null)
            {
                return Task.FromResult<object>("The profile does not exist.");
            }

            try
            {
                string promptString = null;

                var prompt = arguments.First(x => x.Key == PromptProperty).Value;

                if (prompt is JsonElement jsonElement)
                {
                    promptString = jsonElement.GetString();
                }
                else if (prompt is JsonNode jsonNode)
                {
                    promptString = jsonNode.ToJsonString();
                }
                else if (prompt is string str)
                {
                    promptString = str;
                }
                else
                {
                    promptString = prompt?.ToString();
                }

                var context = new AICompletionContext
                {
                    Profile = _profile,
                    DisableTools = true,
                };

                return await _completionService.CompleteAsync(_profile.Source, [new ChatMessage(ChatRole.User, promptString)], context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while invoking the profile with the id '{ProfileId}' and source '{Source}'.", _profile.Id, _profile.Source);

                return Task.FromResult<object>("Unable to get a response from the profile.");
            }
        }
    }
}
