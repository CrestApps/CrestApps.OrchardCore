namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Extensions for <see cref="ITextToSpeechClient"/>.
/// </summary>
public static class TextToSpeechClientExtensions
{
    /// <summary>
    /// Asks the <see cref="ITextToSpeechClient"/> for an object of type <typeparamref name="TService"/>.
    /// </summary>
    /// <typeparam name="TService">The type of the object to be retrieved.</typeparam>
    /// <param name="client">The client.</param>
    /// <param name="serviceKey">An optional key that can be used to help identify the target service.</param>
    /// <returns>The found object, otherwise <see langword="null"/>.</returns>
    public static TService GetService<TService>(this ITextToSpeechClient client, object serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        return (TService)client.GetService(typeof(TService), serviceKey);
    }
}
