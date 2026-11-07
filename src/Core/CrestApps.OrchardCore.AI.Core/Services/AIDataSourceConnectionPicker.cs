using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// The data connection picker of the external AI data source editors (Elasticsearch, Azure AI Search): an AI data
/// source may reach its index through a data connection of the Data Connectors feature instead of storing an endpoint
/// and credentials of its own. Only people who may manage data connections pick one, because a data source that uses
/// a connection uses its credentials; for everyone else the stored choice is kept as it is.
/// </summary>
public sealed class AIDataSourceConnectionPicker
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataConnectionResolver _resolver;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceConnectionPicker"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the current user.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="resolver">The data connection resolver; <see langword="null"/> while the Data Connectors feature is off.</param>
    public AIDataSourceConnectionPicker(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AIDataSourceConnectionPicker> stringLocalizer,
        IDataConnectionResolver resolver = null)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
        _resolver = resolver;
    }

    /// <summary>
    /// Reads the identifier of the data connection a data source uses.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <returns>The identifier, or <see langword="null"/> when the data source uses its own settings.</returns>
    public static string GetConnectionId(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return dataSource.TryGet<DataConnectionReference>(out var reference) && !string.IsNullOrWhiteSpace(reference.ConnectionId)
            ? reference.ConnectionId
            : null;
    }

    /// <summary>
    /// Tells whether the current user may pick a data connection.
    /// </summary>
    /// <returns><see langword="true"/> when the Data Connectors feature is on and the user may manage data connections.</returns>
    public async Task<bool> CanPickAsync()
        => _resolver is not null &&
            _httpContextAccessor.HttpContext?.User is { } user &&
            await _authorizationService.AuthorizeAsync(user, DataConnectorPermissions.ManageDataConnections);

    /// <summary>
    /// Lists the connections of the given connectors, after an option that keeps the data source's own settings.
    /// </summary>
    /// <param name="connectorNames">The connectors whose connections the data source can use.</param>
    /// <param name="selectedId">The identifier of the selected connection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The options.</returns>
    public async Task<List<SelectListItem>> GetOptionsAsync(IReadOnlyCollection<string> connectorNames, string selectedId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectorNames);

        var options = new List<SelectListItem>
        {
            new(S["None: enter the connection settings below"], string.Empty, string.IsNullOrEmpty(selectedId)),
        };

        if (_resolver is null)
        {
            return options;
        }

        foreach (var connection in await _resolver.ListAsync(connectorNames, cancellationToken))
        {
            options.Add(new SelectListItem(
                string.IsNullOrWhiteSpace(connection.DisplayText) ? connection.Name : connection.DisplayText,
                connection.ItemId,
                string.Equals(connection.ItemId, selectedId, StringComparison.Ordinal)));
        }

        return options;
    }

    /// <summary>
    /// Stores the connection picked in the editor. A person who may not pick a connection keeps the stored choice.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="postedConnectionId">The posted connection identifier; empty for the data source's own settings.</param>
    /// <param name="connectorNames">The connectors whose connections the data source can use.</param>
    /// <param name="context">The editor update context.</param>
    /// <param name="prefix">The editor's prefix.</param>
    /// <param name="fieldName">The name of the picker field, for the validation message.</param>
    /// <returns>The identifier of the connection the data source now uses, or <see langword="null"/> for its own settings.</returns>
    public async Task<string> UpdateAsync(
        AIDataSource dataSource,
        string postedConnectionId,
        IReadOnlyCollection<string> connectorNames,
        UpdateEditorContext context,
        string prefix,
        string fieldName)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(connectorNames);
        ArgumentNullException.ThrowIfNull(context);

        if (!await CanPickAsync())
        {
            return GetConnectionId(dataSource);
        }

        if (string.IsNullOrWhiteSpace(postedConnectionId))
        {
            dataSource.Remove<DataConnectionReference>();

            return null;
        }

        var resolution = await _resolver.ResolveForAsync(postedConnectionId, connectorNames);

        if (!resolution.Succeeded)
        {
            context.Updater.ModelState.AddModelError(prefix, fieldName, S["Pick a data connection that exists and fits this data source."]);
        }

        dataSource.Put(new DataConnectionReference
        {
            ConnectionId = postedConnectionId.Trim(),
        });

        return postedConnectionId.Trim();
    }
}
