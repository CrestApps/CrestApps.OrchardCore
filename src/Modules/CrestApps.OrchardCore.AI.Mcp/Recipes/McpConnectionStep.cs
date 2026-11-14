using System.Text.Json.Nodes;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Mcp.Services;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Recipes;

/// <summary>
/// Imports MCP client connections, matched by their item id.
/// </summary>
internal sealed class McpConnectionStep : CatalogRecipeStepBase<McpConnection>
{
    public const string StepKey = "McpConnection";

    private readonly ISourceCatalogManager<McpConnection> _manager;
    private readonly McpClientAIOptions _mcpClientOptions;
    private readonly ShellSettings _shellSettings;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpConnectionStep"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="mcpClientOptions">The mcp client options.</param>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, to find who runs the recipe.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpConnectionStep(
        ISourceCatalogManager<McpConnection> manager,
        IOptions<McpClientAIOptions> mcpClientOptions,
        ShellSettings shellSettings,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<McpConnectionStep> stringLocalizer)
        : base(StepKey, "Connections")
    {
        _manager = manager;
        _mcpClientOptions = mcpClientOptions.Value;
        _shellSettings = shellSettings;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    protected override ValueTask<McpConnection> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override ValueTask UpdateAsync(McpConnection entry, JsonObject token)
        => _manager.UpdateAsync(entry, token);

    protected override async ValueTask<McpConnection> PrepareUpdateAsync(McpConnection entry, JsonObject token)
    {
        // The package copies Source from the entry when it updates a connection, so an entry could turn a remote connection
        // into a local one that starts a process. A recipe never changes a connection's transport.
        var source = token[nameof(McpConnection.Source)] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        if (!string.IsNullOrEmpty(source) && !string.Equals(source, entry.Source, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The recipe can't change the transport of the MCP connection '{entry.ItemId}' from '{entry.Source}' to '{source}'. Remove the connection and import it again.");
        }

        // Changing an existing local connection changes the process it starts, so it needs the same right as creating one.
        if ((IsStdio(entry.Source) || IsStdio(source)) && !await CanManageStdioAsync())
        {
            throw new InvalidOperationException($"The recipe can't change the local (stdio) MCP connection '{entry.ItemId}': local connections need the 'Manage local (stdio) MCP connections' permission on the default tenant.");
        }

        return await base.PrepareUpdateAsync(entry, token);
    }

    protected override async ValueTask<McpConnection> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = token[nameof(McpConnection.Source)]?.GetValue<string>();

        if (string.IsNullOrEmpty(source))
        {
            context.Errors.Add(S["Could not find provider name. The deployment will not be imported."]);

            return null;
        }

        if (!_mcpClientOptions.TransportTypes.TryGetValue(source, out _))
        {
            context.Errors.Add(S["Invalid source used for MCP connection '{0}'.", source]);

            return null;
        }

        // A local connection starts a process on the server: a recipe imports one only on the default tenant, for a
        // caller who holds the dedicated permission (or for the host itself, when no one is signed in).
        if (IsStdio(source) && !await CanManageStdioAsync())
        {
            context.Errors.Add(S["The local (stdio) MCP connection was not imported: it needs the 'Manage local (stdio) MCP connections' permission on the default tenant."]);

            return null;
        }

        return await _manager.NewAsync(source, token);
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(McpConnection entry)
        => _manager.ValidateAsync(entry);

    protected override ValueTask CreateAsync(McpConnection entry)
        => _manager.CreateAsync(entry);

    private static bool IsStdio(string source)
        => string.Equals(source, McpConstants.TransportTypes.StdIo, StringComparison.OrdinalIgnoreCase);

    private Task<bool> CanManageStdioAsync()
        => McpStdioAccess.CanManageAsync(_shellSettings, _authorizationService, _httpContextAccessor.HttpContext?.User);
}
