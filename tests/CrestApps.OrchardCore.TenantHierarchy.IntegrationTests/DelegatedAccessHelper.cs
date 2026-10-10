using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Issues and redeems delegated access codes directly through the services, for the tests that check the rules of
/// the broker without a browser.
/// </summary>
internal static class DelegatedAccessHelper
{
    /// <summary>
    /// Builds the principal a parent user has after signing in to the parent.
    /// </summary>
    /// <param name="services">The services of the parent tenant.</param>
    /// <param name="userName">The parent user name.</param>
    public static async Task<ClaimsPrincipal> CreatePrincipalAsync(IServiceProvider services, string userName)
    {
        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var signInManager = services.GetRequiredService<SignInManager<IUser>>();
        var user = await userManager.FindByNameAsync(userName) ?? throw new InvalidOperationException($"No user '{userName}'.");

        return await signInManager.CreateUserPrincipalAsync(user);
    }

    /// <summary>
    /// Issues a one-time code for a child tenant and returns it with the callback address it was sent to.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="parent">The tenant name of the parent.</param>
    /// <param name="userName">The parent user name.</param>
    /// <param name="childTenantId">The tenant identifier of the child tenant.</param>
    /// <param name="codeVerifier">The PKCE verifier the code is bound to.</param>
    public static Task<(string Code, string Callback)> IssueCodeAsync(
        TenantHierarchyTestHost host,
        string parent,
        string userName,
        string childTenantId,
        string codeVerifier)
    {
        return host.InTenantAsync(parent, async services =>
        {
            var principal = await CreatePrincipalAsync(services, userName);
            var callback = await services.GetRequiredService<DelegatedAccessIssuer>()
                .IssueCodeAsync(principal, childTenantId, DelegatedAccessTokens.CreateCodeChallenge(codeVerifier), "state-value");

            if (callback is null)
            {
                return (null, null);
            }

            var query = QueryHelpers.ParseQuery(new Uri(callback).Query);

            return (query["code"].ToString(), callback);
        });
    }

    /// <summary>
    /// Redeems a code in a child tenant through its broker.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="child">The tenant name of the child tenant.</param>
    /// <param name="code">The code.</param>
    /// <param name="codeVerifier">The PKCE verifier.</param>
    public static Task<DelegatedAccessRedemption> RedeemAsync(TenantHierarchyTestHost host, string child, string code, string codeVerifier)
    {
        return host.InTenantAsync(child, services => services.GetRequiredService<ITenantHierarchyBroker>().RedeemCodeAsync(code, codeVerifier));
    }

    /// <summary>
    /// Issues and redeems a code, returning the session the child tenant gets.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="parent">The tenant name of the parent.</param>
    /// <param name="userName">The parent user name.</param>
    /// <param name="childEntry">The registry entry of the child tenant.</param>
    public static async Task<DelegatedAccessRedemption> EnterAsync(TenantHierarchyTestHost host, string parent, string userName, ChildTenantEntry childEntry)
    {
        var verifier = DelegatedAccessTokens.CreateToken();
        var (code, _) = await IssueCodeAsync(host, parent, userName, childEntry.TenantId, verifier);

        Assert.NotNull(code);

        var redemption = await RedeemAsync(host, childEntry.TenantName, code, verifier);

        Assert.NotNull(redemption);

        return redemption;
    }
}
