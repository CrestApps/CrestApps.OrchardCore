using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Core.Services;
using CrestApps.OrchardCore.Users.Filters;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Confirming, with an emailed code, that a signed-in user's address is theirs: the "Confirm it's you" pages and what
/// they share with signing in by code. Signing in with a code and the Customers feature depend on it.
/// </summary>
[Feature(UsersConstants.Feature.EmailOwnership)]
public sealed class EmailOwnershipStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailOwnershipStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant's configuration.</param>
    public EmailOwnershipStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<EmailSignInCodes>();
        services.AddScoped<IEmailSignInCodeSender, EmailSignInCodeSender>();
        services.AddScoped<EmailSignInEligibility>();
        services.AddScoped<EmailOwnershipProof>();
        services.AddScoped<IEmailOwnershipConfirmationPage, EmailOwnershipConfirmationPage>();

        // A proof that resets an account's earlier credentials ends its earlier sessions at once, not at the next
        // security stamp check.
        services.AddSingleton<UserCredentialsResets>();
        services.AddTransient<IPostConfigureOptions<CookieAuthenticationOptions>, CredentialsResetCookieValidation>();

        // A confirmation link proves the address of an account with no credentials, with or without the Customers feature.
        services.AddEmailOwnershipConfirmation();
        services.Configure<EmailSignInOptions>(_shellConfiguration.GetSection(EmailSignInOptions.SectionName));

        // A user a code signed in to an account with credentials nobody proved are theirs is told on every page.
        services.Configure<MvcOptions>(options => options.Filters.Add<EmailOwnershipNoticeFilter>());

        // Counted on the shared attempt limiter, across nodes, whether or not Rate Limits is on.
        services
            .AddAttemptPolicy(EmailSignInCodes.SendNetworkPolicy, 10, TimeSpan.FromMinutes(15))
            .AddAttemptPolicy(EmailSignInCodes.SendEmailPolicy, 3, TimeSpan.FromMinutes(15))
            .AddAttemptPolicy(EmailSignInCodes.VerifyNetworkPolicy, 30, TimeSpan.FromMinutes(15))
            .AddAttemptPolicyOverrides(_shellConfiguration.GetSection(AttemptLimitOptions.SectionName));
    }
}
