using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agents.Workflows;

public sealed class ListWorkflowActivitiesTool : AIFunction
{
    public const string TheName = "listWorkflowActivities";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IActivityLibrary _activityLibrary;

    public ListWorkflowActivitiesTool(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IActivityLibrary activityLibrary)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _activityLibrary = activityLibrary;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
              "required": [],
              "additionalProperties": false
            }     
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "List all available workflow activities like tasks and events.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageWorkflows))
        {
            return "The current user does not have permission to manage workflows.";
        }

        if (!arguments.TryGetFirst<string>("workflowTypeId", out var workflowTypeId))
        {
            return "Unable to find a workflowTypeId argument in the function arguments.";
        }

        var activities = _activityLibrary.ListActivities();

        if (!activities.Any())
        {
            return "There are no available activities.";
        }

        return JsonSerializer.Serialize(activities.Select(x => new
        {
            x.Name,
            x.DisplayText,
            x.Category,
            IsEvent = x is IEvent,
            IsTask = x is ITask,
        }));
    }
}
