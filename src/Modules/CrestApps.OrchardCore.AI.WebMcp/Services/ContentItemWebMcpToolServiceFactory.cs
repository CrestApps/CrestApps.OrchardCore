using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.WebMcp.Models;
using CrestApps.Core.AI.WebMcp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Creates the WebMCP tool service of one content item: CrestApps.Core's <see cref="DefaultWebMcpToolService"/>,
/// limited to the tools the item's <see cref="Models.WebMcpPart"/> lists. It still checks that each tool is selectable
/// and that the user may use it.
/// </summary>
internal sealed class ContentItemWebMcpToolServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<AIToolDefinitionOptions> _toolDefinitions;
    private readonly IAIToolAccessEvaluator _toolAccessEvaluator;
    private readonly ILogger<DefaultWebMcpToolService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemWebMcpToolServiceFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The request's service provider, which creates the tools.</param>
    /// <param name="toolDefinitions">The registered AI tool definitions.</param>
    /// <param name="toolAccessEvaluator">The evaluator of the per-tool permission.</param>
    /// <param name="logger">The logger.</param>
    public ContentItemWebMcpToolServiceFactory(
        IServiceProvider serviceProvider,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAIToolAccessEvaluator toolAccessEvaluator,
        ILogger<DefaultWebMcpToolService> logger)
    {
        _serviceProvider = serviceProvider;
        _toolDefinitions = toolDefinitions;
        _toolAccessEvaluator = toolAccessEvaluator;
        _logger = logger;
    }

    /// <summary>
    /// Creates the tool service for the given tool names.
    /// </summary>
    /// <param name="tools">The tool and tool instance names the content item exposes.</param>
    /// <returns>The tool service.</returns>
    public IWebMcpToolService Create(IEnumerable<string> tools)
    {
        var options = new WebMcpOptions
        {
            ExposeAllTools = false,
            Tools = [.. (tools ?? []).Where(name => !string.IsNullOrWhiteSpace(name))],
        };

        return new DefaultWebMcpToolService(
            _serviceProvider,
            _toolDefinitions,
            new FixedOptionsMonitor<WebMcpOptions>(options),
            _toolAccessEvaluator,
            _logger);
    }
}
