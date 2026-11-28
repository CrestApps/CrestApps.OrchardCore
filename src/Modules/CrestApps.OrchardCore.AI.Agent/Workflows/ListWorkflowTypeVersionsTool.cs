using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

/// <summary>
/// Lists the published versions of a workflow, the most recent first, from the workflow type version store, as the
/// Versions panel of the designer does. The panel's draft state, instance counts and comparison need services of the
/// Workflows module and are not offered.
/// </summary>
public sealed class ListWorkflowTypeVersionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listWorkflowTypeVersions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "workflowTypeId": {
          "type": "string",
          "description": "The workflow type ID, numeric ID or exact name of the workflow, from listWorkflowTypes."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of versions per page, 20 by default and 50 at most."
        }
      },
      "required": [
        "workflowTypeId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists a workflow's published versions, the most recent first: number, name, who published it, when, and its activity and transition counts, with the version that runs now marked.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, WorkflowsPermissions.ManageWorkflows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var reference = arguments.GetFirstValueOrDefault<string>("workflowTypeId");
        var workflowType = await WorkflowToolSupport.FindWorkflowTypeAsync(services.GetRequiredService<IWorkflowTypeStore>(), reference);

        if (workflowType is null)
        {
            return $"There is no workflow '{reference}'. Call listWorkflowTypes to find its ID.";
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var localClock = services.GetRequiredService<ILocalClock>();
        var versions = (await services.GetRequiredService<IWorkflowTypeVersionStore>().ListAsync(workflowType.WorkflowTypeId)).ToList();
        var items = new List<object>();

        foreach (var version in versions.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(new
            {
                versionId = version.VersionId,
                version = version.Version,
                name = version.Name,
                isPublished = version.VersionId == workflowType.VersionId,
                createdAt = await AgentToolResults.ToLocalAsync(localClock, version.CreatedUtc),
                createdBy = version.CreatedByUserName,
                activities = version.Activities.Count,
                transitions = version.Transitions.Count,
                variables = version.Variables.Count,
            });
        }

        return AgentToolResults.Json(new
        {
            workflowTypeId = workflowType.WorkflowTypeId,
            name = workflowType.Name,
            items,
            total = versions.Count,
            page,
            pageSize,
            truncated = versions.Count > page * pageSize,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            note = "Unpublished designer changes and the differences between versions are shown in the designer, not by this tool.",
            designerUrl = WorkflowToolSupport.GetDesignerPath(services, workflowType),
        });
    }
}
