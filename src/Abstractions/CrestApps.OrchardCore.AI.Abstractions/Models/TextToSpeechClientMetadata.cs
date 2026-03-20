namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Provides metadata about an <see cref="ITextToSpeechClient"/>.
/// </summary>
public sealed class TextToSpeechClientMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechClientMetadata"/> class.
    /// </summary>
    /// <param name="providerName">The name of the text to speech provider, if applicable.</param>
    /// <param name="providerUri">The URL for accessing the text to speech provider, if applicable.</param>
    /// <param name="defaultModelId">The ID of the text to speech model used by default, if applicable.</param>
    public TextToSpeechClientMetadata(string providerName = null, Uri providerUri = null, string defaultModelId = null)
    {
        DefaultModelId = defaultModelId;
        ProviderName = providerName;
        ProviderUri = providerUri;
    }

    /// <summary>
    /// Gets the name of the text to speech provider.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the URL for accessing the text to speech provider.
    /// </summary>
    public Uri ProviderUri { get; }

    /// <summary>
    /// Gets the ID of the default model used by this text to speech client.
    /// </summary>
    public string DefaultModelId { get; }
}
