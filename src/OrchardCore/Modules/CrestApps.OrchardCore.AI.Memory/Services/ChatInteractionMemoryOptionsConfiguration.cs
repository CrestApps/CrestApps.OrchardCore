using CrestApps.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Services;

internal sealed class ChatInteractionMemoryOptionsConfiguration : IConfigureOptions<ChatInteractionMemoryOptions>
{
    private readonly ISiteService _siteService;

    public ChatInteractionMemoryOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(ChatInteractionMemoryOptions options)
    {
        var settings = _siteService.GetSettings<MemoryMetadata>();

        options.EnableUserMemory = ChatInteractionMemoryOptions.FromMetadata(settings).EnableUserMemory;
    }
}
