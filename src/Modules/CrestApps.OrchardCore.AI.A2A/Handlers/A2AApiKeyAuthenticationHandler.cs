using System.Text.Encodings.Web;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.A2A.Handlers;

/// <summary>
/// Signs an A2A caller in with the A2A host's configured API key, through the shared
/// <see cref="ConfiguredApiKeyAuthenticationHandler{TOptions}"/>.
/// </summary>
internal sealed class A2AApiKeyAuthenticationHandler : ConfiguredApiKeyAuthenticationHandler<A2AApiKeyAuthenticationOptions>
{
    private readonly IOptionsMonitor<A2AHostOptions> _hostOptionsMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AApiKeyAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="hostOptionsMonitor">The host options monitor.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="encoder">The encoder.</param>
    public A2AApiKeyAuthenticationHandler(
        IOptionsMonitor<A2AApiKeyAuthenticationOptions> options,
        IOptionsMonitor<A2AHostOptions> hostOptionsMonitor,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder)
    {
        _hostOptionsMonitor = hostOptionsMonitor;
    }

    protected override bool IsApiKeyAuthenticationEnabled
        => _hostOptionsMonitor.CurrentValue.AuthenticationType == A2AHostAuthenticationType.ApiKey;

    protected override string ConfiguredApiKey => _hostOptionsMonitor.CurrentValue.ApiKey;

    protected override string CallerId => "a2a-api-key-user";

    protected override string CallerName => "A2A API Key User";
}

internal sealed class A2AApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

internal static class A2AApiKeyAuthenticationDefaults
{
    /// <summary>
    /// The authentication scheme constant.
    /// </summary>
    public const string AuthenticationScheme = "A2AApiKey";
}
