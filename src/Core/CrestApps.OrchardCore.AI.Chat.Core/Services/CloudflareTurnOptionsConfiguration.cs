using CrestApps.Core.AI.Chat.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Chat.Core.Services;

/// <summary>
/// Re-binds <see cref="CloudflareTurnOptions"/> from the tenant's shell configuration.
/// </summary>
/// <remarks>
/// CrestApps.Core binds these options from <see cref="IConfiguration"/>, which under Orchard Core resolves to
/// the host configuration alone. That would leave the TURN token settable only at the host, so every tenant
/// would mint its relay credentials from one Cloudflare account and none could be billed or rotated on its
/// own. Running as a post-configure step, this reapplies the same section from <see cref="IShellConfiguration"/>
/// after the core registration whatever order the modules load in, so a value set for a tenant wins and one it
/// leaves alone keeps whatever the host provided.
/// </remarks>
internal sealed class CloudflareTurnOptionsConfiguration : IPostConfigureOptions<CloudflareTurnOptions>
{
    /// <summary>
    /// The configuration section, matching the one CrestApps.Core binds from the host configuration so a
    /// value moved from <c>appsettings.json</c> into a tenant's own configuration keeps its spelling.
    /// </summary>
    public const string ConfigurationSection = "CrestApps:AI:RealtimeTransport:Cloudflare";

    private readonly IShellConfiguration _shellConfiguration;

    public CloudflareTurnOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void PostConfigure(string name, CloudflareTurnOptions options)
    {
        var section = _shellConfiguration.GetSection(ConfigurationSection);

        if (!section.Exists())
        {
            return;
        }

        section.Bind(options);
    }
}
