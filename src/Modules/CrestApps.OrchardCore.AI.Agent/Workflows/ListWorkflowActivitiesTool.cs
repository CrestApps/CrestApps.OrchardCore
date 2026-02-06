using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

public sealed class ListWorkflowActivitiesTool : AIFunction
{
    public const string TheName = "listWorkflowActivities";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "List all available workflow activities like tasks and events.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var activityLibrary = arguments.Services.GetRequiredService<IActivityLibrary>();

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageWorkflows))
        {
            return "The current user does not have permission to manage workflows.";
        }

        if (!arguments.TryGetFirst<string>("workflowTypeId", out var workflowTypeId))
        {
            return "Unable to find a workflowTypeId argument in the function arguments.";
        }

        var activities = activityLibrary.ListActivities();

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
