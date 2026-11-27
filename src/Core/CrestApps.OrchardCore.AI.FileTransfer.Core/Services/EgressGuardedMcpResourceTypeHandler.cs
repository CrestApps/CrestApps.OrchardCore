using CrestApps.Core;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core.Services;

/// <summary>
/// Holds a remote-file MCP resource type of CrestApps.Core (FTP, SFTP) to the <see cref="FileTransferEgressPolicy"/>:
/// every read checks the server before the package connects, and hands the package a copy of the resource that names
/// the addresses that were checked (review 3).
/// </summary>
internal sealed class EgressGuardedMcpResourceTypeHandler : IMcpResourceTypeHandler
{
    private readonly IMcpResourceTypeHandler _inner;
    private readonly FileTransferEgressPolicy _policy;
    private readonly Func<McpResource, IEnumerable<(string Field, string Host)>> _hosts;
    private readonly Action<McpResource, FileTransferEgressPolicy> _prepare;

    public EgressGuardedMcpResourceTypeHandler(
        IMcpResourceTypeHandler inner,
        FileTransferEgressPolicy policy,
        Func<McpResource, IEnumerable<(string Field, string Host)>> hosts,
        Action<McpResource, FileTransferEgressPolicy> prepare = null)
    {
        _inner = inner;
        _policy = policy;
        _hosts = hosts;
        _prepare = prepare;
    }

    public string Type => _inner.Type;

    public Task<ReadResourceResult> ReadAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);

        foreach (var (_, host) in _hosts(resource))
        {
            _policy.EnsureAllowed(host);
        }

        if (_prepare is null)
        {
            return _inner.ReadAsync(resource, variables, cancellationToken);
        }

        // A copy, so the checked addresses never replace the names the resource stores. Clone shares the properties.
        var copy = resource.Clone();
        copy.Properties = resource.Properties?.Clone();
        _prepare(copy, _policy);

        return _inner.ReadAsync(copy, variables, cancellationToken);
    }
}
