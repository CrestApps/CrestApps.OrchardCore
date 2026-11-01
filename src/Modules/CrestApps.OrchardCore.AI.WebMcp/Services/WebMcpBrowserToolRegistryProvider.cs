using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Adds the page tools of the user's browser tab to the chat completions of that user, for the AI profiles that
/// allow page tools.
/// </summary>
internal sealed class WebMcpBrowserToolRegistryProvider : IToolRegistryProvider
{
    private readonly IWebMcpBrowserToolRegistry _registry;
    private readonly IWebMcpBrowserToolInvoker _invoker;
    private readonly IAIProfileManager _profileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpBrowserToolRegistryProvider"/> class.
    /// </summary>
    /// <param name="registry">The registry of the page tools each user's tab offers.</param>
    /// <param name="invoker">The invoker that runs page tools.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    public WebMcpBrowserToolRegistryProvider(
        IWebMcpBrowserToolRegistry registry,
        IWebMcpBrowserToolInvoker invoker,
        IAIProfileManager profileManager)
    {
        _registry = registry;
        _invoker = invoker;
        _profileManager = profileManager;
    }

    /// <summary>
    /// Gets the page tools for a chat completion: none unless the completion belongs to a signed-in user's chat
    /// session, the user's tab offers page tools, and the session's profile allows them.
    /// </summary>
    /// <param name="context">The completion context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool registry entries.</returns>
    public async Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(AICompletionContext context, CancellationToken cancellationToken = default)
    {
        if (context is null ||
            context.DisableTools ||
            !context.AdditionalProperties.TryGetValue(AICompletionContextKeys.Session, out var value) ||
            value is not AIChatSession session ||
            string.IsNullOrEmpty(session.UserId) ||
            string.IsNullOrEmpty(session.ProfileId) ||
            !_registry.TryGet(session.UserId, out var registration) ||
            registration.Tools.Count == 0)
        {
            return [];
        }

        var profile = await _profileManager.FindByIdAsync(session.ProfileId, cancellationToken);

        if (profile is null ||
            !profile.TryGetSettings<WebMcpProfileSettings>(out var settings) ||
            !settings.AllowPageTools)
        {
            return [];
        }

        return registration.Tools
            .Select(tool => new ToolRegistryEntry
            {
                Id = $"webmcp:{tool.Name}",
                Name = tool.Name,
                Description = tool.Description,
                Source = ToolRegistryEntrySource.Local,
                SourceId = WebMcpConstants.Feature.Chat,
                CreateAsync = _ => ValueTask.FromResult<AITool>(new WebMcpBrowserToolFunction(tool, registration.ConnectionId, _invoker)),
            })
            .ToList();
    }
}
