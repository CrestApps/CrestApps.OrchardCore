using CrestApps.Core;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Mcp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// The local (stdio) MCP transport for Orchard Core. It starts the connection's command as a process on the server,
/// so it refuses to on any tenant other than the default one (a child tenant that had the feature enabled before it
/// became default-tenant-only keeps the feature but can't start anything), and only starts a command the host's
/// <see cref="McpStdioOptions.AllowedCommands"/> list allows when one is set.
/// </summary>
internal sealed class GuardedStdioClientTransportProvider : IMcpClientTransportProvider
{
    private readonly ShellSettings _shellSettings;
    private readonly McpStdioOptions _options;
    private readonly ILogger _logger;
    private readonly StdioClientTransportProvider _inner = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardedStdioClientTransportProvider"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="options">The host's stdio settings.</param>
    /// <param name="logger">The logger.</param>
    public GuardedStdioClientTransportProvider(
        ShellSettings shellSettings,
        IOptions<McpStdioOptions> options,
        ILogger<GuardedStdioClientTransportProvider> logger)
    {
        _shellSettings = shellSettings;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanHandle(McpConnection connection)
        => connection?.Source == McpConstants.TransportTypes.StdIo;

    /// <inheritdoc/>
    public Task<IClientTransport> GetAsync(McpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!_shellSettings.IsDefaultShell())
        {
            _logger.LogWarning("The local MCP connection '{ConnectionId}' was not started: tenant '{Tenant}' is not the default tenant.", connection.ItemId, _shellSettings.Name);

            throw new InvalidOperationException("Local (stdio) MCP connections can only run on the default tenant.");
        }

        if (connection.TryGet<StdioMcpConnectionMetadata>(out var metadata) && !IsAllowed(metadata.Command, _options.AllowedCommands))
        {
            _logger.LogWarning("The local MCP connection '{ConnectionId}' was not started: its command is not in the host's allowed commands.", connection.ItemId);

            throw new InvalidOperationException("The command of this local (stdio) MCP connection is not one the host allows.");
        }

        return _inner.GetAsync(connection);
    }

    /// <summary>
    /// Whether a command may be started under the allowlist. An empty list allows any command. A bare command name, such
    /// as <c>npx</c> or <c>npx.cmd</c>, must be in the list with or without its extension. A command given as a path is
    /// allowed only when that exact path is in the list: with <c>["npx"]</c>, <c>\\host\share\npx.exe</c> is refused.
    /// The list limits the program only; its arguments are not checked.
    /// </summary>
    /// <param name="command">The command to start.</param>
    /// <param name="allowedCommands">The commands the host allows.</param>
    internal static bool IsAllowed(string command, IReadOnlyCollection<string> allowedCommands)
    {
        if (allowedCommands is null || allowedCommands.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.Trim();

        if (trimmed.IndexOfAny(['/', '\\']) >= 0)
        {
            return allowedCommands.Any(allowed => string.Equals(allowed?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        }

        var withoutExtension = Path.GetFileNameWithoutExtension(trimmed);

        return allowedCommands.Any(allowed =>
            string.Equals(allowed, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(allowed, withoutExtension, StringComparison.OrdinalIgnoreCase));
    }
}
