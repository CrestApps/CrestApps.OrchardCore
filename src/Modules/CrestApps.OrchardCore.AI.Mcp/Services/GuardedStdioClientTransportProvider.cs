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

    // Environment variables that change which program runs or what it loads before its own code: with them a connection
    // could make an allowed command run something else. Whenever the host lists its allowed commands, a connection sets
    // only the variables the host lists too; these are allowed only when listed by their full name.
    private static readonly string[] _programChangingVariables =
    [
        "PATH", "PATHEXT", "COMSPEC", "SHELL", "BASH_ENV", "ENV",
        "NODE_OPTIONS", "NODE_PATH", "NPM_CONFIG_PREFIX", "NPM_CONFIG_REGISTRY", "NPM_CONFIG_USERCONFIG",
        "PYTHONPATH", "PYTHONHOME", "PYTHONSTARTUP", "PYTHONUSERBASE",
        "LD_PRELOAD", "LD_LIBRARY_PATH", "LD_AUDIT", "DYLD_INSERT_LIBRARIES", "DYLD_LIBRARY_PATH", "DYLD_FRAMEWORK_PATH",
        "PERL5OPT", "PERL5LIB", "RUBYOPT", "RUBYLIB", "JAVA_TOOL_OPTIONS", "_JAVA_OPTIONS", "DOTNET_STARTUP_HOOKS",
        "UV_INDEX_URL", "UV_EXTRA_INDEX_URL", "PIP_INDEX_URL", "PIP_EXTRA_INDEX_URL", "DOCKER_HOST",
    ];

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

        if (_options.AllowedCommands.Length > 0 && FindRefusedVariable(metadata?.EnvironmentVariables, _options.AllowedEnvironmentVariables) is { } variable)
        {
            _logger.LogWarning("The local MCP connection '{ConnectionId}' was not started: it sets an environment variable the host does not allow.", connection.ItemId);

            throw new InvalidOperationException($"This local (stdio) MCP connection sets {variable}, which the host's allowed environment variables don't include.");
        }

        return _inner.GetAsync(connection);
    }

    /// <summary>
    /// Whether a command may be started under the allowlist. An empty list allows any command. A bare command name, such
    /// as <c>npx</c> or <c>npx.cmd</c>, must be in the list with or without its extension. A command given as a path is
    /// allowed only when that exact path is in the list: with <c>["npx"]</c>, <c>\\host\share\npx.exe</c> is refused.
    /// The list limits the program only; its arguments and working directory are not checked, so <c>npx -y</c> with any
    /// package name still runs that package. Environment variables that change which program runs are refused separately.
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

    /// <summary>
    /// Returns the first environment variable a connection sets that the host does not allow, or <see langword="null"/>.
    /// A list of names to refuse could never be complete (<c>CORECLR_PROFILER_PATH</c>, <c>HOME</c> for <c>.npmrc</c>,
    /// <c>GIT_SSH_COMMAND</c> and many more change what runs), so only the names the host lists are allowed.
    /// </summary>
    /// <param name="variables">The connection's environment variables.</param>
    /// <param name="allowedVariables">The variables the host allows: full names, or prefixes ending with <c>*</c>.</param>
    internal static string FindRefusedVariable(IReadOnlyDictionary<string, string> variables, IReadOnlyCollection<string> allowedVariables)
        => variables?.Keys.FirstOrDefault(name => !IsAllowedVariable(name?.Trim(), allowedVariables));

    private static bool IsAllowedVariable(string name, IReadOnlyCollection<string> allowedVariables)
    {
        if (string.IsNullOrEmpty(name) || allowedVariables is null)
        {
            return false;
        }

        var changesProgram = _programChangingVariables.Contains(name, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in allowedVariables)
        {
            var allowed = entry?.Trim();

            if (string.IsNullOrEmpty(allowed))
            {
                continue;
            }

            if (string.Equals(allowed, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!changesProgram &&
                allowed.EndsWith('*') &&
                name.StartsWith(allowed[..^1], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
