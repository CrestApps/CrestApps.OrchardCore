using CrestApps.OrchardCore.Users.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Revokes the OpenID Connect tokens issued to an account when a proof of its address resets its credentials.
/// </summary>
[Feature(UsersConstants.Feature.EmailOwnership)]
[RequireFeatures(OrchardCoreFeatureIds.OpenIdServer)]
public sealed class EmailCodeSignInOpenIdStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUserSignInRevocation, OpenIdUserSignInRevocation>();
    }
}
