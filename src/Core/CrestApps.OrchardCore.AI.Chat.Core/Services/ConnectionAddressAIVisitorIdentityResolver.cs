using System.Security.Cryptography;
using System.Text;
using CrestApps.Core.AI.Chat.Security;
using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.Core.Throttling;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Chat.Core.Services;

/// <summary>
/// Resolves the AI chat visitor from CrestApps.Core's resolver, but takes the visitor's address from the connection
/// (through <see cref="ClientNetworkKey"/>) instead of the first <c>X-Forwarded-For</c> value of the raw request. The
/// package's resolver trusts that header, which any client can set, so a visitor could pick a new address for every
/// message and never be counted by the address-keyed chat limits. Behind a proxy, enable Orchard Core's Reverse Proxy
/// feature: it moves the forwarded address onto the connection.
/// </summary>
public sealed class ConnectionAddressAIVisitorIdentityResolver : IAIVisitorIdentityResolver
{
    // Kept equal to the package's purpose, so addresses stored by either resolver can be read by the other.
    private const string RemoteAddressProtectionPurpose = "CrestApps.Core.AI.VisitorIdentity.RemoteAddress";

    private readonly DefaultAIVisitorIdentityResolver _inner;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<AIVisitorIdentityOptions> _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IDataProtector _remoteAddressProtector;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionAddressAIVisitorIdentityResolver"/> class.
    /// </summary>
    public ConnectionAddressAIVisitorIdentityResolver(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AIVisitorIdentityOptions> options,
        IHostEnvironment hostEnvironment,
        IDataProtectionProvider dataProtectionProvider)
    {
        _inner = new DefaultAIVisitorIdentityResolver(httpContextAccessor, options, hostEnvironment, dataProtectionProvider);
        _httpContextAccessor = httpContextAccessor;
        _options = options;
        _hostEnvironment = hostEnvironment;
        _remoteAddressProtector = dataProtectionProvider.CreateProtector(hostEnvironment.ApplicationName, RemoteAddressProtectionPurpose);
    }

    /// <inheritdoc/>
    public AIVisitorIdentity Resolve()
    {
        var identity = _inner.Resolve();
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return identity;
        }

        var address = ClientNetworkKey.NetworkOnly(httpContext);

        if (string.Equals(address, ClientNetworkKey.Unknown, StringComparison.Ordinal))
        {
            identity.RemoteAddress = null;
            identity.RemoteAddressHash = null;

            return identity;
        }

        var options = _options.Value;

        identity.RemoteAddressHash = options.RemoteAddressMode is AIVisitorRemoteAddressMode.Hashed or AIVisitorRemoteAddressMode.Encrypted
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{_hostEnvironment.ApplicationName}|{options.RemoteAddressHashSalt}|{address}")))
            : null;

        identity.RemoteAddress = options.RemoteAddressMode switch
        {
            AIVisitorRemoteAddressMode.PlainText => address,
            AIVisitorRemoteAddressMode.Encrypted => _remoteAddressProtector.Protect(address),
            _ => null,
        };

        return identity;
    }
}
