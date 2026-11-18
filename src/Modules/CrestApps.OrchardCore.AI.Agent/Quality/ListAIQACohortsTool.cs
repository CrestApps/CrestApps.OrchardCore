using System.Text.Json;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// Lists the QA cohorts, as the QA page does: the profiles each scores, its sample, criteria, passing score and judge,
/// and whether it is on.
/// </summary>
public sealed class ListAIQACohortsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIQACohorts";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many cohorts per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists QA cohorts: profiles covered, sample percent, criteria, passing score, judge deployment and whether each is on.";

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

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var cohorts = (await services.GetRequiredService<IAIQACohortStore>().GetAllAsync(cancellationToken))
            .OrderBy(cohort => cohort.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = (await services.GetRequiredService<IAIProfileManager>().GetAllAsync(cancellationToken))
            .Where(profile => !string.IsNullOrEmpty(profile.ItemId))
            .GroupBy(profile => profile.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var items = cohorts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cohort => new
            {
                id = cohort.ItemId,
                name = cohort.Name,
                enabled = cohort.Enabled,
                profiles = (cohort.ProfileIds ?? []).Select(profileId => new
                {
                    id = profileId,
                    title = profiles.TryGetValue(profileId, out var profile)
                        ? (string.IsNullOrWhiteSpace(profile.DisplayText) ? profile.Name : profile.DisplayText)
                        : null,
                }).ToList(),
                samplePercent = cohort.SamplePercent,
                criteria = cohort.Criteria,
                passingScore = cohort.PassingScore,
                checkKnowledge = cohort.CheckKnowledge,
                judgeDeploymentName = cohort.JudgeDeploymentName,
                label = cohort.GetLabel(),
                editPath = QualityToolSupport.GetCohortEditPath(services, cohort.ItemId),
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            items,
            total = cohorts.Count,
            page,
            pageSize,
            truncated = page * pageSize < cohorts.Count,
        });
    }
}
