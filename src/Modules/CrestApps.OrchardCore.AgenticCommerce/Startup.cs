using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers agent platforms, the merchant's signing keys and the protocol-neutral agent checkout.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}
