using CrestApps.Core.AI.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Re-binds <see cref="RealtimeTransportOptions"/> from the tenant's shell configuration.
/// </summary>
/// <remarks>
/// CrestApps.Core binds these options from <see cref="IConfiguration"/>, which under Orchard Core resolves to
/// the host configuration alone. That left the realtime transport — the STUN and TURN servers above all —
/// settable only at the host, so every tenant shared one set of relay credentials and none could be given its
/// own. Running as a post-configure step, this reapplies the same section from <see cref="IShellConfiguration"/>
/// after the core registration whatever order the modules load in, so a value set for a tenant wins and one it
/// leaves alone keeps whatever the host provided.
/// </remarks>
internal sealed class RealtimeTransportOptionsConfiguration : IPostConfigureOptions<RealtimeTransportOptions>
{
    public const string ConfigurationSection = "CrestApps:AI:RealtimeTransport";

    private readonly IShellConfiguration _shellConfiguration;

    public RealtimeTransportOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void PostConfigure(string name, RealtimeTransportOptions options)
    {
        var section = _shellConfiguration.GetSection(ConfigurationSection);

        if (!section.Exists())
        {
            return;
        }

        // The configuration binder appends to an array property rather than replacing it, so binding this
        // section over values the host already supplied would leave every STUN and TURN URL listed twice. A
        // duplicated TURN URL is not harmless: it makes the peer allocate a second relay on the same server,
        // which is billed and needlessly slows gathering. Clear an array only when this section actually
        // declares it, so a tenant that overrides the URLs replaces them and one that does not keeps the host's.
        if (section.GetSection(nameof(RealtimeTransportOptions.StunUrls)).Exists())
        {
            options.StunUrls = [];
        }

        if (section.GetSection(nameof(RealtimeTransportOptions.TurnUrls)).Exists())
        {
            options.TurnUrls = [];
        }

        section.Bind(options);
    }
}
