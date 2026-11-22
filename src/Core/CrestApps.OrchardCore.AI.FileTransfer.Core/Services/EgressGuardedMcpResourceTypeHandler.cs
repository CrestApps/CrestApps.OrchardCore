using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core.Services;

/// <summary>
/// Holds a remote-file MCP resource type of CrestApps.Core (FTP, SFTP) to the <see cref="FileTransferEgressPolicy"/>:
/// every read checks the server before the package connects.
/// </summary>
internal sealed class EgressGuardedMcpResourceTypeHandler : IMcpResourceTypeHandler
{
    private readonly IMcpResourceTypeHandler _inner;
    private readonly FileTransferEgressPolicy _policy;
    private readonly Func<McpResource, IEnumerable<(string Field, string Host)>> _hosts;

    public EgressGuardedMcpResourceTypeHandler(IMcpResourceTypeHandler inner, FileTransferEgressPolicy policy, Func<McpResource, IEnumerable<(string Field, string Host)>> hosts)
    {
        _inner = inner;
        _policy = policy;
        _hosts = hosts;
    }

    public string Type => _inner.Type;

    public Task<ReadResourceResult> ReadAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        foreach (var (_, host) in _hosts(resource))
        {
            _policy.EnsureAllowed(host);
        }

        return _inner.ReadAsync(resource, variables, cancellationToken);
    }
}
