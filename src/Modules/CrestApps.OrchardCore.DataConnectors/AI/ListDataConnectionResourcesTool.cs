using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Lists what a connection offers to read, as the data set editor's resource list does, through the same reader and
/// with the same 20 second limit.
/// </summary>
public sealed class ListDataConnectionResourcesTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listDataConnectionResources";

    private static readonly TimeSpan _discoveryTimeout = TimeSpan.FromSeconds(20);

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "connectionId": {
          "type": "string",
          "description": "The ID of the connection, from listDataConnections or listConnectedDataSets."
        },
        "search": {
          "type": "string",
          "description": "Text to find in the resource's name or display name, such as part of a table name."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1. Defaults to 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many resources a page holds, from 1 to 50. Defaults to 20."
        }
      },
      "required": ["connectionId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists what a connection offers to read, such as the tables and views of a database or the files of a folder. Use it to pick the resource of a new data set.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => DataConnectorPermissions.ManageConnectedDataSets;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var connectionId = ReadString(arguments, "connectionId");

        if (connectionId is null)
        {
            return "Give the ID of the connection in connectionId.";
        }

        var connection = await services.GetRequiredService<IDataConnectionStore>().FindByIdAsync(connectionId, cancellationToken);

        if (connection is null)
        {
            return $"No data connection has the ID '{connectionId}'. Call listDataConnections to find it.";
        }

        var descriptor = services.GetRequiredService<IDataConnectorManager>().GetDescriptor(connection.Connector);

        if (descriptor is null)
        {
            return $"The connector '{connection.Connector}' of the connection '{connection.DisplayText}' isn't enabled, so its resources can't be listed.";
        }

        if (!descriptor.Supports(DataConnectorCapabilities.Discovery))
        {
            return $"A {descriptor.DisplayName.Value} connection can't list its resources. Give the data set's resource yourself, as the connector expects it (for example an index name or an API path).";
        }

        IReadOnlyList<DataConnectorResource> resources;

        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(_discoveryTimeout);

            try
            {
                resources = await services.GetRequiredService<ConnectedDataReader>().DiscoverAsync(connection, timeout.Token);
            }
            catch (DataConnectorException exception)
            {
                return $"The resources of '{connection.DisplayText}' couldn't be listed: {exception.Message}";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return $"The connection '{connection.DisplayText}' didn't list its resources within {_discoveryTimeout.TotalSeconds} seconds.";
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                services.GetRequiredService<ILogger<ListDataConnectionResourcesTool>>().LogWarning(exception, "The resources of the data connection '{Connection}' couldn't be listed.", connection.Name);

                return "The resources couldn't be listed. See the site's log for details.";
            }
        }

        var search = ReadString(arguments, "search");
        var (page, pageSize) = ReadPaging(arguments);
        var matches = resources
            .Where(resource => resource is not null && (search is null
                || resource.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                || resource.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true))
            .ToArray();

        return Json(new
        {
            connection = new
            {
                id = connection.ItemId,
                displayText = connection.DisplayText,
                connector = connection.Connector,
            },
            items = matches
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(resource => new
                {
                    name = resource.Name,
                    displayName = resource.DisplayName,
                    kind = resource.Kind,
                })
                .ToArray(),
            total = matches.Length,
            page,
            pageSize,
            truncated = page * pageSize < matches.Length,
        });
    }
}
