using System.Net;
using System.Net.Http;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Checks a request with the <see cref="EgressGuard"/> before it is sent, for HTTP clients whose transport the guard
/// cannot replace.
/// </summary>
internal sealed class EgressGuardDelegatingHandler : DelegatingHandler
{
    private readonly EgressGuard _guard;

    /// <summary>
    /// Initializes a new instance of the <see cref="EgressGuardDelegatingHandler"/> class.
    /// </summary>
    /// <param name="guard">The egress guard.</param>
    public EgressGuardDelegatingHandler(EgressGuard guard)
    {
        _guard = guard;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;

        if (uri is not null && uri.IsAbsoluteUri)
        {
            _guard.EnsureHostAllowed(uri.Host, uri.Port);

            var addresses = IPAddress.TryParse(uri.Host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);

            _guard.EnsureAddressesAllowed(uri.Host, addresses);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
