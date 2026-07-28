using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Tooling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// An <see cref="CrestApps.Core.AI.Tooling.IToolRegistryProvider"/> that surfaces configured
/// <see cref="AIToolInstance"/> entries to the AI model only when the current user is allowed to access
/// them. Access is evaluated through <see cref="IAIToolAccessEvaluator"/> using the instance's
/// model-facing function name, which maps to the Orchard Core <c>AccessAITool</c> permission and its
/// per-tool dynamic permission.
/// </summary>
/// <remarks>
/// This provider replaces the built-in registry provider, which is why the tool instances feature is
/// registered with <c>useDefaultRegistry: false</c>. Registering the built-in provider alongside this one
/// would expose every configured instance regardless of the caller's permissions.
/// </remarks>
public sealed class OrchardCoreToolInstanceRegistryProvider : ToolInstanceRegistryProvider
{
    private readonly IAIToolAccessEvaluator _toolAccessEvaluator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreToolInstanceRegistryProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the instance catalog and sources.</param>
    /// <param name="toolAccessEvaluator">The evaluator used to authorize access to a tool instance.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current user.</param>
    /// <param name="logger">The logger.</param>
    public OrchardCoreToolInstanceRegistryProvider(
        IServiceProvider serviceProvider,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ToolInstanceRegistryProvider> logger)
        : base(serviceProvider, logger)
    {
        _toolAccessEvaluator = toolAccessEvaluator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Determines whether the resolved tool instance may be surfaced to the AI model for the current user.
    /// Completions that run outside an HTTP request, such as workflows and background tasks, have no user
    /// to authorize, so the instance is included the same way locally registered tools are.
    /// </summary>
    /// <param name="instance">The resolved tool instance.</param>
    /// <param name="context">The completion context that scopes available tools.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the current user may use the instance; otherwise <see langword="false"/>.</returns>
    protected override async ValueTask<bool> ShouldIncludeInstanceAsync(
        AIToolInstance instance,
        AICompletionContext context,
        CancellationToken cancellationToken)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user is null)
        {
            return true;
        }

        return await _toolAccessEvaluator.IsAuthorizedAsync(user, instance.GetFunctionName());
    }
}
