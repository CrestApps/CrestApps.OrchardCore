using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Lists and calls the storefront tools: the registered, selectable AI tools that are marked safe for anonymous
/// callers (<see cref="AnonymousAIToolOptions"/>) and that the WebMCP settings select. Tool instances and unmarked
/// tools are never offered, whoever calls.
/// </summary>
internal sealed class StorefrontWebMcpToolService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly AnonymousAIToolOptions _anonymousTools;
    private readonly IOptionsMonitor<WebMcpOptions> _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorefrontWebMcpToolService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The request services, which resolve the tools and are given to them.</param>
    /// <param name="toolDefinitions">The registered AI tool definitions.</param>
    /// <param name="anonymousTools">The tools marked safe for anonymous callers.</param>
    /// <param name="options">The WebMCP tool selection.</param>
    /// <param name="logger">The logger.</param>
    public StorefrontWebMcpToolService(
        IServiceProvider serviceProvider,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IOptions<AnonymousAIToolOptions> anonymousTools,
        IOptionsMonitor<WebMcpOptions> options,
        ILogger<StorefrontWebMcpToolService> logger)
    {
        _serviceProvider = serviceProvider;
        _toolDefinitions = toolDefinitions.Value;
        _anonymousTools = anonymousTools.Value;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Lists the storefront tools.
    /// </summary>
    /// <returns>The tools, by the function name the agent calls them by.</returns>
    public IReadOnlyList<WebMcpToolDescriptor> GetTools()
        => ResolveTools()
            .Select(tool => tool.Descriptor)
            .ToList();

    /// <summary>
    /// Calls one of the storefront tools.
    /// </summary>
    /// <param name="name">The function name of the tool.</param>
    /// <param name="arguments">The tool's arguments, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The tool's answer; <see cref="WebMcpToolInvocationStatus.NotFound"/> when it is not a storefront tool, whatever
    /// the reason; or <see cref="WebMcpToolInvocationStatus.Failed"/>, without the error details, when it throws.
    /// </returns>
    public async Task<WebMcpToolInvocationResult> InvokeAsync(string name, IDictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return WebMcpToolInvocationResult.NotFound();
        }

        var function = ResolveTools()
            .FirstOrDefault(tool => string.Equals(tool.Descriptor.Name, name, StringComparison.Ordinal))
            .Function;

        if (function is null)
        {
            return WebMcpToolInvocationResult.NotFound();
        }

        var functionArguments = new AIFunctionArguments
        {
            Services = _serviceProvider,
        };

        foreach (var argument in arguments ?? new Dictionary<string, object>())
        {
            functionArguments[argument.Key] = argument.Value;
        }

        try
        {
            // A WebMCP call stands in for a top-level completion, so it opens the invocation scope tools expect.
            using var invocationScope = AIInvocationScope.Begin();

            var result = await function.InvokeAsync(functionArguments, cancellationToken);

            return WebMcpToolInvocationResult.Success(Format(result));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The WebMCP storefront tool '{ToolName}' failed.", name);

            return WebMcpToolInvocationResult.Failure("The tool failed. The details are in the site's log.");
        }
    }

    private List<(WebMcpToolDescriptor Descriptor, AIFunction Function)> ResolveTools()
    {
        var options = _options.CurrentValue;
        var selected = new HashSet<string>((options.Tools ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()), StringComparer.OrdinalIgnoreCase);
        var tools = new List<(WebMcpToolDescriptor Descriptor, AIFunction Function)>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, definition) in _toolDefinitions.Tools)
        {
            if (!definition.IsSelectable() ||
                !_anonymousTools.IsSafe(key) ||
                (!options.ExposeAllTools && !selected.Contains(key) && !(definition.Name is { } name && selected.Contains(name))))
            {
                continue;
            }

            var function = CreateFunction(key);

            if (function is null || !seenNames.Add(function.Name))
            {
                continue;
            }

            tools.Add((new WebMcpToolDescriptor
            {
                Name = function.Name,
                Title = string.IsNullOrWhiteSpace(definition.Title) ? function.Name : definition.Title,
                Description = string.IsNullOrWhiteSpace(function.Description) ? definition.Description : function.Description,
                InputSchema = function.JsonSchema,
            }, function));
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
            _logger.LogError(ex, "The AI tool '{ToolName}' could not be created for the WebMCP storefront tools.", key);

            return null;
        }
    }

    // The same text CrestApps.Core's WebMCP endpoints answer with.
    private static string Format(object result)
        => AIToolResultText.Normalize(result) switch
        {
            null => string.Empty,
            string text => text,
            JsonElement element => element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText(),
            JsonNode node => node.ToJsonString(),
            TextContent content => content.Text,
            IEnumerable<AIContent> contents => string.Join('\n', contents.OfType<TextContent>().Select(content => content.Text)),
            var other => JsonSerializer.Serialize(other, other.GetType()),
        };
}
