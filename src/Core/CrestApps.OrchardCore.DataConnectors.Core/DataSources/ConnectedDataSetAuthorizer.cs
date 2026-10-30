using System.Security.Claims;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.DataConnectors.DataSources;

/// <summary>
/// Decides who may read a connected data set.
/// </summary>
/// <remarks>
/// The same permission guards a data set whether it is read live or from an import. An import runs with the
/// connection's credentials and no user, so copying the rows locally never lets anyone read them who could not read
/// the live data set. No user, as when nobody is signed in, reads nothing.
/// </remarks>
internal sealed class ConnectedDataSetAuthorizer
{
    private readonly IAuthorizationService _authorizationService;

    public ConnectedDataSetAuthorizer(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<bool> CanReadAsync(ClaimsPrincipal user, ConnectedDataSet dataSet)
    {
        if (user is null || dataSet is null || string.IsNullOrEmpty(dataSet.Name))
        {
            return false;
        }

        return await _authorizationService.AuthorizeAsync(user, DataConnectorPermissions.CreateReadPermission(dataSet.Name));
    }
}
