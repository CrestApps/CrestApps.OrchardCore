using CrestApps.OrchardCore.Customers.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// A Customers settings section, shown and saved only for a user who may manage customers.
/// </summary>
/// <typeparam name="TSettings">The settings the section edits.</typeparam>
public abstract class CustomerSettingsDisplayDriverBase<TSettings> : SiteDisplayDriver<TSettings>
    where TSettings : new()
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerSettingsDisplayDriverBase{TSettings}"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor of the current request, whose user is authorized.</param>
    /// <param name="authorizationService">The service that checks the user's permissions.</param>
    protected CustomerSettingsDisplayDriverBase(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Whether the current user may manage customers, and so see and change the section.
    /// </summary>
    protected Task<bool> CanManageCustomersAsync()
        => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CustomerPermissions.ManageCustomers);
}
