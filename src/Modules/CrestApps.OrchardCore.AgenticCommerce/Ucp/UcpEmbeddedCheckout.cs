using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Declares UCP embedded checkout in the store's profile, as the <c>embedded</c> transport, when embedded checkout is
/// allowed and a platform lists the origins that may frame it.
/// </summary>
public sealed class UcpEmbeddedProfileContributor : IUcpProfileContributor
{
    private readonly EmbeddedCheckoutPolicy _embedded;

    /// <summary>
    /// Initializes a new instance of the <see cref="UcpEmbeddedProfileContributor"/> class.
    /// </summary>
    /// <param name="embedded">Who may frame embedded checkout.</param>
    public UcpEmbeddedProfileContributor(EmbeddedCheckoutPolicy embedded)
    {
        _embedded = embedded;
    }

    /// <inheritdoc />
    public async Task ContributeAsync(UcpProfileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!await _embedded.IsOfferedAsync())
        {
            return;
        }

        context.Services.Add(new UcpService
        {
            Transport = UcpConstants.Embedded.Transport,
            Schema = UcpConstants.Url(context.Version, UcpConstants.Embedded.SchemaPath),
            Config = UcpEmbeddedCheckoutExtension.WriteConfig(),
        });
    }
}

/// <summary>
/// Tells the platform, in each checkout answer, that this checkout can be embedded from its <c>continue_url</c> and
/// which delegations the store accepts (ECP requires an embedded service binding in the checkout's <c>ucp</c>).
/// </summary>
public sealed class UcpEmbeddedCheckoutExtension : IUcpCheckoutExtension
{
    private readonly EmbeddedCheckoutPolicy _embedded;

    /// <summary>
    /// Initializes a new instance of the <see cref="UcpEmbeddedCheckoutExtension"/> class.
    /// </summary>
    /// <param name="embedded">Who may frame embedded checkout.</param>
    public UcpEmbeddedCheckoutExtension(EmbeddedCheckoutPolicy embedded)
    {
        _embedded = embedded;
    }

    /// <inheritdoc />
    public async Task WritingAsync(UcpCheckoutContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Json?["ucp"] is not JsonObject ucp ||
            !IsEmbeddable(context.View?.ContinueUrl) ||
            (await _embedded.GetFramingOriginsAsync(context.Request?.Platform)).Count == 0)
        {
            return;
        }

        AddBinding(ucp, context.Request.Version);
    }

    /// <inheritdoc />
    public Task<AgentCheckoutMessage> CompletingAsync(UcpCheckoutContext context)
        => Task.FromResult<AgentCheckoutMessage>(null);

    /// <inheritdoc />
    public Task CompletedAsync(UcpCheckoutContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Returns the configuration of the embedded transport: the delegations the store accepts.
    /// </summary>
    public static JsonObject WriteConfig()
        => new()
        {
            ["delegate"] = new JsonArray([.. UcpConstants.Embedded.Delegations.Select(name => (JsonNode)name)]),
        };

    /// <summary>
    /// Adds the embedded service binding to a checkout answer's <c>ucp</c> member.
    /// </summary>
    /// <param name="ucp">The answer's <c>ucp</c> member.</param>
    /// <param name="version">The answer's version.</param>
    public static void AddBinding(JsonObject ucp, string version)
    {
        ArgumentNullException.ThrowIfNull(ucp);

        if (ucp["services"] is not JsonObject services)
        {
            services = [];
            ucp["services"] = services;
        }

        if (services["dev.ucp.shopping"] is not JsonArray shopping)
        {
            shopping = [];
            services["dev.ucp.shopping"] = shopping;
        }

        shopping.Add(new JsonObject
        {
            ["version"] = version,
            ["transport"] = UcpConstants.Embedded.Transport,
            ["config"] = WriteConfig(),
        });
    }

    // Only the store's own hand-off link can be embedded; a bank's 3-D Secure page the checkout escalates to can't.
    private static bool IsEmbeddable(string continueUrl)
        => !string.IsNullOrEmpty(continueUrl) &&
            Uri.TryCreate(continueUrl, UriKind.Absolute, out var uri) &&
            uri.AbsolutePath.Contains("/" + AgentSiteUrls.HandoffPath + "/", StringComparison.Ordinal);
}
