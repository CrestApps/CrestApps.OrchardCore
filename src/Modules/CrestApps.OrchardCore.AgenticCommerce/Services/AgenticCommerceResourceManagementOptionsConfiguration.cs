using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AgenticCommerce.Services;

/// <summary>
/// Registers the client-side resources of agentic commerce.
/// </summary>
public sealed class AgenticCommerceResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    /// <summary>
    /// The name of the script that speaks UCP's Embedded Checkout Protocol to the agent platform.
    /// </summary>
    public const string EmbeddedCheckoutScript = "agentic-commerce-embedded-checkout";

    private static readonly ResourceManifest _manifest;

    static AgenticCommerceResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(EmbeddedCheckoutScript)
            .SetUrl("~/CrestApps.OrchardCore.AgenticCommerce/Scripts/embedded-checkout.min.js", "~/CrestApps.OrchardCore.AgenticCommerce/Scripts/embedded-checkout.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
