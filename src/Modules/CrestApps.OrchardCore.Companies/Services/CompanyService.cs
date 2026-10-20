using System.Security.Claims;
using CrestApps.OrchardCore.Companies.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// The company a signed-in user buys for, and what they may do for it.
/// </summary>
public sealed class CompanyService
{
    private readonly ICompanyStore _store;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Dictionary<string, Company> _byUser = new(StringComparer.Ordinal);

    public CompanyService(ICompanyStore store, IHttpContextAccessor httpContextAccessor)
    {
        _store = store;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Returns the company a user buys for, when it's on, or <see langword="null"/>.
    /// </summary>
    public async Task<Company> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        if (!_byUser.TryGetValue(userId, out var company))
        {
            company = await _store.FindByBuyerAsync(userId, cancellationToken);
            _byUser[userId] = company is { IsEnabled: true } ? company : null;
        }

        return _byUser[userId];
    }

    /// <summary>
    /// Returns the company the signed-in user buys for and the buyer they are, or nulls.
    /// </summary>
    public async Task<(Company Company, CompanyBuyer Buyer)> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var company = await GetForUserAsync(userId, cancellationToken);

        return (company, company?.FindBuyer(userId));
    }
}
