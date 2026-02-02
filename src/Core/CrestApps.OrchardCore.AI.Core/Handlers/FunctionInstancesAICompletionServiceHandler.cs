using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class FunctionInstancesAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly IAIToolsService _toolsService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FunctionInstancesAICompletionServiceHandler(
        IAIToolsService toolsService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _toolsService = toolsService;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task ConfigureAsync(CompletionServiceConfigureContext context)
    {
        if (!context.IsFunctionInvocationSupported ||
            context.CompletionContext is null ||
            context.CompletionContext.InstanceIds is null ||
            context.CompletionContext.InstanceIds.Length == 0)
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        var user = _httpContextAccessor.HttpContext?.User;

        foreach (var instanceId in context.CompletionContext.InstanceIds)
        {
            // Verify user has permission to access this tool instance
            if (user is not null)
            {
                if (!await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, instanceId as object))
                {
                    continue;
                }
            }

            var tool = await _toolsService.GetByInstanceIdAsync(instanceId);

            if (tool is null)
            {
                continue;
            }

            context.ChatOptions.Tools.Add(tool);
        }
    }
}

