using System.Text.Json;
using CrestApps.Core;
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
/// Creates a QA cohort or updates one, with the checks of the QA cohort editor. An enabled cohort has a judge model
/// score a sample of real conversations, which the provider charges for, until it is turned off.
/// </summary>
public sealed class SaveAIQACohortTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIQACohort";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "cohortId": {
          "type": "string",
          "description": "The ID of the cohort to change, from listAIQACohorts. Leave it out to create a cohort."
        },
        "name": {
          "type": "string",
          "description": "The name of the cohort. Required for a new cohort."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the cohort scores conversations. A new cohort is on unless this is false."
        },
        "profileIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "description": "The IDs of the AI profiles whose conversations are scored. It replaces the stored list. Required for a new cohort."
        },
        "samplePercent": {
          "type": "integer",
          "minimum": 1,
          "maximum": 100,
          "description": "The share of closed conversations scored, 1 to 100 percent. Defaults to 20."
        },
        "criteria": {
          "type": "string",
          "description": "What the judge checks, one criterion per line, for example 'The answer is accurate'."
        },
        "passingScore": {
          "type": "integer",
          "minimum": 0,
          "maximum": 100,
          "description": "The judge's score a conversation needs to pass, 0 to 100. Defaults to 70."
        },
        "checkKnowledge": {
          "type": "boolean",
          "description": "Whether the judge also checks the answers against the knowledge the AI was given. Defaults to true."
        },
        "judgeDeploymentName": {
          "type": "string",
          "description": "The deployment that judges the conversations. Empty uses the site default."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a QA cohort that scores a sample of real conversations with a judge model. An enabled cohort makes paid model calls for every sampled conversation until it is turned off.";

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

        var store = services.GetRequiredService<IAIQACohortStore>();
        var clock = services.GetRequiredService<IClock>();
        arguments.TryGetFirstString("cohortId", out var cohortId);

        AIQACohort cohort;
        var isNew = string.IsNullOrWhiteSpace(cohortId);

        if (isNew)
        {
            cohort = new AIQACohort();
        }
        else
        {
            cohort = await store.FindByIdAsync(cohortId.Trim(), cancellationToken);

            if (cohort is null)
            {
                return $"Nothing was saved: no QA cohort has the ID '{cohortId}'. Use listAIQACohorts to find it.";
            }
        }

        var refusal = Apply(arguments, cohort);

        if (refusal is not null)
        {
            return $"Nothing was saved: {refusal}";
        }

        var errors = AIQACohortValidator.Validate(cohort, services.GetRequiredService<IStringLocalizer<SaveAIQACohortTool>>());

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {QualityToolSupport.Describe(errors)}";
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var unknown = new List<string>();

        foreach (var profileId in cohort.ProfileIds)
        {
            if (await profileManager.FindByIdAsync(profileId, cancellationToken) is null)
            {
                unknown.Add(profileId);
            }
        }

        if (unknown.Count > 0)
        {
            return $"Nothing was saved: no AI profile has the ID {string.Join(", ", unknown.Select(id => $"'{id}'"))}. Use listAIProfiles to find them.";
        }

        if (isNew)
        {
            cohort.ItemId = UniqueId.GenerateId();
            cohort.CreatedUtc = clock.UtcNow;

            // A new cohort scores the conversations that close from now on, as the QA page does.
            cohort.CursorUtc = clock.UtcNow;

            await store.CreateAsync(cohort, cancellationToken);
        }
        else
        {
            await store.UpdateAsync(cohort, cancellationToken);
        }

        return AgentToolResults.Json(new
        {
            created = isNew,
            updated = !isNew,
            id = cohort.ItemId,
            name = cohort.Name,
            enabled = cohort.Enabled,
            profileIds = cohort.ProfileIds,
            samplePercent = cohort.SamplePercent,
            passingScore = cohort.PassingScore,
            checkKnowledge = cohort.CheckKnowledge,
            judgeDeploymentName = cohort.JudgeDeploymentName,
            label = cohort.GetLabel(),
            note = cohort.Enabled
                ? $"The cohort scores about {cohort.SamplePercent}% of the conversations that close from now on, each with a paid judge model call, until it is turned off. Read the scores with summarizeAIQuality and the label."
                : "The cohort is off and scores nothing.",
            editPath = QualityToolSupport.GetCohortEditPath(services, cohort.ItemId),
        });
    }

    private static string Apply(AIFunctionArguments arguments, AIQACohort cohort)
    {
        if (QualityToolSupport.TryReadText(arguments, "name", out var name))
        {
            cohort.Name = name;
        }

        if (QualityToolSupport.ReadBool(arguments, "enabled") is { } enabled)
        {
            cohort.Enabled = enabled;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "profileIds", out var profileIds))
        {
            return "profileIds must be a list of AI profile IDs.";
        }

        if (profileIds is not null)
        {
            cohort.ProfileIds = profileIds;
        }

        if (!QualityToolSupport.TryReadInt(arguments, "samplePercent", out var samplePercent))
        {
            return "samplePercent must be a whole number.";
        }

        if (samplePercent is not null)
        {
            cohort.SamplePercent = samplePercent.Value;
        }

        if (QualityToolSupport.TryReadText(arguments, "criteria", out var criteria))
        {
            cohort.Criteria = criteria;
        }

        if (!QualityToolSupport.TryReadInt(arguments, "passingScore", out var passingScore))
        {
            return "passingScore must be a whole number.";
        }

        if (passingScore is not null)
        {
            cohort.PassingScore = passingScore.Value;
        }

        if (QualityToolSupport.ReadBool(arguments, "checkKnowledge") is { } checkKnowledge)
        {
            cohort.CheckKnowledge = checkKnowledge;
        }

        if (QualityToolSupport.TryReadText(arguments, "judgeDeploymentName", out var judgeDeploymentName))
        {
            cohort.JudgeDeploymentName = judgeDeploymentName;
        }

        return null;
    }
}
