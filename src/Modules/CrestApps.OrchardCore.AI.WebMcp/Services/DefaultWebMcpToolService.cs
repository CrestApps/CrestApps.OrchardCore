using System.Security.Claims;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Lists and calls the AI tools a user may give to browser agents, from the same tool catalog, tool instances and
/// per-tool permission the rest of the AI modules use.
/// </summary>
public sealed class DefaultWebMcpToolService : IWebMcpToolService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly WebMcpOptions _options;
    private readonly IAIToolAccessEvaluator _toolAccessEvaluator;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultWebMcpToolService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The request services, which resolve the tools and are given to them.</param>
    /// <param name="toolDefinitions">The registered AI tool definitions.</param>
    /// <param name="options">The effective WebMCP settings.</param>
    /// <param name="toolAccessEvaluator">The evaluator of the per-tool permission.</param>
    /// <param name="logger">The logger.</param>
    public DefaultWebMcpToolService(
        IServiceProvider serviceProvider,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IOptionsSnapshot<WebMcpOptions> options,
        IAIToolAccessEvaluator toolAccessEvaluator,
        ILogger<DefaultWebMcpToolService> logger)
    {
        _serviceProvider = serviceProvider;
        _toolDefinitions = toolDefinitions.Value;
        _options = options.Value;
        _toolAccessEvaluator = toolAccessEvaluator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebMcpToolDescriptor>> GetToolsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var tools = await ResolveToolsAsync(user, cancellationToken);

        return tools
            .Select(tool => tool.Descriptor)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<WebMcpToolInvocationResult> InvokeAsync(
        ClaimsPrincipal user,
        string name,
        IDictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(name))
        {
            return WebMcpToolInvocationResult.NotFound();
        }

        var tools = await ResolveToolsAsync(user, cancellationToken);
        var tool = tools.FirstOrDefault(candidate => string.Equals(candidate.Descriptor.Name, name, StringComparison.Ordinal));

        if (tool.Function is null)
        {
            return WebMcpToolInvocationResult.NotFound();
        }

        var functionArguments = new AIFunctionArguments
        {
            Services = _serviceProvider,
        };

        if (arguments is not null)
        {
            foreach (var argument in arguments)
            {
                functionArguments[argument.Key] = argument.Value;
            }
        }

        try
        {
            // A WebMCP call stands in for a top-level completion, so it opens the invocation scope tools expect.
            using var invocationScope = AIInvocationScope.Begin();

            var result = await tool.Function.InvokeAsync(functionArguments, cancellationToken);

            return WebMcpToolInvocationResult.Success(WebMcpToolResultFormatter.Format(result));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The WebMCP tool '{ToolName}' failed.", name);

            return WebMcpToolInvocationResult.Failure("The tool failed. The details are in the site's log.");
        }
    }

    private async Task<List<(WebMcpToolDescriptor Descriptor, AIFunction Function)>> ResolveToolsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var allowList = BuildAllowList(_options.Tools);
        var tools = new List<(WebMcpToolDescriptor Descriptor, AIFunction Function)>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, definition) in _toolDefinitions.Tools)
        {
            if (!definition.IsSelectable() ||
                !IsAllowed(allowList, key, definition.Name) ||
                !await _toolAccessEvaluator.IsAuthorizedAsync(user, key))
            {
                continue;
            }

            var function = CreateFunction(key);

            if (function is null || !seenNames.Add(function.Name))
            {
                continue;
            }

            tools.Add((CreateDescriptor(function, definition.Title, definition.Description), function));
        }

        var instanceCatalog = _serviceProvider.GetService<INamedCatalog<AIToolInstance>>();

        if (instanceCatalog is null)
        {
            return tools;
        }

        foreach (var instance in await instanceCatalog.GetAllAsync(cancellationToken))
        {
            if (string.IsNullOrEmpty(instance.Source))
            {
                continue;
            }

            var functionName = instance.GetFunctionName();

            if (!IsAllowed(allowList, functionName, instance.Name) ||
                !await _toolAccessEvaluator.IsAuthorizedAsync(user, functionName))
            {
                continue;
            }

            var function = CreateInstanceFunction(instance);

            if (function is null || !seenNames.Add(function.Name))
            {
                continue;
            }

            tools.Add((CreateDescriptor(function, instance.Name, instance.Description), function));
        }

        return tools;
    }

    private AIFunction CreateFunction(string key)
    {
        try
        {
            return _serviceProvider.GetKeyedService<AITool>(key) as AIFunction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The AI tool '{ToolName}' could not be created for WebMCP.", key);

            return null;
        }
    }

    private AIFunction CreateInstanceFunction(AIToolInstance instance)
    {
        try
        {
            var source = _serviceProvider.GetKeyedService<IAIToolInstanceSource>(instance.Source);

            return source?.CreateTool(instance) as AIFunction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The AI tool instance '{InstanceName}' could not be created for WebMCP.", instance.Name);

            return null;
        }
    }

    private static WebMcpToolDescriptor CreateDescriptor(AIFunction function, string title, string description)
    {
        return new WebMcpToolDescriptor
        {
            Name = function.Name,
            Title = string.IsNullOrWhiteSpace(title) ? function.Name : title,
            Description = string.IsNullOrWhiteSpace(function.Description) ? description : function.Description,
            InputSchema = function.JsonSchema,
        };
    }

    private HashSet<string> BuildAllowList(IEnumerable<string> names)
    {
        if (_options.ExposeAllTools)
        {
            return null;
        }

        var allowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names ?? [])
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                allowList.Add(name.Trim());
            }
        }

        return allowList;
    }

    private static bool IsAllowed(HashSet<string> allowList, params string[] candidates)
    {
        if (allowList is null)
        {
            return true;
        }

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) && allowList.Contains(candidate))
            {
                return true;
            }
        }

        return false;
    }
}
