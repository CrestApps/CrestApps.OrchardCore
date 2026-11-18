using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// Creates an AI simulation test case or updates one, with the checks of the test case editor, optionally starting
/// from a real conversation as the "Create test from conversation" action does. Saving runs nothing.
/// </summary>
public sealed class SaveAITestCaseTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAITestCase";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "testCaseId": {
          "type": "string",
          "description": "The ID of the test case to change, from listAITestCases. Leave it out to create a test case."
        },
        "fromConversationId": {
          "type": "string",
          "description": "For a new test case: the ID of a real conversation (chat session) to start from. Its customer lines become the scenario and its profile the profile under test; other arguments override what it gives."
        },
        "name": {
          "type": "string",
          "description": "The name of the test case."
        },
        "profileId": {
          "type": "string",
          "description": "The ID of the AI profile to test."
        },
        "profileVersion": {
          "type": "integer",
          "minimum": 0,
          "description": "A saved version of the profile to test, from listAIProfileVersions. 0 tests the current profile."
        },
        "scenario": {
          "type": "string",
          "description": "Who the simulated customer is and what they want, written as instructions to the customer model."
        },
        "criteria": {
          "type": "string",
          "description": "What the judge checks to pass the conversation, one criterion per line."
        },
        "variables": {
          "type": "object",
          "additionalProperties": { "type": "string" },
          "description": "Conversation variables the profile sees, as names and texts. It replaces the stored list."
        },
        "mockedTools": {
          "type": "object",
          "additionalProperties": { "type": "string" },
          "description": "Tool names and the result each returns during the test instead of running, so a test changes nothing. It replaces the stored list."
        },
        "maxTurns": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The most customer turns, 1 to 50. Defaults to 8."
        },
        "passingScore": {
          "type": "integer",
          "minimum": 0,
          "maximum": 100,
          "description": "The judge's score needed to pass, 0 to 100. Defaults to 70."
        },
        "customerDeploymentName": {
          "type": "string",
          "description": "The deployment that plays the customer. Empty uses the site default."
        },
        "judgeDeploymentName": {
          "type": "string",
          "description": "The deployment that judges the conversation. Empty uses the site default."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a simulation test case: profile, optional profile version, customer scenario, pass criteria, variables, mocked tool results, turn limit, passing score and the customer and judge deployments. Pass fromConversationId to start from a real conversation. Saving runs nothing; use runAITests.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIQuality))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var store = services.GetRequiredService<IAITestCaseStore>();
        var clock = services.GetRequiredService<IClock>();
        arguments.TryGetFirstString("testCaseId", out var testCaseId);
        arguments.TryGetFirstString("fromConversationId", out var fromConversationId);

        AITestCase testCase;
        var isNew = string.IsNullOrWhiteSpace(testCaseId);

        if (!isNew)
        {
            if (!string.IsNullOrWhiteSpace(fromConversationId))
            {
                return "Nothing was saved: fromConversationId starts a new test case; leave testCaseId out to use it.";
            }

            testCase = await store.FindByIdAsync(testCaseId.Trim(), cancellationToken);

            if (testCase is null)
            {
                return $"Nothing was saved: no test case has the ID '{testCaseId}'. Use listAITestCases to find it.";
            }
        }
        else if (!string.IsNullOrWhiteSpace(fromConversationId))
        {
            var session = await services.GetRequiredService<IAIChatSessionManager>().FindByIdAsync(fromConversationId.Trim(), cancellationToken);

            if (session is null)
            {
                return $"Nothing was saved: no conversation has the ID '{fromConversationId}'. Use listAIConversations to find it.";
            }

            var prompts = await services.GetRequiredService<IAIChatSessionPromptStore>().GetPromptsAsync(session.SessionId);
            testCase = AITestCaseFactory.FromConversation(session, prompts);
        }
        else
        {
            testCase = new AITestCase();
        }

        var refusal = Apply(arguments, testCase);

        if (refusal is not null)
        {
            return $"Nothing was saved: {refusal}";
        }

        var errors = AITestCaseValidator.Validate(testCase, services.GetRequiredService<IStringLocalizer<SaveAITestCaseTool>>());

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {QualityToolSupport.Describe(errors)}";
        }

        if (await services.GetRequiredService<IAIProfileManager>().FindByIdAsync(testCase.ProfileId, cancellationToken) is null)
        {
            return $"Nothing was saved: no AI profile has the ID '{testCase.ProfileId}'. Use listAIProfiles to find it.";
        }

        if (isNew)
        {
            testCase.ItemId = UniqueId.GenerateId();
            testCase.CreatedUtc = clock.UtcNow;

            await store.CreateAsync(testCase, cancellationToken);
        }
        else
        {
            testCase.ModifiedUtc = clock.UtcNow;

            await store.UpdateAsync(testCase, cancellationToken);
        }

        return AgentToolResults.Json(new
        {
            created = isNew,
            updated = !isNew,
            id = testCase.ItemId,
            name = testCase.Name,
            profileId = testCase.ProfileId,
            profileVersion = testCase.ProfileVersion,
            fromConversationId = testCase.SourceSessionId,
            scenario = testCase.Scenario,
            criteria = testCase.Criteria,
            variables = testCase.Variables,
            mockedTools = testCase.MockedTools.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            maxTurns = testCase.MaxTurns,
            passingScore = testCase.PassingScore,
            note = "Saving runs nothing. Run it with runAITests and read the outcome with listAITestRuns.",
            editPath = QualityToolSupport.GetTestCaseEditPath(services, testCase.ItemId),
        });
    }

    private static string Apply(AIFunctionArguments arguments, AITestCase testCase)
    {
        if (QualityToolSupport.TryReadText(arguments, "name", out var name))
        {
            testCase.Name = name;
        }

        if (QualityToolSupport.TryReadText(arguments, "profileId", out var profileId))
        {
            testCase.ProfileId = profileId;
        }

        if (!QualityToolSupport.TryReadInt(arguments, "profileVersion", out var profileVersion))
        {
            return "profileVersion must be a whole number.";
        }

        if (profileVersion is not null)
        {
            testCase.ProfileVersion = profileVersion == 0 ? null : profileVersion;
        }

        if (QualityToolSupport.TryReadText(arguments, "scenario", out var scenario))
        {
            testCase.Scenario = scenario;
        }

        if (QualityToolSupport.TryReadText(arguments, "criteria", out var criteria))
        {
            testCase.Criteria = criteria;
        }

        if (!QualityToolSupport.TryReadTextMap(arguments, "variables", out var variables))
        {
            return "variables must be an object of names and texts.";
        }

        if (variables is not null)
        {
            testCase.Variables = variables;
        }

        if (!QualityToolSupport.TryReadTextMap(arguments, "mockedTools", out var mockedTools))
        {
            return "mockedTools must be an object of tool names and results.";
        }

        if (mockedTools is not null)
        {
            testCase.MockedTools = mockedTools;
        }

        if (!QualityToolSupport.TryReadInt(arguments, "maxTurns", out var maxTurns))
        {
            return "maxTurns must be a whole number.";
        }

        if (maxTurns is not null)
        {
            testCase.MaxTurns = maxTurns.Value;
        }

        if (!QualityToolSupport.TryReadInt(arguments, "passingScore", out var passingScore))
        {
            return "passingScore must be a whole number.";
        }

        if (passingScore is not null)
        {
            testCase.PassingScore = passingScore.Value;
        }

        if (QualityToolSupport.TryReadText(arguments, "customerDeploymentName", out var customerDeploymentName))
        {
            testCase.CustomerDeploymentName = customerDeploymentName;
        }

        if (QualityToolSupport.TryReadText(arguments, "judgeDeploymentName", out var judgeDeploymentName))
        {
            testCase.JudgeDeploymentName = judgeDeploymentName;
        }

        return null;
    }
}
