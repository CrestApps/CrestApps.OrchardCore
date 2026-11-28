using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// Makes a simulation test case from a real conversation, as the "Create test from conversation" action of the test
/// cases page does (the customer's lines become the scenario, the conversation's profile and variables are kept), and
/// saves it with the checks of the test case editor. Other changes to a test case go through the generic record tools
/// (kind <c>aiTestCase</c>). Saving runs nothing.
/// </summary>
public sealed class CreateAITestCaseFromConversationTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "createAITestCaseFromConversation";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "conversationId": {
          "type": "string",
          "description": "The ID of the real conversation (chat session) to replay, from listAIConversations. Its customer lines become the scenario and its profile the profile under test."
        },
        "name": {
          "type": "string",
          "description": "The name of the test case. Defaults to the conversation's title."
        },
        "criteria": {
          "type": "string",
          "description": "What the judge checks to pass the conversation, one criterion per line. Defaults to: the customer's need is met, or they are told clearly what happens next."
        }
      },
      "required": [
        "conversationId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Turns a real conversation into a simulation test case, so a conversation that went wrong can be replayed until the AI handles it: the customer's lines become the scenario and the conversation's profile and variables are kept. Change the test later with saveRecord (kind aiTestCase); saving runs nothing, so run it with runAITests.";

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

        // The check of the test cases page.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIQuality))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("conversationId", out var conversationId) || string.IsNullOrWhiteSpace(conversationId))
        {
            return "Nothing was saved: give conversationId, the ID of the conversation to replay. Use listAIConversations to find it.";
        }

        var session = await services.GetRequiredService<IAIChatSessionManager>().FindByIdAsync(conversationId.Trim(), cancellationToken);

        if (session is null)
        {
            return $"Nothing was saved: no conversation has the ID '{conversationId.Trim()}'. Use listAIConversations to find it.";
        }

        var prompts = await services.GetRequiredService<IAIChatSessionPromptStore>().GetPromptsAsync(session.SessionId);
        var testCase = AITestCaseFactory.FromConversation(session, prompts);

        if (QualityToolSupport.TryReadText(arguments, "name", out var name) && name is not null)
        {
            testCase.Name = name;
        }

        if (QualityToolSupport.TryReadText(arguments, "criteria", out var criteria) && criteria is not null)
        {
            testCase.Criteria = criteria;
        }

        // The checks of the test case editor, which the page runs when the filled-in form is saved.
        var errors = AITestCaseValidator.Validate(testCase, services.GetRequiredService<IStringLocalizer<CreateAITestCaseFromConversationTool>>());

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {QualityToolSupport.Describe(errors)}";
        }

        if (await services.GetRequiredService<IAIProfileManager>().FindByIdAsync(testCase.ProfileId, cancellationToken) is null)
        {
            return $"Nothing was saved: the conversation's AI profile '{testCase.ProfileId}' no longer exists. Create the test case with saveRecord (kind aiTestCase) and another profile.";
        }

        testCase.ItemId = UniqueId.GenerateId();
        testCase.CreatedUtc = services.GetRequiredService<IClock>().UtcNow;

        await services.GetRequiredService<IAITestCaseStore>().CreateAsync(testCase, cancellationToken);

        return AgentToolResults.Json(new
        {
            created = true,
            id = testCase.ItemId,
            name = testCase.Name,
            profileId = testCase.ProfileId,
            profileVersion = testCase.ProfileVersion,
            fromConversationId = testCase.SourceSessionId,
            scenario = testCase.Scenario,
            criteria = testCase.Criteria,
            variables = testCase.Variables.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            maxTurns = testCase.MaxTurns,
            passingScore = testCase.PassingScore,
            note = "The scenario quotes what the customer wrote, as data for the simulated customer. Saving runs nothing: run it with runAITests and read the outcome with listAITestRuns. Change the turn limit, mocked tool results or deployments with saveRecord (kind aiTestCase).",
            editPath = QualityToolSupport.GetTestCaseEditPath(services, testCase.ItemId),
        });
    }
}
