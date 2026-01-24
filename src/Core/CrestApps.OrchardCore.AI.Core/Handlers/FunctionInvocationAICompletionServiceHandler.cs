using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class FunctionInvocationAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly IAIToolsService _toolsService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FunctionInvocationAICompletionServiceHandler(
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
            context.CompletionContext.ToolNames is null ||
            context.CompletionContext.ToolNames.Length == 0)
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        var user = _httpContextAccessor.HttpContext?.User;

        foreach (var toolName in context.CompletionContext.ToolNames)
        {
            // Verify user has permission to access this tool
            if (user is not null && !await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, toolName))
            {
                continue;
            }

            var tool = await _toolsService.GetByNameAsync(toolName);

            if (tool is null)
            {
                continue;
            }

            context.ChatOptions.Tools.Add(tool);
        }
    }
}
