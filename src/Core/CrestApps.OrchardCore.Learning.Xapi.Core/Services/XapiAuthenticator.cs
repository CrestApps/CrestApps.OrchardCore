using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.Security.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Services;

/// <summary>
/// The home page the LRS names the site by in agent accounts: the site's base URL when one is set, else the scheme,
/// host and path of the current request.
/// </summary>
public sealed class XapiHomePage
{
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="XapiHomePage"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public XapiHomePage(ISiteService siteService, IHttpContextAccessor httpContextAccessor)
    {
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the home page.
    /// </summary>
    /// <returns>The home page, without a trailing slash.</returns>
    public async Task<string> GetAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        if (!string.IsNullOrWhiteSpace(site?.BaseUrl))
        {
            return site.BaseUrl.TrimEnd('/');
        }

        var request = _httpContextAccessor.HttpContext?.Request;

        return request is null || !request.Host.HasValue
            ? "http://localhost"
            : $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
    }
}

/// <summary>
/// Signed, expiring launch tokens that open the LRS for one launch (<see cref="XapiLaunchScope"/>): made with the
/// tenant's ASP.NET Core Data Protection, with a purpose of their own and the expiry inside the protected payload,
/// checked against the site clock. A client sends one with HTTP Basic, with the user name
/// <see cref="BasicUserName"/> and the token as the password; <see cref="CreateBasicValue"/> makes that value.
/// </summary>
public sealed class XapiLaunchTokens
{
    /// <summary>
    /// The HTTP Basic user name of a launch token. No OpenID application can use it.
    /// </summary>
    public const string BasicUserName = "xapi-launch";

    private const string Purpose = "CrestApps.OrchardCore.Learning.Xapi.Launch";

    private readonly IDataProtector _protector;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="XapiLaunchTokens"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection.</param>
    /// <param name="clock">The clock.</param>
    public XapiLaunchTokens(IDataProtectionProvider dataProtectionProvider, IClock clock)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _clock = clock;
    }

    /// <summary>
    /// Makes a token for a launch.
    /// </summary>
    /// <param name="scope">What the token opens, with its expiry.</param>
    /// <returns>The token.</returns>
    public string Create(XapiLaunchScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return _protector.Protect(JsonSerializer.Serialize(scope));
    }

    /// <summary>
    /// Makes the value a client puts after <c>Basic</c> in the Authorization header: the user name
    /// <see cref="BasicUserName"/> and the token, in base64.
    /// </summary>
    /// <param name="scope">What the token opens.</param>
    /// <returns>The value.</returns>
    public string CreateBasicValue(XapiLaunchScope scope)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(BasicUserName + ":" + Create(scope)));

    /// <summary>
    /// Reads a token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>What it opens, or <see langword="null"/> when it was altered, made for another purpose or tenant, or
    /// has expired.</returns>
    public XapiLaunchScope Read(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 8192)
        {
            return null;
        }

        try
        {
            var scope = JsonSerializer.Deserialize<XapiLaunchScope>(_protector.Unprotect(token));

            return scope is null || scope.ExpiresUtc <= _clock.UtcNow ? null : scope;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Finds the credential of an LRS request. Only three kinds count, and never the sign-in cookie: a bearer token of
/// Orchard Core's <c>Api</c> scheme (an OpenID application with client credentials); HTTP Basic with an OpenID
/// application's client id and secret, checked by OpenIddict's application manager on the same application records;
/// and HTTP Basic with a launch token. An application's permissions come from its roles, as for a bearer token.
/// </summary>
public sealed class XapiAuthenticator
{
    private readonly IServiceProvider _services;
    private readonly IAuthorizationService _authorizationService;
    private readonly XapiLaunchTokens _launchTokens;
    private readonly XapiHomePage _homePage;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XapiAuthenticator"/> class.
    /// </summary>
    public XapiAuthenticator(
        IServiceProvider services,
        IAuthorizationService authorizationService,
        XapiLaunchTokens launchTokens,
        XapiHomePage homePage,
        ILogger<XapiAuthenticator> logger)
    {
        _services = services;
        _authorizationService = authorizationService;
        _launchTokens = launchTokens;
        _homePage = homePage;
        _logger = logger;
    }

    /// <summary>
    /// Finds the credential of a request.
    /// </summary>
    /// <param name="httpContext">The HTTP context, for the bearer token.</param>
    /// <param name="request">The request, whose Authorization header is read (also from the alternate request syntax).</param>
    /// <returns>The credential, or <see langword="null"/> when the request has none that is valid.</returns>
    public async Task<XapiCredential> AuthenticateAsync(HttpContext httpContext, XapiRequest request)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(request);

        var authorization = request.Header("Authorization").Trim();

        if (authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return await BasicAsync(authorization[6..].Trim(), httpContext.RequestAborted);
        }

        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return await BearerAsync(httpContext);
        }

        return null;
    }

    private async Task<XapiCredential> BasicAsync(string value, CancellationToken cancellationToken)
    {
        string decoded;

        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return null;
        }

        var separator = decoded.IndexOf(':');

        if (separator <= 0)
        {
            return null;
        }

        var userName = decoded[..separator];
        var password = decoded[(separator + 1)..];

        if (string.Equals(userName, XapiLaunchTokens.BasicUserName, StringComparison.Ordinal))
        {
            return await LaunchAsync(password);
        }

        var applications = _services.GetService<IOpenIdApplicationManager>();

        if (applications is null || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var application = await applications.FindByClientIdAsync(userName, cancellationToken);

        // A public client has no secret to check, so it cannot use HTTP Basic.
        if (application is null ||
            !await applications.HasClientTypeAsync(application, OpenIddictConstants.ClientTypes.Confidential, cancellationToken) ||
            !await applications.ValidateClientSecretAsync(application, password, cancellationToken))
        {
            _logger.LogInformation("An LRS request with HTTP Basic was refused: the client id or the secret is not valid.");

            return null;
        }

        var name = await applications.GetDisplayNameAsync(application, cancellationToken) ?? userName;
        var identity = new ClaimsIdentity("Basic", ClaimTypes.Name, ClaimTypes.Role);
        var roleService = _services.GetService<IRoleService>();

        identity.AddClaim(new Claim(ClaimTypes.Name, name));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.ClientId, userName));

        foreach (var role in await applications.GetRolesAsync(application, cancellationToken))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

            if (roleService is not null)
            {
                identity.AddClaims(await roleService.GetRoleClaimsAsync(role, cancellationToken));
            }
        }

        return await ClientAsync(new ClaimsPrincipal(identity), userName, name);
    }

    private async Task<XapiCredential> BearerAsync(HttpContext httpContext)
    {
        AuthenticateResult result;

        try
        {
            result = await httpContext.AuthenticateAsync(OrchardCoreConstants.AuthenticationSchemes.Api);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "The Api authentication scheme could not authenticate an LRS request.");

            return null;
        }

        var principal = result?.Succeeded == true ? result.Principal : null;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var clientId = principal.FindFirstValue(OpenIddictConstants.Claims.ClientId)
            ?? principal.FindFirstValue(OpenIddictConstants.Claims.AuthorizedParty)
            ?? principal.FindFirstValue(OpenIddictConstants.Claims.Subject);

        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        // A token issued to a person through a client (its subject is the person, not the client) would make every
        // person of that client share the client's own statements: only client credentials tokens are taken.
        var subject = principal.FindFirstValue(OpenIddictConstants.Claims.Subject);

        if (!string.IsNullOrEmpty(subject) && !string.Equals(subject, clientId, StringComparison.Ordinal))
        {
            return null;
        }

        return await ClientAsync(principal, clientId, principal.Identity.Name ?? clientId);
    }

    private async Task<XapiCredential> ClientAsync(ClaimsPrincipal principal, string clientId, string name)
    {
        var homePage = await _homePage.GetAsync();

        return new XapiCredential
        {
            ClientId = clientId,
            Name = name,
            AuthorityKey = "client:" + clientId,
            Authority = new JsonObject
            {
                ["objectType"] = "Agent",
                ["name"] = name,
                ["account"] = new JsonObject
                {
                    ["homePage"] = homePage,
                    ["name"] = clientId,
                },
            },
            CanWriteStatements = await _authorizationService.AuthorizeAsync(principal, LearningXapiPermissions.WriteLearningStatements),
            CanReadAllStatements = await _authorizationService.AuthorizeAsync(principal, LearningXapiPermissions.ReadAllLearningStatements),
            CanReadOwnStatements = await _authorizationService.AuthorizeAsync(principal, LearningXapiPermissions.ReadOwnLearningStatements),
            CanManageDocuments = await _authorizationService.AuthorizeAsync(principal, LearningXapiPermissions.ManageLearningDocuments),
        };
    }

    private async Task<XapiCredential> LaunchAsync(string token)
    {
        var scope = _launchTokens.Read(token);

        if (scope is null)
        {
            return null;
        }

        var homePage = await _homePage.GetAsync();

        return new XapiCredential
        {
            Name = "Learning launch",
            AuthorityKey = "launch:" + (scope.Kind ?? "xapi"),
            Authority = new JsonObject
            {
                ["objectType"] = "Agent",
                ["name"] = "Learning launch",
                ["account"] = new JsonObject
                {
                    ["homePage"] = homePage,
                    ["name"] = "launch:" + (scope.Kind ?? "xapi"),
                },
            },
            Launch = scope,
        };
    }
}
