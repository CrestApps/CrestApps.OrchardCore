using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Sources;

internal sealed class McpConnectionDeploymentSource : DeploymentSourceBase<McpConnectionDeploymentStep>
{
    private readonly ISourceCatalog<McpConnection> _store;

    public McpConnectionDeploymentSource(ISourceCatalog<McpConnection> store)
    {
        _store = store;
    }

    protected override async Task ProcessAsync(McpConnectionDeploymentStep step, DeploymentPlanResult result)
    {
        var connections = await _store.GetAllAsync();

        var connectionsData = new JsonArray();

        var connectionIds = step.IncludeAll
            ? []
            : step.ConnectionIds ?? [];

        foreach (var connection in connections)
        {
            if (connectionIds.Length > 0 && !connectionIds.Contains(connection.ItemId))
            {
                continue;
            }

            var deploymentInfo = new JsonObject()
            {
                { "ItemId", connection.ItemId },
                { "DisplayText", connection.DisplayText },
                { "Author", connection.Author },
                { "CreatedUtc" , connection.CreatedUtc },
                { "OwnerId" , connection.OwnerId },
            };

            var properties = new JsonObject();

            foreach (var property in connection.Properties)
            {
                properties[property.Key] = property.Value.DeepClone();
            }

            SanitizeSensitiveData(connection, properties);

            deploymentInfo["Properties"] = properties;

            connectionsData.Add(deploymentInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["connections"] = connectionsData,
        });
    }

    private static void SanitizeSensitiveData(McpConnection connection, JsonObject properties)
    {
        if (!string.Equals(connection.Source, McpConstants.TransportTypes.Sse, StringComparison.Ordinal))
        {
            return;
        }

        var metadataNode = properties[nameof(SseMcpConnectionMetadata)]?.AsObject();

        if (metadataNode == null)
        {
            return;
        }

        // Clear all sensitive fields to prevent accidental credential exposure.
        metadataNode[nameof(SseMcpConnectionMetadata.ApiKey)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.BasicPassword)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.OAuth2ClientSecret)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.OAuth2PrivateKey)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.OAuth2ClientCertificate)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.OAuth2ClientCertificatePassword)] = string.Empty;
    }
}
