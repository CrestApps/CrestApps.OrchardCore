using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Chat.Services;

internal sealed class AIChatOptionsConfiguration : IConfigureOptions<AIChatOptions>
{
    private readonly IShellConfiguration _shellConfiguration;
    private readonly ILogger _logger;

    public AIChatOptionsConfiguration(
        IShellConfiguration shellConfiguration,
        ILogger<AIChatOptionsConfiguration> logger)
    {
        _shellConfiguration = shellConfiguration;
        _logger = logger;
    }

    public void Configure(AIChatOptions options)
    {
        var chatSettings = _shellConfiguration.GetSection("CrestApps_AI:Chat");

        if (chatSettings is null)
        {
            _logger.LogDebug("The 'CrestApps_AI:Chat' section is not defined in the settings. Using default values.");
            return;
        }

        try
        {
            var maxAudioSizeValue = chatSettings["MaxAudioSizeInBytes"];

            if (maxAudioSizeValue != null)
            {
                // User explicitly provided a value
                if (long.TryParse(maxAudioSizeValue, out var maxSize))
                {
                    options.MaxAudioSizeInBytes = maxSize;
                    _logger.LogDebug("MaxAudioSizeInBytes configured to {MaxSize} bytes from settings.", maxSize);
                }
                else
                {
                    _logger.LogWarning("Invalid MaxAudioSizeInBytes value '{Value}' in settings. Using default.", maxAudioSizeValue);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring AIChatOptions from settings. Using default values.");
        }
    }
}
