using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Services;

internal sealed class ChatInteractionMemoryOptionsConfiguration : IConfigureOptions<ChatInteractionMemoryOptions>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionMemoryOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public ChatInteractionMemoryOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Configures the .
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(ChatInteractionMemoryOptions options)
    {
        var settings = _siteService.GetSettings<MemoryMetadata>();

        options.EnableUserMemory = settings.EnableUserMemory ?? false;
    }
}
